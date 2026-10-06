using System.Globalization;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

public sealed record BookResult(Appointment? Appointment, int Status, string? Error);

/// <summary>Franjas, disponibilidad y reservas. Todo se guarda en UTC; las reglas de horario usan la zona del taller.</summary>
public sealed class BookingService(Db db, IOptions<BookingOptions> options)
{
    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];
    private static readonly string[] Meses =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    private readonly BookingOptions o = options.Value;
    private readonly TimeZoneInfo tz = FindZone(options.Value.TimeZone);

    public int SlotMinutes => o.SlotMinutes;

    private static TimeZoneInfo FindZone(string id)
    {
        foreach (var candidate in new[] { id, "Europe/Madrid", "Romance Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateTime AsUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private static TimeOnly T(string s) => TimeOnly.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Horario de apertura del día (null = cerrado). Los festivos no se contemplan.</summary>
    public (TimeOnly Open, TimeOnly Close)? Hours(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => null,
        DayOfWeek.Saturday => (T(o.SaturdayOpen), T(o.SaturdayClose)),
        _ => (T(o.WeekdayOpen), T(o.WeekdayClose))
    };

    public DateOnly TodayLocal() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

    public (DateTime FromUtc, DateTime ToUtc) DayRangeUtc(DateOnly day) => (
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), tz),
        TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), tz));

    /// <summary>Inicios de franja (UTC) de un día local.</summary>
    public IEnumerable<DateTime> SlotsUtc(DateOnly day)
    {
        if (Hours(day.DayOfWeek) is not { } h) yield break;
        var step = TimeSpan.FromMinutes(o.SlotMinutes);
        for (var t = h.Open.ToTimeSpan(); t + step <= h.Close.ToTimeSpan(); t += step)
        {
            var local = day.ToDateTime(TimeOnly.MinValue).Add(t);
            if (tz.IsInvalidTime(local)) continue;
            yield return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
    }

    /// <summary>Franja nº <paramref name="index"/> del primer día laborable desde <paramref name="day"/> (datos de ejemplo).</summary>
    public DateTime SlotAt(DateOnly day, int index)
    {
        while (Hours(day.DayOfWeek) is null) day = day.AddDays(1);
        var slots = SlotsUtc(day).ToList();
        return slots[Math.Min(index, slots.Count - 1)];
    }

    public string Describe(DateTime utc)
    {
        var l = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), tz);
        return $"{Dias[(int)l.DayOfWeek]} {l.Day} de {Meses[l.Month - 1]} de {l.Year}, {l.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Disponibilidad de un día, o null si la fecha está fuera del rango reservable.</summary>
    public async Task<AvailabilityDto?> AvailabilityAsync(DateOnly day, CancellationToken ct)
    {
        var today = TodayLocal();
        if (day < today || day > today.AddDays(o.MaxDaysAhead)) return null;

        var slots = SlotsUtc(day).ToList();
        var (from, to) = DayRangeUtc(day);
        var f = Builders<Appointment>.Filter;
        var taken = await db.Appointments
            .Find(f.Gte(a => a.Start, from) & f.Lt(a => a.Start, to) & f.In(a => a.Status, Statuses.Active))
            .Project(a => a.Start).ToListAsync(ct);
        var counts = taken.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

        var earliest = DateTime.UtcNow.AddHours(o.MinHoursAhead);
        var list = slots.Select(s =>
        {
            var free = Math.Max(0, o.Bays - counts.GetValueOrDefault(s));
            var label = TimeZoneInfo.ConvertTimeFromUtc(s, tz).ToString("HH:mm", CultureInfo.InvariantCulture);
            return new SlotDto(s, label, free, free > 0 && s >= earliest);
        }).ToList();
        return new AvailabilityDto(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), list.Count == 0, list);
    }

    public async Task<BookResult> BookAsync(AppUser user, string serviceId, DateTime start, string plate, string vehicle,
        string? notes, CancellationToken ct)
    {
        var startUtc = AsUtc(start);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz));
        var now = DateTime.UtcNow;

        if (!SlotsUtc(day).Contains(startUtc))
            return new(null, 400, "Esa hora no es una franja de cita válida.");
        if (startUtc < now.AddHours(o.MinHoursAhead))
            return new(null, 400, $"Las citas se reservan con al menos {o.MinHoursAhead} horas de antelación.");
        if (day > TodayLocal().AddDays(o.MaxDaysAhead))
            return new(null, 400, $"Solo se puede reservar con un máximo de {o.MaxDaysAhead} días de antelación.");

        var f = Builders<Appointment>.Filter;
        var mine = f.Eq(a => a.UserId, user.Id) & f.In(a => a.Status, Statuses.Active);
        if (await db.Appointments.CountDocumentsAsync(mine & f.Eq(a => a.Start, startUtc), cancellationToken: ct) > 0)
            return new(null, 409, "Ya tienes una cita a esa hora.");
        if (await db.Appointments.CountDocumentsAsync(mine & f.Gt(a => a.Start, now), cancellationToken: ct) >= o.MaxActivePerUser)
            return new(null, 409, $"Ya tienes {o.MaxActivePerUser} citas activas. Cancela alguna o espera a que se completen.");

        // El índice único (Start, Bay) garantiza que no haya dos citas en el mismo elevador y hueco.
        for (var bay = 1; bay <= o.Bays; bay++)
        {
            var a = new Appointment
            {
                UserId = user.Id, ServiceId = serviceId, Start = startUtc, Bay = bay,
                Plate = plate, Vehicle = vehicle, Notes = notes, Status = Statuses.Pending
            };
            try
            {
                await db.Appointments.InsertOneAsync(a, cancellationToken: ct);
                return new(a, 201, null);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
        }
        return new(null, 409, "Ese hueco acaba de ocuparse. Elige otra hora.");
    }
}
