using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>
/// Estadísticas del panel de gestión (solo lectura). Administrador y gestor ven las mismas cifras: son datos
/// agregados, sin nombres ni correos, así que no hay nada que el gestor no deba ver.
/// </summary>
public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/stats").RequireAuthorization("staff");
        g.MapGet("/clients", ClientsAsync);
        g.MapGet("/appointments", AppointmentsAsync);
    }

    private static bool TryRange(string? granularity, int? periods, BookingService booking, out Granularity g, out List<Period> list)
    {
        list = [];
        if (!StatsBuckets.TryParse(granularity, out g)) return false;
        if (periods is < 1 or > StatsBuckets.MaxPeriods) return false;
        list = StatsBuckets.Last(booking.TodayLocal(), g, periods ?? StatsBuckets.DefaultPeriods(g));
        return true;
    }

    /// <summary>Clientes nuevos (cuentas con rol cliente) por mes, trimestre o año de alta.</summary>
    private static async Task<IResult> ClientsAsync(string? granularity, int? periods, Db db, BookingService booking, CancellationToken ct)
    {
        if (!TryRange(granularity, periods, booking, out var g, out var list))
            return ApiResults.Error(400, "Parámetros no válidos (granularity: month, quarter o year; periods: 1 a 60).");

        var fromUtc = booking.DayRangeUtc(list[0].Start).FromUtc;
        var dates = await db.Users.Find(u => u.Role == Roles.Client && u.CreatedAt >= fromUtc)
            .Project(u => u.CreatedAt).ToListAsync(ct);

        var counts = list.ToDictionary(p => p.Key, _ => 0);
        foreach (var d in dates)
        {
            var key = StatsBuckets.PeriodOf(DateOnly.FromDateTime(booking.ToLocal(d)), g).Key;
            if (counts.ContainsKey(key)) counts[key]++;
        }
        var points = list.Select(p => new ClientsPointDto(p.Key, p.Label, counts[p.Key])).ToList();
        return Results.Ok(new ClientsStatsDto(StatsBuckets.Name(g), points.Sum(p => p.Count), points));
    }

    /// <summary>
    /// Citas agrupadas por el periodo en que se CREARON, con su estado actual (pendiente, confirmada, cerrada = completada
    /// o cancelada). «Creadas» es el total del periodo. Filtro opcional por categoría de servicio (?category=brake…).
    /// </summary>
    private static async Task<IResult> AppointmentsAsync(string? granularity, int? periods, string? category, Db db, BookingService booking, CancellationToken ct)
    {
        if (!TryRange(granularity, periods, booking, out var g, out var list))
            return ApiResults.Error(400, "Parámetros no válidos (granularity: month, quarter o year; periods: 1 a 60).");
        if (!string.IsNullOrWhiteSpace(category) && Catalog.Find(category) is null)
            return ApiResults.Error(400, "Categoría no válida.");

        var fromUtc = booking.DayRangeUtc(list[0].Start).FromUtc;
        var f = Builders<Appointment>.Filter;
        var filter = f.Gte(a => a.CreatedAt, fromUtc);
        if (!string.IsNullOrWhiteSpace(category)) filter &= f.Eq(a => a.ServiceId, category);
        var rows = await db.Appointments.Find(filter).Project(a => new { a.CreatedAt, a.Status }).ToListAsync(ct);

        var acc = list.ToDictionary(p => p.Key, _ => new int[4]); // pendientes, confirmadas, cerradas, canceladas
        foreach (var r in rows)
        {
            var key = StatsBuckets.PeriodOf(DateOnly.FromDateTime(booking.ToLocal(r.CreatedAt)), g).Key;
            if (!acc.TryGetValue(key, out var c)) continue;
            switch (r.Status)
            {
                case Statuses.Pending: c[0]++; break;
                case Statuses.Confirmed: c[1]++; break;
                case Statuses.Completed: c[2]++; break;
                case Statuses.Cancelled: c[3]++; break;
            }
        }
        var points = list.Select(p =>
        {
            var c = acc[p.Key];
            return new AppointmentsPointDto(p.Key, p.Label, c.Sum(), c[0], c[1], c[2], c[3]);
        }).ToList();
        var totals = new AppointmentsTotalsDto(points.Sum(p => p.Created), points.Sum(p => p.Pending),
            points.Sum(p => p.Confirmed), points.Sum(p => p.Closed), points.Sum(p => p.Cancelled));
        var categories = Catalog.Services.Select(s => new CategoryDto(s.Id, s.Name)).ToList();
        return Results.Ok(new AppointmentsStatsDto(StatsBuckets.Name(g),
            string.IsNullOrWhiteSpace(category) ? null : category, categories, totals, points));
    }
}
