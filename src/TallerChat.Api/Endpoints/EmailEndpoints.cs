using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TallerChat.Api;

public static partial class EmailEndpoints
{
    private const int MaxItems = 5, MaxQuestion = 500, MaxAnswer = 4000, MaxSources = 8, MaxSections = 12, MaxTotalChars = 20_000;
    private static readonly TimeSpan RecipientCooldown = TimeSpan.FromMinutes(5);

    [GeneratedRegex(@"^[^@\s,;<>()\[\]\\""]+@[^@\s,;<>()\[\]\\""]+\.[^@\s,;<>()\[\]\\""]{2,}$")]
    private static partial Regex EmailRegex();

    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        // La web consulta esto para mostrar u ocultar el botón "Enviar por email".
        api.MapGet("/features", (IOptions<SmtpOptions> smtp) => Results.Ok(new { email = smtp.Value.Enabled }));
        api.MapPost("/email", SendAsync).RequireRateLimiting("email");
    }

    private static async Task<IResult> SendAsync(
        EmailRequest req, EmailService mail, IMemoryCache cache, IOptions<SmtpOptions> smtp,
        ILogger<EmailService> log, CancellationToken ct)
    {
        if (!smtp.Value.Enabled) return Error(503, "El envío por email no está configurado.");
        if (!TryValidate(req, out var to, out var items, out var problem)) return Error(400, problem);

        // Enfriamiento por destinatario (evita "mail bombing" a una víctima desde varias IP).
        // Solo se guarda un hash con caducidad, nunca la dirección.
        var key = "email:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(to.ToLowerInvariant())));
        if (cache.TryGetValue(key, out _))
            return Error(429, "Ya se envió un correo a esa dirección hace poco. Espera unos minutos.");
        cache.Set(key, true, RecipientCooldown);

        try
        {
            await mail.SendAsync(to, items, ct);
            return Results.Ok(new { sent = true });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cache.Remove(key);
            log.LogError("Fallo al enviar email: {Type}", ex.GetType().Name); // sin dirección ni mensaje (puede contenerla)
            return Error(502, "No se pudo enviar el correo ahora mismo. Inténtalo más tarde.");
        }
    }

    private static bool TryValidate(EmailRequest req, out string to, out List<EmailItem> items, out string problem)
    {
        to = req.To?.Trim() ?? "";
        items = req.Items ?? [];
        problem = "";

        if (to.Length > 254 || !EmailRegex().IsMatch(to) || !MailboxAddress.TryParse(to, out _))
            return Fail("Introduce una dirección de correo válida.", out problem);
        if (items.Count is 0 or > MaxItems)
            return Fail($"Selecciona entre 1 y {MaxItems} respuestas.", out problem);

        var total = 0;
        foreach (var i in items)
        {
            var q = i.Question?.Trim() ?? "";
            var a = i.Answer?.Trim() ?? "";
            if (q.Length is 0 || q.Length > MaxQuestion || a.Length is 0 || a.Length > MaxAnswer)
                return Fail("El contenido a enviar no es válido o es demasiado largo.", out problem);
            total += q.Length + a.Length;

            var sources = i.Sources ?? [];
            if (sources.Count > MaxSources) return Fail("Hay demasiadas fuentes en el contenido.", out problem);
            foreach (var s in sources)
            {
                var sections = s.Sections ?? [];
                if (sections.Count > MaxSections) return Fail("Hay demasiados campos en una fuente.", out problem);
                total += (s.Title?.Length ?? 0) + (s.Category?.Length ?? 0) + (s.SubCategory?.Length ?? 0) + (s.ChunkId?.Length ?? 0);
                total += sections.Sum(x => (x.Label?.Length ?? 0) + (x.Text?.Length ?? 0));
            }
        }
        return total <= MaxTotalChars || Fail("El contenido a enviar es demasiado largo. Selecciona menos respuestas.", out problem);
    }

    private static bool Fail(string message, out string problem) { problem = message; return false; }
    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
