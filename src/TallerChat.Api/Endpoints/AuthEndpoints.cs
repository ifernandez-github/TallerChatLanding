using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

public static class AuthEndpoints
{
    /// <summary>Códigos que la web usa para ofrecer la acción adecuada sin depender del texto del error.</summary>
    public const string NotVerified = "email_not_verified", BadToken = "invalid_token";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");
        g.MapPost("/register", RegisterAsync).RequireRateLimiting("auth");
        g.MapPost("/login", LoginAsync).RequireRateLimiting("auth");
        g.MapPost("/logout", LogoutAsync);
        g.MapGet("/me", MeAsync).RequireAuthorization();
        g.MapPost("/verify", VerifyAsync).RequireRateLimiting("auth");
        g.MapPost("/resend", ResendAsync).RequireRateLimiting("auth");
        g.MapPost("/forgot", ForgotAsync).RequireRateLimiting("auth");
        g.MapPost("/reset", ResetAsync).RequireRateLimiting("auth");
    }

    // ---------- Alta ----------

    private static async Task<IResult> RegisterAsync(RegisterRequest req, Db db, TokenService tokens, Notifier notifier,
        IOptions<ShopOptions> shop, IWebHostEnvironment env, HttpContext http, CancellationToken ct)
    {
        if (!Validation.TryName(req.Name, out var name)) return ApiResults.Error(400, "Escribe tu nombre (entre 2 y 80 caracteres).");
        if (!Validation.TryEmail(req.Email, out var email)) return ApiResults.Error(400, "Introduce un correo electrónico válido.");
        if (!Validation.TryEmail(req.EmailConfirm, out var confirm) || confirm != email)
            return ApiResults.Error(400, "Los dos correos no coinciden. Repásalos antes de continuar.");
        if (!Notifier.Deliverable(email))
            return ApiResults.Error(400, "Ese dominio está reservado para los datos de ejemplo. Usa otra dirección.");
        if (!Validation.TryPhone(req.Phone, out var phone)) return ApiResults.Error(400, "El teléfono no es válido.");
        if (!Validation.StrongPassword(req.Password))
            return ApiResults.Error(400, "La contraseña debe tener al menos 10 caracteres, con letras y números.");

        // Sin SMTP nadie podría confirmar nunca su cuenta. En desarrollo se da por verificada para que la
        // aplicación siga siendo usable; fuera de desarrollo se rechaza el alta, para no quedarse sin
        // confirmación de correo por un secreto mal cargado sin que nadie se entere.
        var byEmail = notifier.Enabled;
        if (!byEmail && !env.IsDevelopment())
            return ApiResults.Error(503, "Ahora mismo no se pueden crear cuentas. Inténtalo más tarde o llama al taller.");
        var user = new AppUser
        {
            Name = name, Email = email, Phone = phone,
            PasswordHash = PasswordHasher.Hash(req.Password!),
            EmailVerified = !byEmail,
            SecurityStamp = NewStamp()
        };

        try { await db.Users.InsertOneAsync(user, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ApiResults.Error(409, "Ya existe una cuenta con ese correo. Prueba a iniciar sesión.");
        }

        if (!byEmail)
        {
            await AuthSetup.SignInAsync(http, user);
            return Results.Json(new RegisterResponse(true, false, true, email), statusCode: 201);
        }

        // CancellationToken.None: aunque el cliente cierre la pestaña, el correo de confirmación sale igualmente.
        var sent = await SendVerifyAsync(user, tokens, notifier, shop.Value, CancellationToken.None);
        return Results.Json(new RegisterResponse(true, sent, false, email), statusCode: 201);
    }

    // ---------- Sesión ----------

    private static async Task<IResult> LoginAsync(LoginRequest req, Db db, HttpContext http, CancellationToken ct)
    {
        const string Bad = "Correo o contraseña incorrectos.";
        if (!Validation.TryEmail(req.Email, out var email) || string.IsNullOrEmpty(req.Password) || req.Password.Length > 100)
            return ApiResults.Error(401, Bad);

        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
        var valid = PasswordHasher.Verify(req.Password, user?.PasswordHash ?? PasswordHasher.Dummy);
        if (user is null || !valid) return ApiResults.Error(401, Bad);
        if (!user.Active) return ApiResults.Error(403, "Tu cuenta está desactivada. Contacta con el taller.");
        if (!user.EmailVerified)
            return ApiResults.Error(403, "Antes de entrar tienes que confirmar tu correo. Busca el mensaje que te enviamos al crear la cuenta.", NotVerified);

        await AuthSetup.SignInAsync(http, user);
        return Results.Ok(user.ToDto());
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, Db db, CancellationToken ct)
    {
        var id = principal.UserId();
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return user is { Active: true } ? Results.Ok(user.ToDto()) : ApiResults.Error(401, "Sesión no válida.");
    }

    // ---------- Confirmar la cuenta ----------

    private static async Task<IResult> VerifyAsync(TokenRequest req, Db db, TokenService tokens, IMemoryCache cache, CancellationToken ct)
    {
        var user = await tokens.ConsumeAsync(req.Token, TokenKinds.Verify, ct);
        if (user is null)
            return ApiResults.Error(400, "Este enlace no es válido o ha caducado. Pide uno nuevo desde la pantalla de acceso.", BadToken);
        if (!user.Active) return ApiResults.Error(403, "Tu cuenta está desactivada. Contacta con el taller.");

        await db.Users.UpdateOneAsync(u => u.Id == user.Id,
            Builders<AppUser>.Update.Set(u => u.EmailVerified, true), cancellationToken: ct);
        Refresh(cache, user.Id, user.Role, user.SecurityStamp);
        return Results.Ok(new { verified = true, email = user.Email });
    }

    private static async Task<IResult> ResendAsync(AccountEmailRequest req, Db db, TokenService tokens, Notifier notifier,
        IOptions<ShopOptions> shop, CancellationToken ct)
    {
        if (!notifier.Enabled) return ApiResults.Error(503, "El envío por email no está configurado.");

        if (Validation.TryEmail(req.Email, out var email) && tokens.TryStartCooldown(email, TokenKinds.Verify))
        {
            var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
            if (user is { Active: true, EmailVerified: false })
                await SendVerifyAsync(user, tokens, notifier, shop.Value, CancellationToken.None);
        }
        // La respuesta es siempre la misma: no se revela si esa dirección tiene cuenta ni en qué estado está.
        return Results.Ok(new { sent = true });
    }

    // ---------- Contraseña olvidada ----------

    private static async Task<IResult> ForgotAsync(AccountEmailRequest req, Db db, TokenService tokens, Notifier notifier,
        IOptions<ShopOptions> shop, CancellationToken ct)
    {
        if (!notifier.Enabled) return ApiResults.Error(503, "El envío por email no está configurado.");

        if (Validation.TryEmail(req.Email, out var email) && tokens.TryStartCooldown(email, TokenKinds.Reset))
        {
            var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
            if (user is { Active: true })
            {
                var raw = await tokens.IssueAsync(user.Id, TokenKinds.Reset, ct);
                await notifier.TrySendResetAsync(user, TokenService.Link(shop.Value, "/restablecer", raw), CancellationToken.None);
            }
        }
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ResetAsync(ResetRequest req, Db db, TokenService tokens, IMemoryCache cache, CancellationToken ct)
    {
        if (!Validation.StrongPassword(req.Password))
            return ApiResults.Error(400, "La contraseña debe tener al menos 10 caracteres, con letras y números.");

        var user = await tokens.ConsumeAsync(req.Token, TokenKinds.Reset, ct);
        if (user is null)
            return ApiResults.Error(400, "Este enlace no es válido o ha caducado. Vuelve a pedir uno nuevo.", BadToken);
        if (!user.Active) return ApiResults.Error(403, "Tu cuenta está desactivada. Contacta con el taller.");

        // Quien llega aquí ha demostrado tener acceso al correo, así que la cuenta queda también verificada.
        // El sello nuevo invalida las sesiones que hubiera abiertas en otros dispositivos.
        var stamp = NewStamp();
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<AppUser>.Update
            .Set(u => u.PasswordHash, PasswordHasher.Hash(req.Password!))
            .Set(u => u.EmailVerified, true)
            .Set(u => u.SecurityStamp, stamp), cancellationToken: ct);

        await tokens.InvalidateAsync(user.Id, TokenKinds.Verify, ct);
        Refresh(cache, user.Id, user.Role, stamp);
        return Results.Ok(new { reset = true });
    }

    // ---------- Apoyo ----------

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Deja en la caché el estado recién guardado. Borrar la entrada no basta: una petición que estuviera
    /// leyendo el usuario justo antes del cambio volvería a dejar el estado antiguo durante un minuto.
    /// </summary>
    private static void Refresh(IMemoryCache cache, string userId, string role, string stamp) =>
        cache.Set(AuthSetup.CacheKey(userId), new UserState(true, role, stamp), TimeSpan.FromMinutes(1));

    private static async Task<bool> SendVerifyAsync(AppUser user, TokenService tokens, Notifier notifier, ShopOptions shop, CancellationToken ct)
    {
        var raw = await tokens.IssueAsync(user.Id, TokenKinds.Verify, ct);
        return await notifier.TrySendVerifyAsync(user, TokenService.Link(shop, "/verificar", raw), ct);
    }
}
