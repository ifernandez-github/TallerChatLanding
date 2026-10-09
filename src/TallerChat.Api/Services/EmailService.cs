using System.Text;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TallerChat.Api;

public sealed class EmailService(IOptions<SmtpOptions> options, GmailApiSender gmail)
{
    /// <summary>Correo del chat: respuestas del asistente y sus fuentes (asunto y plantilla fijos).</summary>
    public async Task SendAsync(string to, IReadOnlyList<EmailItem> items, CancellationToken ct)
    {
        var (html, text) = EmailTemplate.Render(items);
        var msg = NewMessage(to, "Respuesta del asistente del taller"); // fijo: el visitante no controla el asunto
        msg.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();
        await DeliverAsync(msg, ct);
    }

    /// <summary>Correo de citas: el contenido lo genera <see cref="AppointmentEmails"/>, nunca el cliente.</summary>
    public async Task SendMessageAsync(string to, string subject, string html, string text, string? ics, CancellationToken ct)
    {
        var msg = NewMessage(to, subject);
        var body = new BodyBuilder { HtmlBody = html, TextBody = text };
        if (ics is not null) body.Attachments.Add("cita.ics", Encoding.UTF8.GetBytes(ics), ContentType.Parse("text/calendar; charset=utf-8; method=PUBLISH"));
        msg.Body = body.ToMessageBody();
        await DeliverAsync(msg, ct);
    }

    private MimeMessage NewMessage(string to, string subject)
    {
        var o = options.Value;
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(o.FromName, o.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        return msg;
    }

    private async Task DeliverAsync(MimeMessage msg, CancellationToken ct)
    {
        var o = options.Value;
        if (o.UseGmailApi) { await gmail.SendAsync(msg, ct); return; }
        using var smtp = new SmtpClient { Timeout = 15_000 };
        await smtp.ConnectAsync(o.Host, o.Port, o.Security, ct);
        if (!string.IsNullOrEmpty(o.User)) await smtp.AuthenticateAsync(o.User, o.Password, ct);
        await smtp.SendAsync(msg, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
