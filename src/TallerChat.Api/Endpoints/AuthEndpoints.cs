using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;

namespace TallerChat.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");
        g.MapPost("/register", RegisterAsync).RequireRateLimiting("auth");
        g.MapPost("/login", LoginAsync).RequireRateLimiting("auth");
        g.MapPost("/logout", LogoutAsync);
        g.MapGet("/me", MeAsync).RequireAuthorization();
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest req, Db db, HttpContext http, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 2 or > 80) return ApiResults.Error(400, "Escribe tu nombre (entre 2 y 80 caracteres).");
        if (!Validation.TryEmail(req.Email, out var email)) return ApiResults.Error(400, "Introduce un correo electrónico válido.");
        if (!Validation.TryPhone(req.Phone, out var phone)) return ApiResults.Error(400, "El teléfono no es válido.");
        if (!Validation.StrongPassword(req.Password))
            return ApiResults.Error(400, "La contraseña debe tener al menos 10 caracteres, con letras y números.");

        var user = new AppUser { Name = name, Email = email, Phone = phone, PasswordHash = PasswordHasher.Hash(req.Password!) };
        try { await db.Users.InsertOneAsync(user, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ApiResults.Error(409, "Ya existe una cuenta con ese correo. Prueba a iniciar sesión.");
        }

        await AuthSetup.SignInAsync(http, user);
        return Results.Json(user.ToDto(), statusCode: 201);
    }

    private static async Task<IResult> LoginAsync(LoginRequest req, Db db, HttpContext http, CancellationToken ct)
    {
        const string Bad = "Correo o contraseña incorrectos.";
        if (!Validation.TryEmail(req.Email, out var email) || string.IsNullOrEmpty(req.Password) || req.Password.Length > 100)
            return ApiResults.Error(401, Bad);

        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
        var valid = PasswordHasher.Verify(req.Password, user?.PasswordHash ?? PasswordHasher.Dummy);
        if (user is null || !valid) return ApiResults.Error(401, Bad);
        if (!user.Active) return ApiResults.Error(403, "Tu cuenta está desactivada. Contacta con el taller.");

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
}
