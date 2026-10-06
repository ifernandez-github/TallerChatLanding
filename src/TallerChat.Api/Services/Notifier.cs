using Microsoft.Extensions.Options;

namespace TallerChat.Api;

/// <summary>Envía los correos de citas. Un fallo de SMTP nunca rompe la operación que lo origina.</summary>
public sealed class Notifier(EmailService mail, IOptions<SmtpOptions> smtp, BookingService booking,
    IOptions<ShopOptions> shop, ILogger<Notifier> log)
{
    public bool Enabled => smtp.Value.Enabled;

    // Los clientes de ejemplo no existen: no se les envía nada.
    private static bool Deliverable(string email) => !email.EndsWith("@demo.taller", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> TrySendAsync(AppUser user, Appointment a, EmailKind kind, CancellationToken ct)
    {
        if (!Enabled || !Deliverable(user.Email)) return false;
        try
        {
            var m = AppointmentEmails.Render(kind, user, a, booking, shop.Value);
            await mail.SendMessageAsync(user.Email, m.Subject, m.Html, m.Text, m.Ics, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Fallo al enviar el email de cita ({Kind}): {Type}", kind, ex.GetType().Name); // sin dirección ni mensaje
            return false;
        }
    }

    public async Task<bool> TrySendSummaryAsync(AppUser user, IReadOnlyList<Appointment> upcoming, CancellationToken ct)
    {
        if (!Enabled || !Deliverable(user.Email)) return false;
        try
        {
            var m = AppointmentEmails.RenderSummary(user, upcoming, booking, shop.Value);
            await mail.SendMessageAsync(user.Email, m.Subject, m.Html, m.Text, null, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Fallo al enviar el resumen de citas: {Type}", ex.GetType().Name);
            return false;
        }
    }
}
