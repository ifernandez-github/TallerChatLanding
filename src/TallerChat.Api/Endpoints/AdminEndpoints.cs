using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TallerChat.Api;

public static class AdminEndpoints
{
    // estado actual -> estados a los que puede pasar
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Statuses.Pending] = [Statuses.Confirmed, Statuses.Cancelled],
        [Statuses.Confirmed] = [Statuses.Completed, Statuses.Cancelled]
    };

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("admin");
        g.MapGet("/summary", SummaryAsync);
        g.MapGet("/calendar", CalendarAsync);
        g.MapGet("/appointments", AppointmentsAsync);
        g.MapPost("/appointments/{id}/status", SetStatusAsync);
        g.MapGet("/users", UsersAsync);
        g.MapPut("/users/{id}", UpdateUserAsync);
        g.MapPost("/users/{id}/active", SetActiveAsync);
        g.MapGet("/users/{id}/deletion", DeletionPreviewAsync).RequireRateLimiting("account");
        // POST y no DELETE: el correo de confirmación viaja en el cuerpo, nunca en la URL.
        g.MapPost("/users/{id}/delete", DeleteUserAsync).RequireRateLimiting("account");
        g.MapPost("/users/{id}/vehicles", AddVehicleAsync);
        g.MapPut("/users/{id}/vehicles/{vehicleId}", UpdateVehicleAsync);
        g.MapDelete("/users/{id}/vehicles/{vehicleId}", DeleteVehicleAsync);
    }

    private static async Task<IResult> SummaryAsync(Db db, BookingService booking, CancellationToken ct)
    {
        var (from, to) = booking.DayRangeUtc(booking.TodayLocal());
        var f = Builders<Appointment>.Filter;
        var pending = await db.Appointments.CountDocumentsAsync(f.Eq(a => a.Status, Statuses.Pending) & f.Gt(a => a.Start, DateTime.UtcNow), cancellationToken: ct);
        var today = await db.Appointments.CountDocumentsAsync(f.Gte(a => a.Start, from) & f.Lt(a => a.Start, to) & f.In(a => a.Status, Statuses.Active), cancellationToken: ct);
        var users = await db.Users.CountDocumentsAsync(u => u.Role == Roles.Client, cancellationToken: ct);
        return Results.Ok(new SummaryDto((int)pending, (int)today, (int)users));
    }

    /// <summary>Ocupación de todo un mes (?month=AAAA-MM) para el calendario con código de colores.</summary>
    private static async Task<IResult> CalendarAsync(string? month, BookingService booking, CancellationToken ct)
    {
        var today = booking.TodayLocal();
        int year = today.Year, m = today.Month;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return ApiResults.Error(400, "Mes no válido (usa AAAA-MM).");
            year = parsed.Year; m = parsed.Month;
        }
        return Results.Ok(await booking.CalendarAsync(year, m, ct));
    }

    /// <summary>?date=AAAA-MM-DD → agenda del día; ?pending=true → todas las pendientes futuras.</summary>
    private static async Task<IResult> AppointmentsAsync(string? date, bool? pending, Db db, BookingService booking, CancellationToken ct)
    {
        var f = Builders<Appointment>.Filter;
        FilterDefinition<Appointment> filter;
        if (pending == true)
            filter = f.Eq(a => a.Status, Statuses.Pending) & f.Gt(a => a.Start, DateTime.UtcNow);
        else
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return ApiResults.Error(400, "Fecha no válida (usa AAAA-MM-DD).");
            var (from, to) = booking.DayRangeUtc(day);
            filter = f.Gte(a => a.Start, from) & f.Lt(a => a.Start, to);
        }

        var items = await db.Appointments.Find(filter).SortBy(a => a.Start).ThenBy(a => a.Bay).Limit(300).ToListAsync(ct);
        var ids = items.Select(a => a.UserId).Distinct().ToList();
        var users = (await db.Users.Find(Builders<AppUser>.Filter.In(u => u.Id, ids)).ToListAsync(ct)).ToDictionary(u => u.Id);

        return Results.Ok(items.Select(a =>
        {
            users.TryGetValue(a.UserId, out var u);
            return new AdminAppointmentDto(a.Id, a.UserId, u?.Name ?? "(usuario eliminado)", u?.Email ?? "", u?.Phone, a.ServiceId,
                Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId, a.Start, a.Bay, a.Plate, a.Vehicle, a.Notes, a.Status);
        }));
    }

    private static async Task<IResult> SetStatusAsync(string id, StatusRequest req, Db db, Notifier notifier, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Cita no encontrada.");
        var target = req.Status ?? "";
        var from = Transitions.Where(t => t.Value.Contains(target)).Select(t => t.Key).ToArray();
        if (from.Length == 0) return ApiResults.Error(400, "Estado no válido.");

        var f = Builders<Appointment>.Filter;
        var filter = f.Eq(a => a.Id, id) & f.In(a => a.Status, from);
        if (target == Statuses.Confirmed) filter &= f.Gt(a => a.Start, DateTime.UtcNow);

        var updated = await db.Appointments.FindOneAndUpdateAsync(
            filter,
            Builders<Appointment>.Update.Set(a => a.Status, target).Set(a => a.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Appointment> { ReturnDocument = ReturnDocument.After }, ct);
        if (updated is null) return ApiResults.Error(409, target == Statuses.Confirmed
            ? "No se puede confirmar: la cita ya ha pasado o no está pendiente."
            : "Ese cambio de estado no es posible para la cita actual.");

        var sent = false;
        if (target is Statuses.Confirmed or Statuses.Cancelled)
        {
            var user = await db.Users.Find(u => u.Id == updated.UserId).FirstOrDefaultAsync(ct);
            if (user is not null)
                sent = await notifier.TrySendAsync(user, updated, target == Statuses.Confirmed ? EmailKind.Confirmed : EmailKind.Cancelled, CancellationToken.None);
        }
        return Results.Ok(new BookResponse(AppointmentEndpoints.ToDto(updated), sent));
    }

    private static async Task<IResult> UsersAsync(Db db, CancellationToken ct)
    {
        var users = await db.Users.Find(_ => true).SortBy(u => u.Name).Limit(500).ToListAsync(ct);
        var counts = await db.Appointments.Aggregate()
            .Group(a => a.UserId, g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byUser = counts.ToDictionary(c => c.UserId, c => c.Count);
        return Results.Ok(users.Select(u => u.ToAdminDto(byUser.GetValueOrDefault(u.Id))));
    }

    /// <summary>Edita cualquier dato de la ficha de un cliente. Un administrador no puede quitarse a sí mismo el rol.</summary>
    private static async Task<IResult> UpdateUserAsync(string id, AdminUserRequest req, ClaimsPrincipal principal, Db db,
        AccountDeletion deletion, IMemoryCache cache, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Usuario no encontrado.");
        var role = req.Role;
        if (id == principal.UserId() && role is not null && role != Roles.Admin)
            return ApiResults.Error(400, "No puedes quitarte a ti mismo el rol de administrador.");

        // Degradar al último administrador utilizable dejaría el taller sin nadie que pueda entrar aquí.
        if (role is not null && role != Roles.Admin && await IsLastUsableAdminAsync(id, db, deletion, ct))
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de cambiarle el rol.");

        var result = await UserAdmin.UpdateProfileAsync(db, id, req.Name, req.Email, req.Phone, req.Address, role, ct);
        cache.Remove(AuthSetup.CacheKey(id)); // si ha cambiado el rol, la sesión debe revalidarse enseguida
        return result;
    }

    private static Task<IResult> AddVehicleAsync(string id, VehicleRequest req, Db db, CancellationToken ct)
        => UserAdmin.AddVehicleAsync(db, id, req, ct);

    private static Task<IResult> UpdateVehicleAsync(string id, string vehicleId, VehicleRequest req, Db db, CancellationToken ct)
        => UserAdmin.UpdateVehicleAsync(db, id, vehicleId, req, ct);

    private static Task<IResult> DeleteVehicleAsync(string id, string vehicleId, Db db, CancellationToken ct)
        => UserAdmin.DeleteVehicleAsync(db, id, vehicleId, ct);

    /// <summary>
    /// ¿Esa cuenta es el último administrador con el que se puede entrar? Se usa antes de borrarla,
    /// degradarla o desactivarla: las tres cosas dejarían la administración inaccesible.
    /// </summary>
    private static async Task<bool> IsLastUsableAdminAsync(string id, Db db, AccountDeletion deletion, CancellationToken ct)
    {
        var target = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return target?.Role == Roles.Admin && await deletion.IsLastAdminAsync(id, ct);
    }

    /// <summary>Lo que se perderá con esa cuenta, contado en el momento de abrir el aviso de confirmación.</summary>
    private static async Task<IResult> DeletionPreviewAsync(string id, Db db, AccountDeletion deletion, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Usuario no encontrado.");
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return user is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(await deletion.PreviewAsync(user, ct));
    }

    /// <summary>
    /// Borra una cuenta con todo lo suyo: vehículos, citas y enlaces de correo pendientes.
    /// Para evitar un clic en la fila equivocada, hay que teclear el correo exacto de esa cuenta.
    /// </summary>
    private static async Task<IResult> DeleteUserAsync(string id, AdminDeleteUserRequest req, ClaimsPrincipal principal,
        Db db, AccountDeletion deletion, Notifier notifier, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Usuario no encontrado.");
        if (id == principal.UserId())
            return ApiResults.Error(400, "No puedes borrar tu propia cuenta desde aquí. Hazlo desde tu área de cliente.");

        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (user is null) return ApiResults.Error(404, "Usuario no encontrado.");

        if (!Validation.TryEmail(req.ConfirmEmail, out var confirm) || confirm != user.Email)
            return ApiResults.Error(400, "Escribe el correo exacto de la cuenta para confirmar el borrado.");

        var result = await deletion.DeleteAsync(user, principal.UserId(), ct);
        if (result.Outcome == DeletionOutcome.LastAdmin)
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de borrarla.");

        // El cliente se entera de que su cuenta ya no está, aunque la baja no la haya pedido él.
        if (result.Outcome == DeletionOutcome.Deleted)
            await notifier.TrySendDeletedAsync(user, result.Summary, true, CancellationToken.None);
        return Results.Ok(result.Summary);
    }

    private static async Task<IResult> SetActiveAsync(string id, ActiveRequest req, ClaimsPrincipal principal, Db db,
        AccountDeletion deletion, IMemoryCache cache, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Usuario no encontrado.");
        if (req.Active is not { } active) return ApiResults.Error(400, "Indica si la cuenta debe estar activa.");
        if (id == principal.UserId()) return ApiResults.Error(400, "No puedes desactivar tu propia cuenta.");

        // Una cuenta desactivada no puede iniciar sesión: desactivar al último administrador equivale a perderlo.
        if (!active && await IsLastUsableAdminAsync(id, db, deletion, ct))
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de desactivarla.");

        var result = await db.Users.UpdateOneAsync(u => u.Id == id, Builders<AppUser>.Update.Set(u => u.Active, active), cancellationToken: ct);
        if (result.MatchedCount == 0) return ApiResults.Error(404, "Usuario no encontrado.");
        cache.Remove(AuthSetup.CacheKey(id)); // la sesión de la persona desactivada deja de valer enseguida
        return Results.Ok(new { id, active });
    }
}
