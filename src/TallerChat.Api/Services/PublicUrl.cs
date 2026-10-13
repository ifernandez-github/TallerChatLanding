namespace TallerChat.Api;

/// <summary>
/// URL pública de la web, la que llevan los enlaces de los correos (confirmar cuenta, restablecer contraseña,
/// "Ver mis citas", enlace a la agenda). Orden de preferencia:
/// 1) <c>Shop:PublicBaseUrl</c>, si es una URL real (no localhost);
/// 2) <c>RENDER_EXTERNAL_URL</c>, que Render define sola con la dirección del servicio;
/// 3) la dirección desde la que llega la primera petición (ver <see cref="UsePublicUrlFromRequest"/>).
/// </summary>
public static class PublicUrl
{
    /// <summary>Normaliza una URL base: añade https:// si falta el esquema y quita la barra final. Null si no es válida.</summary>
    public static string? Normalize(string? url)
    {
        var s = url?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (!s.Contains("://", StringComparison.Ordinal)) s = "https://" + s;
        return Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
            ? u.GetLeftPart(UriPartial.Authority) + u.AbsolutePath.TrimEnd('/')
            : null;
    }

    /// <summary>¿Es una dirección que un cliente de correo no podría abrir (vacía, no válida o de la propia máquina)?</summary>
    public static bool IsLocal(string? url) =>
        Normalize(url) is not { } n || new Uri(n).IsLoopback;

    /// <summary>La URL configurada si vale; si no, la que da el hosting; si tampoco, la configurada tal cual.</summary>
    public static string Resolve(string? configured, string? hosting) =>
        !IsLocal(configured) ? Normalize(configured)!
        : !IsLocal(hosting) ? Normalize(hosting)!
        : configured ?? "";

    /// <summary>
    /// Último recurso fuera de desarrollo: si la URL sigue siendo de localhost, se toma la dirección de la primera
    /// petición (con ForwardedHeaders ya aplicado, así que es la pública y no la interna del contenedor).
    /// Lo recomendable es fijar <c>Shop__PublicBaseUrl</c>: una cabecera Host manipulada solo podría influir
    /// mientras esa variable falte, y una vez fijada la dirección no cambia hasta reiniciar.
    /// </summary>
    public static void UsePublicUrlFromRequest(this WebApplication app)
    {
        if (app.Environment.IsDevelopment()) return;
        app.Use(async (ctx, next) =>
        {
            var shop = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ShopOptions>>().Value;
            if (IsLocal(shop.PublicBaseUrl) && Normalize($"{ctx.Request.Scheme}://{ctx.Request.Host}") is { } origin
                && !IsLocal(origin))
            {
                shop.PublicBaseUrl = origin;
                app.Logger.LogWarning("Shop:PublicBaseUrl no está configurada: los enlaces de los correos usarán {Origin}. " +
                    "Define Shop__PublicBaseUrl para fijarla.", origin);
            }
            await next();
        });
    }
}
