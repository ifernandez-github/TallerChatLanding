using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TallerChat.Api;

public sealed record Section(string Label, string Text);
public sealed record ChatTurn(string Role, string Text);
public sealed record ChatRequest(string? Message, List<ChatTurn>? History);
public sealed record SourceRef(int Ref, string ChunkId, string Title, string Category, string SubCategory,
    List<string> Tags, double Score, List<Section> Sections);
public sealed record EmailItem(string? Question, string? Answer, List<SourceRef>? Sources);
public sealed record EmailRequest(string? To, List<EmailItem>? Items);
public sealed record ChatResponse(string Answer, List<SourceRef> Sources, bool GroundedInDatabase);

public sealed class Especificaciones
{
    [BsonElement("cadena_distribucion")] public string? CadenaDistribucion { get; set; }
    [BsonElement("correa_distribucion")] public string? CorreaDistribucion { get; set; }
}

/// <summary>Documento de taller_db.conocimiento (ver schema-taller_db-conocimiento).</summary>
[BsonIgnoreExtraElements]
public sealed class KnowledgeChunk
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("chunk_id")] public string ChunkId { get; set; } = "";
    [BsonElement("category")] public string Category { get; set; } = "";
    [BsonElement("sub_category")] public string SubCategory { get; set; } = "";
    [BsonElement("title")] public string Title { get; set; } = "";
    [BsonElement("tags")] public List<string> Tags { get; set; } = [];

    [BsonElement("sintomatologia")] public string? Sintomatologia { get; set; }
    [BsonElement("causas_probables")] public List<string>? CausasProbables { get; set; }
    [BsonElement("procedimiento_resolucion")] public string? ProcedimientoResolucion { get; set; }
    [BsonElement("requisitos_previos")] public string? RequisitosPrevios { get; set; }
    [BsonElement("pasos_desenergizacion")] public List<string>? PasosDesenergizacion { get; set; }
    [BsonElement("buenas_practicas")] public string? BuenasPracticas { get; set; }
    [BsonElement("intervalos_servicio")] public string? IntervalosServicio { get; set; }
    [BsonElement("normativas_clave")] public List<string>? NormativasClave { get; set; }
    [BsonElement("especificaciones")] public Especificaciones? Especificaciones { get; set; }

    /// <summary>Relevancia devuelta por $text (proyección textScore).</summary>
    [BsonElement("score")] public double Score { get; set; }

    /// <summary>Campos rellenos, en orden de lectura. Sirve para el contexto del LLM y para la UI.</summary>
    public IEnumerable<Section> Sections()
    {
        if (!string.IsNullOrWhiteSpace(Sintomatologia)) yield return new("Sintomatología", Sintomatologia);
        if (CausasProbables is { Count: > 0 }) yield return new("Causas probables", string.Join("; ", CausasProbables));
        if (!string.IsNullOrWhiteSpace(RequisitosPrevios)) yield return new("Requisitos previos", RequisitosPrevios);
        if (PasosDesenergizacion is { Count: > 0 }) yield return new("Pasos de desenergización", string.Join(" → ", PasosDesenergizacion));
        if (!string.IsNullOrWhiteSpace(ProcedimientoResolucion)) yield return new("Procedimiento de resolución", ProcedimientoResolucion);
        if (!string.IsNullOrWhiteSpace(BuenasPracticas)) yield return new("Buenas prácticas", BuenasPracticas);
        if (!string.IsNullOrWhiteSpace(IntervalosServicio)) yield return new("Intervalos de servicio", IntervalosServicio);
        if (NormativasClave is { Count: > 0 }) yield return new("Normativas clave", string.Join("; ", NormativasClave));
        if (!string.IsNullOrWhiteSpace(Especificaciones?.CorreaDistribucion)) yield return new("Correa de distribución", Especificaciones.CorreaDistribucion);
        if (!string.IsNullOrWhiteSpace(Especificaciones?.CadenaDistribucion)) yield return new("Cadena de distribución", Especificaciones.CadenaDistribucion);
    }

    public SourceRef ToSource(int n) => new(n, ChunkId, Title, Category, SubCategory, Tags, Score, Sections().ToList());
}
