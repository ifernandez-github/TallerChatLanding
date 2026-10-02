using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Recuperación de fragmentos con búsqueda de texto de MongoDB (requiere el índice de scripts/mongo-setup.js).</summary>
public sealed class KnowledgeService(IMongoDatabase db, IOptions<MongoOptions> options)
{
    private readonly IMongoCollection<KnowledgeChunk> _col = db.GetCollection<KnowledgeChunk>(options.Value.Collection);
    private readonly int _max = options.Value.MaxResults;

    public async Task<List<KnowledgeChunk>> SearchAsync(string query, CancellationToken ct)
    {
        // En $text, las comillas y el guion inicial tienen significado especial (frase exacta / negación).
        var clean = query.Replace('"', ' ').Replace('-', ' ');
        var b = Builders<KnowledgeChunk>.Projection;
        return await _col.Find(Builders<KnowledgeChunk>.Filter.Text(clean))
            .Project<KnowledgeChunk>(b.MetaTextScore("score"))
            .Sort(Builders<KnowledgeChunk>.Sort.MetaTextScore("score"))
            .Limit(_max)
            .ToListAsync(ct);
    }
}
