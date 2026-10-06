namespace TallerChat.Api;

public sealed record ServiceInfo(string Id, string Name);

/// <summary>
/// Servicios que se pueden reservar. Los identificadores y nombres deben coincidir con
/// <c>services</c> de frontend/src/content/site.ts (el id es el campo <c>icon</c>).
/// </summary>
public static class Catalog
{
    public static readonly IReadOnlyList<ServiceInfo> Services =
    [
        new("oil", "Mantenimiento y aceite"),
        new("brake", "Frenos"),
        new("scan", "Diagnosis electrónica"),
        new("belt", "Distribución y embrague"),
        new("wheel", "Neumáticos y alineado"),
        new("snow", "Aire acondicionado"),
        new("bolt", "Eléctricos e híbridos"),
        new("check-list", "Pre-ITV y revisión")
    ];

    public static ServiceInfo? Find(string? id) => Services.FirstOrDefault(s => s.Id == id);
}
