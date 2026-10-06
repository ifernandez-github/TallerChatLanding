using System.Security.Cryptography;

namespace TallerChat.Api;

/// <summary>PBKDF2-SHA256 con sal aleatoria. Formato: v1.iteraciones.sal.hash (base64).</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000, SaltBytes = 16, KeyBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('.');
        if (p.Length != 4 || p[0] != "v1" || !int.TryParse(p[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(p[2]);
            var expected = Convert.FromBase64String(p[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Hash de relleno: se verifica contra él cuando el usuario no existe, para que el tiempo de respuesta no delate si el correo está registrado.</summary>
    public static readonly string Dummy = Hash("relleno-para-igualar-tiempos-1");
}
