using System.Globalization;

namespace TallerChat.Api;

public enum Granularity { Month, Quarter, Year }

/// <summary>Un tramo del eje de tiempo (mes, trimestre o año locales) con su clave estable y su etiqueta.</summary>
public sealed record Period(string Key, string Label, DateOnly Start);

/// <summary>
/// Agrupación por mes, trimestre o año de las estadísticas. Es lógica pura (sin base de datos): siempre devuelve los
/// tramos completos, también los vacíos, para que las gráficas no «salten» los meses sin actividad.
/// </summary>
public static class StatsBuckets
{
    private static readonly string[] Meses = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public const int MaxPeriods = 60;

    public static bool TryParse(string? value, out Granularity g)
    {
        g = Granularity.Month;
        switch ((value ?? "month").ToLowerInvariant())
        {
            case "month" or "mes": g = Granularity.Month; return true;
            case "quarter" or "trimestre": g = Granularity.Quarter; return true;
            case "year" or "año" or "ano": g = Granularity.Year; return true;
            default: return false;
        }
    }

    public static string Name(Granularity g) => g switch { Granularity.Quarter => "quarter", Granularity.Year => "year", _ => "month" };

    public static int DefaultPeriods(Granularity g) => g switch { Granularity.Quarter => 8, Granularity.Year => 5, _ => 12 };

    public static DateOnly Floor(DateOnly d, Granularity g) => g switch
    {
        Granularity.Year => new DateOnly(d.Year, 1, 1),
        Granularity.Quarter => new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1),
        _ => new DateOnly(d.Year, d.Month, 1)
    };

    public static Period PeriodOf(DateOnly d, Granularity g)
    {
        var start = Floor(d, g);
        var quarter = (start.Month - 1) / 3 + 1;
        return g switch
        {
            Granularity.Year => new Period(start.Year.ToString(CultureInfo.InvariantCulture), start.Year.ToString(CultureInfo.InvariantCulture), start),
            Granularity.Quarter => new Period($"{start.Year}-T{quarter}", $"T{quarter} {start.Year}", start),
            _ => new Period($"{start.Year}-{start.Month:00}", $"{Meses[start.Month - 1]} {start.Year}", start)
        };
    }

    /// <summary>Los últimos <paramref name="count"/> tramos hasta el que contiene a <paramref name="today"/>, del más antiguo al más reciente.</summary>
    public static List<Period> Last(DateOnly today, Granularity g, int count)
    {
        count = Math.Clamp(count, 1, MaxPeriods);
        var current = Floor(today, g);
        var list = new List<Period>(count);
        for (var i = count - 1; i >= 0; i--)
        {
            var start = g switch
            {
                Granularity.Year => current.AddYears(-i),
                Granularity.Quarter => current.AddMonths(-3 * i),
                _ => current.AddMonths(-i)
            };
            list.Add(PeriodOf(start, g));
        }
        return list;
    }
}
