namespace TallerChat.Api.Tests.Unit;

/// <summary>Agrupación por mes, trimestre y año de las estadísticas (lógica pura, sin base de datos).</summary>
public class StatsBucketsTests
{
    private static readonly DateOnly Today = new(2026, 2, 17);

    [Theory]
    [InlineData(null, true, Granularity.Month)]
    [InlineData("month", true, Granularity.Month)]
    [InlineData("QUARTER", true, Granularity.Quarter)]
    [InlineData("trimestre", true, Granularity.Quarter)]
    [InlineData("year", true, Granularity.Year)]
    [InlineData("semana", false, Granularity.Month)]
    public void Reconoce_la_granularidad(string? value, bool ok, Granularity expected)
    {
        Assert.Equal(ok, StatsBuckets.TryParse(value, out var g));
        if (ok) Assert.Equal(expected, g);
    }

    [Fact]
    public void Los_meses_llegan_completos_y_ordenados_incluido_el_cambio_de_año()
    {
        var months = StatsBuckets.Last(Today, Granularity.Month, 4);

        Assert.Equal(["2025-11", "2025-12", "2026-01", "2026-02"], months.Select(p => p.Key));
        Assert.Equal("feb 2026", months[^1].Label);
    }

    [Fact]
    public void Los_trimestres_empiezan_en_enero_abril_julio_y_octubre()
    {
        var quarters = StatsBuckets.Last(Today, Granularity.Quarter, 3);

        Assert.Equal(["2025-T3", "2025-T4", "2026-T1"], quarters.Select(p => p.Key));
        Assert.Equal(new DateOnly(2026, 1, 1), quarters[^1].Start);
        Assert.Equal("T1 2026", quarters[^1].Label);
    }

    [Fact]
    public void Los_años_acaban_en_el_actual()
    {
        Assert.Equal(["2024", "2025", "2026"], StatsBuckets.Last(Today, Granularity.Year, 3).Select(p => p.Key));
    }

    [Theory]
    [InlineData("2026-03-31", "2026-T1")]
    [InlineData("2026-04-01", "2026-T2")]
    [InlineData("2026-12-31", "2026-T4")]
    public void Un_dia_cae_en_el_trimestre_que_le_corresponde(string day, string key) =>
        Assert.Equal(key, StatsBuckets.PeriodOf(DateOnly.Parse(day), Granularity.Quarter).Key);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1000, StatsBuckets.MaxPeriods)]
    public void El_numero_de_tramos_se_acota(int requested, int expected) =>
        Assert.Equal(expected, StatsBuckets.Last(Today, Granularity.Month, requested).Count);
}
