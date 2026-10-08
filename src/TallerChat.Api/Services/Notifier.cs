using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Resultado de un intento de envío. Distinguir "omitido" de "fallido" evita reintentos eternos.</summary>
public enum SendResult { Sent, Skipped, Failed }

/// <summary>Envía los correos de citas. Un fallo de SMTP nunca rompe la operación que lo origina.</summary>
public sealed class Notifier(EmailService mail, IOptions<SmtpOptions> smtp, BookingService booking,
    IOptions<ShopOptions> shop, IOptions<AuthOptions> auth, Db db, ILogger<Notifier> log)
{
    public bool Enabled => smtp.Value.Enabled;

    /// <summary>Los clientes de ejemplo no existen: a ese dominio nunca se le envía nada.</summary>
    public static bool Deliverable(string email) => !email.EndsWith("@demo.taller", StringComparison.OrdinalIgnoreCase);

    public async Task<SendResult> SendAsync(AppUser user, Appointment a, EmailKind kind, CancellationToken ct)
    {
        if (!Enabled || !Deliverable(user.Email)) return SendResult.Skipped;
        try
        {
            var m = AppointmentEmails.Render(kind, user, a, booking, shop.Value);
            await mail.SendMessageAsync(user.Email, m.Subject, m.Html, m.Text, m.Ics, ct);
            return SendResult.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Fallo al enviar el email de cita ({Kind}): {Type}", kind, ex.GetType().Name); // sin dirección ni mensaje
            return SendResult.Failed;
        }
    }

    /// <summary>Igual que <see cref="SendAsync"/>, para quien solo necesita saber si el cliente recibió el aviso.</summary>
    public async Task<bool> TrySendAsync(AppUser user, Appointment a, EmailKind kind, CancellationToken ct)
        => await SendAsync(user, a, kind, ct) == SendResult.Sent;

    /// <summary>Correo con el enlace para confirmar la cuenta recién creada.</summary>
    public Task<bool> TrySendVerifyAsync(AppUser user, string link, CancellationToken ct)
        => SendRenderedAsync(user, AccountEmails.RenderVerify(user, link, auth.Value.VerifyTokenHours, shop.Value), "verificación", ct);

    /// <summary>Correo con el enlace para elegir una contraseña nueva.</summary>
    public Task<bool> TrySendResetAsync(AppUser user, string link, CancellationToken ct)
        => SendRenderedAsync(user, AccountEmails.RenderReset(user, link, auth.Value.ResetTokenMinutes, shop.Value), "restablecimiento", ct);

    /// <summary>Aviso de que la cuenta se ha eliminado. Se manda cuando el borrado ya ha terminado.</summary>
    public Task<bool> TrySendDeletedAsync(AppUser user, DeletionSummary summary, bool byAdmin, CancellationToken ct)
        => SendRenderedAsync(user, AccountEmails.RenderDeleted(user, summary, byAdmin, shop.Value), "baja de cuenta", ct);

    private async Task<bool> SendRenderedAsync(AppUser user, RenderedEmail m, string what, CancellationToken ct)
    {
        if (!Enabled || !Deliverable(user.Email)) return false;
        try
        {
            await mail.SendMessageAsync(user.Email, m.Subject, m.Html, m.Text, m.Ics, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Fallo al enviar el email de {What}: {Type}", what, ex.GetType().Name); // sin dirección ni mensaje
            return false;
        }
    }

    /// <summary>
    /// A qué dirección debe llegar en realidad un aviso para la cuenta <paramref name="email"/>. Las cuentas de
    /// administración con el dominio de ejemplo del taller (Shop:PlaceholderDomain, como "@torque.es") no son
    /// buzones reales, así que el aviso se manda en su lugar al remitente configurado en Smtp:From.
    /// </summary>
    private string ResolveAdminAddress(string email) =>
        email.EndsWith("@" + shop.Value.PlaceholderDomain, StringComparison.OrdinalIgnoreCase) ? smtp.Value.From : email;

    /// <summary>
    /// Avisa a la administración de una nueva solicitud o de una cancelación, con un enlace directo a esa cita
    /// en la agenda. Va a todas las cuentas con rol de administración activas y con el correo confirmado; varias
    /// de ellas pueden compartir el mismo buzón real tras <see cref="ResolveAdminAddress"/>, así que no se repite.
    /// </summary>
    public async Task<SendResult> NotifyAdminsAsync(EmailKind kind, AppUser customer, Appointment a, CancellationToken ct)
    {
        if (!Enabled) return SendResult.Skipped;

        // Todo en el mismo try: un fallo aquí (Mongo al listar administradores, o al renderizar) es tan poco
        // motivo para tumbar la reserva o la cancelación que lo origina como un fallo de SMTP.
        try
        {
            // Solo "Active": no se exige el correo verificado porque una cuenta con el dominio de ejemplo
            // (p.ej. "@torque.es") nunca puede verificarse por sí misma -- su aviso se redirige igualmente
            // al remitente real en ResolveAdminAddress, así que exigir EmailVerified la dejaría sin avisos para siempre.
            var admins = await db.Users
                .Find(u => u.Role == Roles.Admin && u.Active)
                .Project(u => u.Email).ToListAsync(ct);
            var targets = admins.Select(ResolveAdminAddress).Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targets.Count == 0)
            {
                log.LogWarning("Aviso de administración ({Kind}) omitido: no hay ninguna cuenta de administración activa con correo.", kind);
                return SendResult.Skipped;
            }

            var m = AppointmentEmails.RenderAdmin(kind, customer, a, booking, shop.Value);
            var sentAny = false;
            foreach (var to in targets)
            {
                try
                {
                    await mail.SendMessageAsync(to, m.Subject, m.Html, m.Text, null, ct);
                    sentAny = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError("Fallo al enviar el aviso de administración ({Kind}): {Type}", kind, ex.GetType().Name); // sin dirección
                }
            }
            return sentAny ? SendResult.Sent : SendResult.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError("Fallo al preparar el aviso de administración ({Kind}): {Type}", kind, ex.GetType().Name);
            return SendResult.Failed;
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
