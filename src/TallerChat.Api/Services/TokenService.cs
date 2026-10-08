using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>
/// Enlaces de un solo uso que viajan por correo (confirmar la cuenta, restablecer la contraseña).
/// En la base de datos solo se guarda el hash: quien lea la colección no puede usar los enlaces.
/// </summary>
public sealed class TokenService(Db db, IMemoryCache cache, IOptions<AuthOptions> options)
{
    private readonly AuthOptions o = options.Value;

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Emite un enlace nuevo e invalida los anteriores del mismo tipo para ese usuario.</summary>
    public async Task<string> IssueAsync(string userId, string kind, CancellationToken ct)
    {
        await InvalidateAsync(userId, kind, ct);

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_'); // seguro dentro de una URL
        var ttl = kind == TokenKinds.Reset
            ? TimeSpan.FromMinutes(o.ResetTokenMinutes)
            : TimeSpan.FromHours(o.VerifyTokenHours);

        await db.Tokens.InsertOneAsync(new AuthToken
        {
            UserId = userId, Kind = kind, Hash = Sha256(raw), ExpiresAt = DateTime.UtcNow.Add(ttl)
        }, cancellationToken: ct);
        return raw;
    }

    /// <summary>Gasta el token (en la misma operación, para que no pueda usarse dos veces) y devuelve su usuario.</summary>
    public async Task<AppUser?> ConsumeAsync(string? raw, string kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 200) return null;

        var f = Builders<AuthToken>.Filter;
        var token = await db.Tokens.FindOneAndUpdateAsync(
            f.Eq(t => t.Hash, Sha256(raw)) & f.Eq(t => t.Kind, kind) &
            f.Eq(t => t.UsedAt, (DateTime?)null) & f.Gt(t => t.ExpiresAt, DateTime.UtcNow),
            Builders<AuthToken>.Update.Set(t => t.UsedAt, DateTime.UtcNow), cancellationToken: ct);
        if (token is null) return null;

        return await db.Users.Find(u => u.Id == token.UserId).FirstOrDefaultAsync(ct);
    }

    public Task InvalidateAsync(string userId, string kind, CancellationToken ct) =>
        db.Tokens.DeleteManyAsync(t => t.UserId == userId && t.Kind == kind, ct);

    /// <summary>Freno al reenvío: evita que se pueda bombardear a correos de una dirección ajena.</summary>
    public bool TryStartCooldown(string email, string kind)
    {
        var key = $"acct:{kind}:{Sha256(email.ToLowerInvariant())}";
        if (cache.TryGetValue(key, out _)) return false;
        cache.Set(key, true, TimeSpan.FromMinutes(o.EmailCooldownMinutes));
        return true;
    }

    /// <summary>URL del enlace que se manda por correo, apuntando siempre a la web pública.</summary>
    public static string Link(ShopOptions shop, string path, string token) =>
        $"{shop.PublicBaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(token)}";
}
