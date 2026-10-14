namespace TallerChat.Api.Tests.Unit;

public class TokenLinkTests
{
    [Theory]
    [InlineData("https://taller.example.com")]
    [InlineData("https://taller.example.com/")]
    public void El_enlace_cuelga_de_la_url_publica_sin_barras_dobles(string baseUrl)
    {
        var link = TokenService.Link(new ShopOptions { PublicBaseUrl = baseUrl }, "/verificar", "abc");
        Assert.Equal("https://taller.example.com/verificar?token=abc", link);
    }

    [Fact]
    public void El_token_se_escapa_para_ir_en_una_url()
    {
        var link = TokenService.Link(new ShopOptions { PublicBaseUrl = "https://t.test" }, "/restablecer", "a+b/c=d&e");
        Assert.Equal("https://t.test/restablecer?token=a%2Bb%2Fc%3Dd%26e", link);
    }
}
