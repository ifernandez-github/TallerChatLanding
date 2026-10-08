using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace TallerChat.Api;

public sealed record UserState(bool Active, string Role, string Stamp);

public static class TallerClaims
{
    /// <summary>Sello de seguridad: si cambia (al restablecer la contraseña), las sesiones abiertas dejan de valer.</summary>
    public const string Stamp = "taller:stamp";
}

/// <summary>
/// Sesión por cookie HttpOnly (SameSite=Lax) en el mismo origen que la web: el JavaScript no puede leerla y las
/// peticiones POST de otros sitios no la envían. En cada petición se comprueba (con caché de 1 minuto) que la cuenta
/// siga activa y conserve el rol.
/// </summary>
public static class AuthSetup
{
    public static string CacheKey(string userId) => "user-state:" + userId;

    public static IServiceCollection AddTallerAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "taller.auth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            // SameAsRequest: marca la cookie como Secure cuando la petición llega por HTTPS. Detrás de un proxy que
            // termina TLS hay que configurar ForwardedHeaders (ver README) para que lo detecte.
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromDays(7);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            o.Events.OnValidatePrincipal = ValidateAsync;
        });
        services.AddAuthorization(o => o.AddPolicy("admin", p => p.RequireRole(Roles.Admin)));
        return services;
    }

    private static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
        var stamp = ctx.Principal?.FindFirstValue(TallerClaims.Stamp) ?? "";
        if (id is null) { ctx.RejectPrincipal(); return; }

        var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        if (!cache.TryGetValue(CacheKey(id), out UserState? state))
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<Db>();
            var u = await db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);
            state = new UserState(u is { Active: true, EmailVerified: true }, u?.Role ?? "", u?.SecurityStamp ?? "");
            cache.Set(CacheKey(id), state, TimeSpan.FromMinutes(1));
        }

        if (state is null || !state.Active || state.Role != role || state.Stamp != stamp)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public static async Task SignInAsync(HttpContext http, AppUser user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, user.Role),
             new Claim(TallerClaims.Stamp, user.SecurityStamp)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
    }

    public static string UserId(this ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
