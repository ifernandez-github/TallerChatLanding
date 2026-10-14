namespace TallerChat.Api.Tests.Unit;

/// <summary>
/// URL base de los enlaces de los correos: Shop:PublicBaseUrl, si es real; si no, RENDER_EXTERNAL_URL; si no, la
/// configurada tal cual. Un enlace a localhost en un correo real es un enlace roto.
/// </summary>
public class PublicUrlTests
{
    [Theory]
    [InlineData("https://taller.example.com", "https://taller.example.com")]
    [InlineData("https://taller.example.com/", "https://taller.example.com")]
    [InlineData("  taller.example.com  ", "https://taller.example.com")] // sin esquema -> https
    [InlineData("http://taller.example.com/app/", "http://taller.example.com/app")]
    [InlineData("https://taller.example.com:8443", "https://taller.example.com:8443")]
    public void Normalize_valida(string input, string expected) => Assert.Equal(expected, PublicUrl.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://taller.example.com")]
    public void Normalize_invalida(string? input) => Assert.Null(PublicUrl.Normalize(input));

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://127.0.0.1:5080", true)]
    [InlineData("localhost:5173", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("https://taller.example.com", false)]
    public void IsLocal(string? url, bool expected) => Assert.Equal(expected, PublicUrl.IsLocal(url));

    [Theory]
    // configurada real: gana siempre
    [InlineData("https://taller.example.com/", "https://render.onrender.com", "https://taller.example.com")]
    // configurada es localhost -> RENDER_EXTERNAL_URL
    [InlineData("http://localhost:5173", "https://render.onrender.com", "https://render.onrender.com")]
    [InlineData("", "https://render.onrender.com/", "https://render.onrender.com")]
    // ninguna sirve -> la configurada tal cual (la petición actuará de último recurso)
    [InlineData("http://localhost:5173", null, "http://localhost:5173")]
    [InlineData("http://localhost:5173", "http://localhost:10000", "http://localhost:5173")]
    [InlineData(null, null, "")]
    public void Resolve(string? configured, string? hosting, string expected) =>
        Assert.Equal(expected, PublicUrl.Resolve(configured, hosting));
}
