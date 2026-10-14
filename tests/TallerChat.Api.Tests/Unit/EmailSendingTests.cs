using System.Text;
using Microsoft.Extensions.Options;
using MimeKit;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Unit;

/// <summary>El envío por la API de Gmail (el que usa Render, que bloquea SMTP), comprobado sobre el mensaje MIME real.</summary>
public class EmailSendingTests
{
    private readonly GoogleFake google = new();

    private static SmtpOptions Smtp() => new()
    {
        From = "taller@correo-real.test", FromName = "Taller Torque",
        Gmail = new GmailOptions { ClientId = "cid", ClientSecret = "secret", RefreshToken = "refresh" }
    };

    private (GmailApiSender Sender, EmailService Email) Services()
    {
        var opts = Options.Create(Smtp());
        var sender = new GmailApiSender(new HttpClient(google), opts);
        return (sender, new EmailService(opts, sender));
    }

    [Fact]
    public void Con_refresh_token_el_envio_va_por_la_API_de_Gmail_y_sin_el_queda_desactivado()
    {
        Assert.True(Smtp().UseGmailApi);
        Assert.True(Smtp().Enabled);

        var onlyFrom = new SmtpOptions { From = "a@b.test" };
        Assert.False(onlyFrom.Enabled); // sin Host ni Gmail: la función de email queda apagada
        Assert.True(new SmtpOptions { From = "a@b.test", Host = "smtp.test" }.Enabled);
        Assert.False(new SmtpOptions { Host = "smtp.test" }.Enabled); // sin remitente tampoco
    }

    [Fact]
    public async Task El_mensaje_llega_a_Gmail_con_destinatario_asunto_y_cuerpo_y_con_la_cabecera_OAuth()
    {
        var (_, email) = Services();

        await email.SendMessageAsync("ana@correo.test", "Asunto de prueba", "<p>hola</p>", "hola", null, default);

        var sent = Assert.Single(google.Emails);
        Assert.Equal("ana@correo.test", sent.To);
        Assert.Equal("Asunto de prueba", sent.Subject);
        Assert.Contains("<p>hola</p>", sent.Html);
        Assert.Equal("hola", sent.Text.Trim());
        var send = google.Requests.Last();
        Assert.Equal("Bearer fake-access-token", send.Headers["Authorization"]);
    }

    [Fact]
    public async Task El_ics_viaja_como_adjunto_cita_ics()
    {
        var (_, email) = Services();

        await email.SendMessageAsync("ana@correo.test", "Cita", "<p>x</p>", "x", "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n", default);

        Assert.Equal(["cita.ics"], google.Emails.Single().Attachments);
    }

    [Fact]
    public async Task El_token_de_acceso_se_reutiliza_entre_envios()
    {
        var (_, email) = Services();

        await email.SendMessageAsync("a@x.test", "1", "<p>1</p>", "1", null, default);
        await email.SendMessageAsync("b@x.test", "2", "<p>2</p>", "2", null, default);

        Assert.Equal(2, google.Emails.Count);
        Assert.Equal(1, google.TokenRequests);
    }

    [Fact]
    public async Task El_correo_del_chat_lleva_siempre_el_asunto_fijo()
    {
        var (_, email) = Services();
        var items = new List<EmailItem> { new("¿Q?", "R", null) };

        await email.SendAsync("ana@correo.test", items, default);

        Assert.Equal("Respuesta del asistente del taller", google.Emails.Single().Subject);
    }

    [Fact]
    public async Task Si_Gmail_responde_error_el_envio_lanza()
    {
        google.FailSends = true;
        var (_, email) = Services();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            email.SendMessageAsync("a@x.test", "s", "<p>x</p>", "x", null, default));
    }

    [Fact]
    public async Task Una_direccion_con_cabeceras_inyectadas_no_llega_a_enviarse()
    {
        var (_, email) = Services();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            email.SendMessageAsync("victima@x.test\r\nBcc: otro@x.test", "s", "<p>x</p>", "x", null, default));
        Assert.Empty(google.Emails);
    }
}
