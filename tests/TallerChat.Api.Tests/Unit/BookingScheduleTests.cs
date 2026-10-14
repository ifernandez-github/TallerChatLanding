using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Unit;

/// <summary>Horario, franjas y fechas del taller (reglas puras de <see cref="BookingService"/>: no tocan MongoDB).</summary>
public class BookingScheduleTests
{
    private readonly BookingService booking = TestDb.Booking();

    [Fact]
    public void Domingo_cerrado_y_sabado_con_horario_propio()
    {
        Assert.Null(booking.Hours(DayOfWeek.Sunday));
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(13, 30)), booking.Hours(DayOfWeek.Saturday));
        Assert.Equal((new TimeOnly(8, 30), new TimeOnly(18, 30)), booking.Hours(DayOfWeek.Wednesday));
    }

    [Fact]
    public void Un_dia_laborable_tiene_10_franjas_de_una_hora()
    {
        var slots = booking.SlotsUtc(new DateOnly(2026, 3, 4)).ToList(); // miércoles, horario de invierno (UTC+1)
        Assert.Equal(10, slots.Count);
        Assert.Equal(new DateTime(2026, 3, 4, 7, 30, 0, DateTimeKind.Utc), slots[0]);   // 08:30 locales
        Assert.Equal(new DateTime(2026, 3, 4, 16, 30, 0, DateTimeKind.Utc), slots[^1]); // 17:30 locales: acaba a las 18:30
    }

    [Fact]
    public void El_sabado_la_ultima_franja_no_se_pasa_del_cierre()
    {
        var slots = booking.SlotsUtc(new DateOnly(2026, 3, 7)).ToList(); // sábado 9:00-13:30
        Assert.Equal(4, slots.Count); // 9, 10, 11, 12: la de las 13:00 acabaría a las 14:00
        Assert.Equal("12:00", booking.TimeLabel(slots[^1]));
    }

    [Fact]
    public void El_domingo_no_hay_franjas() => Assert.Empty(booking.SlotsUtc(new DateOnly(2026, 3, 8)));

    [Fact]
    public void En_verano_la_hora_UTC_va_dos_horas_por_detras()
    {
        var first = booking.SlotsUtc(new DateOnly(2026, 7, 1)).First(); // miércoles, UTC+2
        Assert.Equal(new DateTime(2026, 7, 1, 6, 30, 0, DateTimeKind.Utc), first);
        Assert.Equal("08:30", booking.TimeLabel(first));
    }

    [Fact]
    public void La_duracion_de_franja_es_configurable()
    {
        var b = TestDb.Booking(o => o.SlotMinutes = 30);
        Assert.Equal(20, b.SlotsUtc(new DateOnly(2026, 3, 4)).Count());
    }

    [Fact]
    public void LocalDateIso_usa_el_dia_local_y_no_el_UTC()
    {
        // 22:30 UTC del 1 de julio = 00:30 del 2 de julio en Madrid (verano). El enlace de la agenda debe abrir el día 2.
        Assert.Equal("2026-07-02", booking.LocalDateIso(new DateTime(2026, 7, 1, 22, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-01-15", booking.LocalDateIso(new DateTime(2026, 1, 15, 22, 30, 0, DateTimeKind.Utc))); // invierno: 23:30 locales
    }

    [Fact]
    public void Describe_da_la_fecha_en_espanol_en_hora_local()
    {
        var text = booking.Describe(new DateTime(2026, 7, 1, 6, 30, 0, DateTimeKind.Utc));
        Assert.Equal("miércoles 1 de julio de 2026, 08:30", text);
    }

    [Fact]
    public void AsUtc_trata_Unspecified_como_UTC_y_convierte_Local()
    {
        var unspecified = new DateTime(2026, 7, 1, 6, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal(DateTimeKind.Utc, BookingService.AsUtc(unspecified).Kind);
        Assert.Equal(unspecified.Ticks, BookingService.AsUtc(unspecified).Ticks);
    }

    [Fact]
    public void DayRangeUtc_cubre_el_dia_local_completo()
    {
        var (from, to) = booking.DayRangeUtc(new DateOnly(2026, 7, 1));
        Assert.Equal(new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc), to);
    }

    [Fact]
    public void SlotAt_salta_al_siguiente_dia_laborable()
    {
        var slot = booking.SlotAt(new DateOnly(2026, 3, 8), 0); // domingo -> lunes 9
        Assert.Equal("2026-03-09", booking.LocalDateIso(slot));
        Assert.Equal("08:30", booking.TimeLabel(slot));
    }
}
