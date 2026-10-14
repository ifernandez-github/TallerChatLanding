using System.Text.Json;
using System.Text.RegularExpressions;
using MimeKit;

namespace TallerChat.Api.Tests.Infrastructure;

public sealed record CapturedEmail(string To, string Subject, string Html, string Text, IReadOnlyList<string> Attachments)
{
    /// <summary>Primer enlace del correo (texto plano) cuyo camino empieza por <paramref name="path"/>.</summary>
    public string LinkTo(string path)
    {
        var m = Regex.Match(Text, @"https?://\S+?" + Regex.Escape(path) + @"\S*");
        Assert.True(m.Success, $"El correo «{Subject}» no contiene un enlace a {path}.\n{Text}");
        return m.Value;
    }

    /// <summary>Valor del parámetro <c>token</c> del enlace a <paramref name="path"/>.</summary>
    public string TokenFor(string path) =>
        Uri.UnescapeDataString(Regex.Match(LinkTo(path), @"[?&]token=([^&\s]+)").Groups[1].Value);
}

/// <summary>
/// Hace de Gmail API + OAuth de Google. La API envía los correos por <see cref="GmailApiSender"/>; aquí se
/// interceptan y se decodifican, de modo que los tests ven el correo MIME real (asunto, cuerpo, adjuntos, enlaces)
/// sin cambiar ni una línea del código de producción.
/// </summary>
public sealed class GoogleFake : FakeHttpHandler
{
    private readonly object gate = new();
    private readonly List<CapturedEmail> emails = [];
    private int tokenRequests;

    /// <summary>Si es true, Gmail responde 500: simula una caída del proveedor de correo.</summary>
    public volatile bool FailSends;

    public GoogleFake()
    {
        Responder = Handle;
    }

    public IReadOnlyList<CapturedEmail> Emails { get { lock (gate) return [.. emails]; } }
    public int TokenRequests => tokenRequests;

    public IReadOnlyList<CapturedEmail> To(string address) =>
        Emails.Where(e => e.To.Equals(address, StringComparison.OrdinalIgnoreCase)).ToList();

    public void ClearEmails() { lock (gate) emails.Clear(); }

    private HttpResponseMessage Handle(RecordedRequest r)
    {
        if (r.Uri.Host == "oauth2.googleapis.com")
        {
            Interlocked.Increment(ref tokenRequests);
            return Json("""{"access_token":"fake-access-token","expires_in":3600}""");
        }
        if (r.Uri.Host == "gmail.googleapis.com")
        {
            if (FailSends) return Status(System.Net.HttpStatusCode.InternalServerError);
            var raw = JsonDocument.Parse(r.Body).RootElement.GetProperty("raw").GetString()!;
            var b64 = raw.Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var ms = new MemoryStream(Convert.FromBase64String(b64));
            var msg = MimeMessage.Load(ms);
            var attachments = msg.Attachments.OfType<MimePart>().Select(a => a.FileName ?? "").ToList();
            lock (gate)
                emails.Add(new CapturedEmail(msg.To.Mailboxes.Single().Address, msg.Subject ?? "", msg.HtmlBody ?? "", msg.TextBody ?? "", attachments));
            return Json("""{"id":"1"}""");
        }
        return Status(System.Net.HttpStatusCode.NotFound);
    }
}
