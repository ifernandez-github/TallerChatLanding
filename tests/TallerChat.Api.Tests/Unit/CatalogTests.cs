using System.Text.RegularExpressions;

namespace TallerChat.Api.Tests.Unit;

public class CatalogTests
{
    [Fact]
    public void Los_ids_son_unicos_y_no_vacios()
    {
        Assert.NotEmpty(Catalog.Services);
        Assert.Equal(Catalog.Services.Count, Catalog.Services.Select(s => s.Id).Distinct().Count());
        Assert.All(Catalog.Services, s => { Assert.False(string.IsNullOrWhiteSpace(s.Id)); Assert.False(string.IsNullOrWhiteSpace(s.Name)); });
    }

    [Fact]
    public void Find_devuelve_el_servicio_o_null()
    {
        Assert.Equal("Frenos", Catalog.Find("brake")?.Name);
        Assert.Null(Catalog.Find("inexistente"));
        Assert.Null(Catalog.Find(null));
    }

    /// <summary>
    /// CLAUDE.md exige que los ids del catálogo coincidan con el campo <c>icon</c> de frontend/src/content/site.ts.
    /// Este test lo comprueba; si no encuentra el frontend (p. ej. solo se copió la carpeta de la API), se da por bueno.
    /// </summary>
    [Fact]
    public void Los_ids_coinciden_con_los_servicios_del_frontend()
    {
        var site = FindUp("frontend/src/content/site.ts");
        if (site is null) return;

        var icons = Regex.Matches(File.ReadAllText(site), @"icon:\s*'([a-z0-9-]+)'").Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var s in Catalog.Services)
            Assert.True(icons.Contains(s.Id), $"El servicio «{s.Id}» del catálogo no está en site.ts.");
    }

    private static string? FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
