using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Cada 15 minutos envía el recordatorio de las citas que empiezan en las próximas 24 horas.</summary>
public sealed class ReminderService(Db db, Notifier notifier, ILogger<ReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!notifier.Enabled) return; // sin SMTP no hay recordatorios

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try { await RunAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError("Fallo en el envío de recordatorios: {Type}", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var limit = now.AddHours(24);
        var f = Builders<Appointment>.Filter;
        var failed = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            // "Reclamar" la cita marcándola como avisada en la misma operación: si hay varias instancias de la API,
            // solo una la obtiene y no se duplican los correos.
            var claimed = await db.Appointments.FindOneAndUpdateAsync(
                f.In(a => a.Status, Statuses.Active) & f.Eq(a => a.ReminderSent, false) &
                f.Gt(a => a.Start, now) & f.Lte(a => a.Start, limit) & f.Nin(a => a.Id, failed),
                Builders<Appointment>.Update.Set(a => a.ReminderSent, true), cancellationToken: ct);
            if (claimed is null) break;

            var user = await db.Users.Find(u => u.Id == claimed.UserId).FirstOrDefaultAsync(ct);
            if (user is not { Active: true }) continue;

            // Solo un fallo real de envío se reintenta. Si el aviso se omite (cliente de ejemplo),
            // la cita se queda marcada para no volver a intentarlo en cada pasada.
            if (await notifier.SendAsync(user, claimed, EmailKind.Reminder, ct) == SendResult.Failed)
            {
                failed.Add(claimed.Id);
                await db.Appointments.UpdateOneAsync(a => a.Id == claimed.Id,
                    Builders<Appointment>.Update.Set(a => a.ReminderSent, false), cancellationToken: ct);
            }
        }
    }
}
