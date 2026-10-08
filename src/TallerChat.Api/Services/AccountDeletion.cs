using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Cómo ha terminado una baja.</summary>
public enum DeletionOutcome
{
    /// <summary>La cuenta y todo lo suyo ya no existen.</summary>
    Deleted,
    /// <summary>No se ha tocado nada: era la única cuenta de administración con la que se puede entrar.</summary>
    LastAdmin,
    /// <summary>Otra petición se adelantó: la cuenta ya no estaba.</summary>
    Gone
}

public sealed record DeletionResult(DeletionOutcome Outcome, DeletionSummary Summary);

/// <summary>
/// Baja definitiva de una cuenta: el usuario (con sus vehículos, que viven dentro del propio documento),
/// sus citas y los enlaces de correo pendientes. No hay papelera: lo que se borra aquí no se recupera.
/// </summary>
public sealed class AccountDeletion(Db db, IMemoryCache cache, ILogger<AccountDeletion> log)
{
    /// <summary>
    /// Las bajas se atienden de una en una. Comprobar "¿queda otro administrador?" y borrar tienen que ir
    /// juntos: si no, dos administradores que se den de baja a la vez pasan los dos el control y el taller
    /// se queda sin nadie que pueda entrar.
    /// </summary>
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Qué hay en la cuenta ahora mismo, para poder avisar de lo que se va a perder antes de borrarla.</summary>
    public async Task<DeletionSummary> PreviewAsync(AppUser user, CancellationToken ct)
    {
        var f = Builders<Appointment>.Filter;
        var total = await db.Appointments.CountDocumentsAsync(f.Eq(a => a.UserId, user.Id), cancellationToken: ct);
        var upcoming = await db.Appointments.CountDocumentsAsync(
            f.Eq(a => a.UserId, user.Id) & f.In(a => a.Status, Statuses.Active) & f.Gt(a => a.Start, DateTime.UtcNow),
            cancellationToken: ct);
        return new DeletionSummary(user.Vehicles.Count, (int)total, (int)upcoming);
    }

    /// <summary>
    /// Borra todo lo de la cuenta. Sin transacción: el orden es lo que limita el daño si algo falla a mitad.
    /// Primero las citas (si falla lo siguiente, los huecos ya están libres y la cuenta sigue ahí para
    /// reintentarlo) y el documento del usuario en último lugar.
    /// </summary>
    public async Task<DeletionResult> DeleteAsync(AppUser user, string actorId, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (user.Role == Roles.Admin && await IsLastAdminAsync(user.Id, ct))
                return new DeletionResult(DeletionOutcome.LastAdmin, new DeletionSummary(0, 0, 0));

            var summary = await PreviewAsync(user, ct);

            var appointments = await db.Appointments.DeleteManyAsync(a => a.UserId == user.Id, ct);
            await db.Tokens.DeleteManyAsync(t => t.UserId == user.Id, ct);
            var users = await db.Users.DeleteOneAsync(u => u.Id == user.Id, ct);

            // La sesión que hubiera abierta (aquí o en otro dispositivo) deja de valer en la siguiente petición.
            // Se escribe un estado muerto en vez de borrar la entrada: así una petición que estuviera leyendo el
            // usuario justo antes del borrado no puede dejar cacheado un estado activo durante un minuto.
            cache.Set(AuthSetup.CacheKey(user.Id), new UserState(false, "", ""), TimeSpan.FromMinutes(1));

            // Un borrado sin papelera tiene que dejar rastro. Solo identificadores: ni nombre, ni correo, ni dirección.
            if (users.DeletedCount == 0)
            {
                log.LogWarning("Baja de {UserId} pedida por {ActorId}: la cuenta ya no existía", user.Id, actorId);
                return new DeletionResult(DeletionOutcome.Gone, summary);
            }

            log.LogWarning("Cuenta {UserId} eliminada por {ActorId}: {Appointments} citas, {Vehicles} vehículos",
                user.Id, actorId, appointments.DeletedCount, summary.Vehicles);
            return new DeletionResult(DeletionOutcome.Deleted, summary);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// ¿Es la última cuenta de administración con la que se puede entrar? Se exige activa y con el correo
    /// confirmado, porque una cuenta desactivada o sin confirmar no supera el inicio de sesión: contarla
    /// dejaría el taller sin administración sin que nadie se diera cuenta.
    /// </summary>
    public async Task<bool> IsLastAdminAsync(string userId, CancellationToken ct)
    {
        var f = Builders<AppUser>.Filter;
        var others = await db.Users.CountDocumentsAsync(
            f.Eq(u => u.Role, Roles.Admin) & f.Ne(u => u.Id, userId) &
            f.Eq(u => u.Active, true) & f.Eq(u => u.EmailVerified, true), cancellationToken: ct);
        return others == 0;
    }
}
