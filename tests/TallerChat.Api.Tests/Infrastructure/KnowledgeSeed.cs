using MongoDB.Bson;
using MongoDB.Driver;

namespace TallerChat.Api.Tests.Infrastructure;

/// <summary>Base de conocimiento de prueba, con el mismo índice de texto que scripts/mongo-setup.js.</summary>
public static class KnowledgeSeed
{
    public static async Task InstallAsync(IMongoDatabase db)
    {
        var col = db.GetCollection<KnowledgeChunk>("conocimiento");
        await col.Indexes.CreateOneAsync(new CreateIndexModel<KnowledgeChunk>(
            new BsonDocument
            {
                ["title"] = "text", ["tags"] = "text", ["category"] = "text", ["sub_category"] = "text",
                ["sintomatologia"] = "text", ["causas_probables"] = "text", ["procedimiento_resolucion"] = "text",
                ["requisitos_previos"] = "text", ["buenas_practicas"] = "text", ["intervalos_servicio"] = "text",
                ["normativas_clave"] = "text", ["pasos_desenergizacion"] = "text",
                ["especificaciones.cadena_distribucion"] = "text", ["especificaciones.correa_distribucion"] = "text"
            },
            new CreateIndexOptions
            {
                Name = "conocimiento_text", DefaultLanguage = "spanish",
                Weights = new BsonDocument { ["title"] = 10, ["tags"] = 8, ["sintomatologia"] = 5, ["category"] = 3, ["sub_category"] = 3 }
            }));

        await col.InsertManyAsync(
        [
            new KnowledgeChunk
            {
                ChunkId = "aceite-001", Category = "Mantenimiento", SubCategory = "Aceite", Title = "Cambio de aceite de motor",
                Tags = ["aceite", "mantenimiento", "filtro"],
                IntervalosServicio = "Cada 15.000 km o 12 meses, lo que antes ocurra.",
                BuenasPracticas = "Sustituir siempre el filtro de aceite junto con el aceite."
            },
            new KnowledgeChunk
            {
                ChunkId = "frenos-001", Category = "Frenos", SubCategory = "Pastillas", Title = "Chirrido al frenar",
                Tags = ["frenos", "pastillas", "ruido"],
                Sintomatologia = "Chirrido metálico al frenar a baja velocidad.",
                CausasProbables = ["Pastillas gastadas", "Discos con rebaba"],
                ProcedimientoResolucion = "Medir el espesor de las pastillas y sustituirlas si están bajo el mínimo."
            },
            new KnowledgeChunk
            {
                ChunkId = "alta-tension-001", Category = "Eléctricos", SubCategory = "Alta tensión", Title = "Desenergización del sistema de alta tensión",
                Tags = ["híbrido", "eléctrico", "seguridad"],
                PasosDesenergizacion = ["Apagar el vehículo", "Retirar el conector de servicio", "Esperar 10 minutos"],
                NormativasClave = ["ISO 6469"]
            }
        ]);
    }
}
