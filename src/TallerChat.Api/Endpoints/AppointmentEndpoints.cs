using System.Globalization;
using System.Security.Claims;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TallerChat.Api;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/appointments");

        // La disponibilidad es pública: sirve para ver huecos antes de crear la cuenta.
        g.MapGet("/availability", AvailabilityAsync);
        g.MapGet("/services", () => Results.Ok(Catalog.Services));

        var mine = g.MapGroup("").RequireAuthorization();
        mine.MapGet("/mine", MineAsync);
        mine.MapPost("", BookAsync).RequireRateLimiting("booking");
        mine.MapPost("/{id}/cancel", CancelAsync);
        mine.MapPost("/mine/email", EmailMineAsync).RequireRateLimiting("mailme");
    }

    public static AppointmentDto ToDto(Appointment a) => new(a.Id, a.ServiceId, Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId,
        a.Start, a.Plate, a.Vehicle, a.Notes, a.Status,
        a.Status is Statuses.Pending or Statuses.Confirmed && a.Start > DateTime.UtcNow);

    private static async Task<IResult> AvailabilityAsync(string? date, BookingService booking, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return ApiResults.Error(400, "Fecha no válida (usa AAAA-MM-DD).");
        var result = await booking.AvailabilityAsync(day, ct);
        return result is null ? ApiResults.Error(400, "Esa fecha no está disponible para reservar.") : Results.Ok(result);
    }

    private static async Task<IResult> MineAsync(ClaimsPrincipal principal, Db db, CancellationToken ct)
    {
        var uid = principal.UserId();
        var list = await db.Appointments.Find(a => a.UserId == uid).SortByDescending(a => a.Start).Limit(200).ToListAsync(ct);
        return Results.Ok(list.Select(ToDto));
    }

    private static async Task<IResult> BookAsync(BookRequest req, ClaimsPrincipal principal, Db db, BookingService booking,
        Notifier notifier, CancellationToken ct)
    {
        if (Catalog.Find(req.ServiceId) is not { } service) return ApiResults.Error(400, "Elige un servicio de la lista.");
        if (req.Start is not { } start) return ApiResults.Error(400, "Elige día y hora.");
        if (!Validation.TryPlate(req.Plate, out var plate)) return ApiResults.Error(400, "La matrícula no es válida.");
        var vehicle = (req.Vehicle ?? "").Trim();
        if (vehicle.Length is < 2 or > 60) return ApiResults.Error(400, "Indica marca y modelo del vehículo (2 a 60 caracteres).");
        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 500 }) return ApiResults.Error(400, "Las notas no pueden superar los 500 caracteres.");

        var uid = principal.UserId();
        var user = await db.Users.Find(u => u.Id == uid).FirstOrDefaultAsync(ct);
        if (user is not { Active: true }) return ApiResults.Error(401, "Sesión no válida.");

        var result = await booking.BookAsync(user, service.Id, start, plate, vehicle, notes, ct);
        if (result.Appointment is null) return ApiResults.Error(result.Status, result.Error ?? "No se pudo reservar la cita.");

        // Con CancellationToken.None: si el cliente cierra la pestaña, el correo de confirmación sale igualmente.
        var sent = await notifier.TrySendAsync(user, result.Appointment, EmailKind.Requested, CancellationToken.None);
        return Results.Json(new BookResponse(ToDto(result.Appointment), sent), statusCode: 201);
    }

    private static async Task<IResult> CancelAsync(string id, ClaimsPrincipal principal, Db db, Notifier notifier, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Cita no encontrada.");
        var uid = principal.UserId();
        var f = Builders<Appointment>.Filter;
        var updated = await db.Appointments.FindOneAndUpdateAsync(
            f.Eq(a => a.Id, id) & f.Eq(a => a.UserId, uid) & f.In(a => a.Status, Statuses.Active) & f.Gt(a => a.Start, DateTime.UtcNow),
            Builders<Appointment>.Update.Set(a => a.Status, Statuses.Cancelled).Set(a => a.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Appointment> { ReturnDocument = ReturnDocument.After }, ct);
        if (updated is null) return ApiResults.Error(409, "Esta cita ya no se puede cancelar.");

        var user = await db.Users.Find(u => u.Id == uid).FirstOrDefaultAsync(ct);
        var sent = user is not null && await notifier.TrySendAsync(user, updated, EmailKind.Cancelled, CancellationToken.None);
        return Results.Ok(new BookResponse(ToDto(updated), sent));
    }

    /// <summary>Envía las próximas citas SOLO al correo de la cuenta (nunca a una dirección elegida por el cliente).</summary>
    private static async Task<IResult> EmailMineAsync(ClaimsPrincipal principal, Db db, Notifier notifier, CancellationToken ct)
    {
        if (!notifier.Enabled) return ApiResults.Error(503, "El envío por email no está configurado.");
        var uid = principal.UserId();
        var user = await db.Users.Find(u => u.Id == uid).FirstOrDefaultAsync(ct);
        if (user is not { Active: true }) return ApiResults.Error(401, "Sesión no válida.");

        var f = Builders<Appointment>.Filter;
        var upcoming = await db.Appointments
            .Find(f.Eq(a => a.UserId, uid) & f.In(a => a.Status, Statuses.Active) & f.Gt(a => a.Start, DateTime.UtcNow))
            .SortBy(a => a.Start).Limit(20).ToListAsync(ct);
        if (upcoming.Count == 0) return ApiResults.Error(400, "No tienes citas próximas que enviar.");

        return await notifier.TrySendSummaryAsync(user, upcoming, CancellationToken.None)
            ? Results.Ok(new { sent = true })
            : ApiResults.Error(502, "No se pudo enviar el correo ahora mismo. Inténtalo más tarde.");
    }
}
