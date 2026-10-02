using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TallerChat.Api;

public sealed class EmailService(IOptions<SmtpOptions> options)
{
    public async Task SendAsync(string to, IReadOnlyList<EmailItem> items, CancellationToken ct)
    {
        var o = options.Value;
        var (html, text) = EmailTemplate.Render(items);

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(o.FromName, o.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = "Respuesta del asistente del taller"; // fijo: el visitante no controla el asunto
        msg.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

        using var smtp = new SmtpClient { Timeout = 15_000 };
        await smtp.ConnectAsync(o.Host, o.Port, o.Security, ct);
        if (!string.IsNullOrEmpty(o.User)) await smtp.AuthenticateAsync(o.User, o.Password, ct);
        await smtp.SendAsync(msg, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
