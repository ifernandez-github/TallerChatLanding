namespace TallerChat.Api.Tests.Unit;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_y_verify_ida_y_vuelta()
    {
        var hash = PasswordHasher.Hash("Mi-clave-segura-1");
        Assert.True(PasswordHasher.Verify("Mi-clave-segura-1", hash));
        Assert.False(PasswordHasher.Verify("otra-clave-1", hash));
    }

    [Fact]
    public void El_hash_nunca_contiene_la_contrasena_y_lleva_sal_distinta()
    {
        var a = PasswordHasher.Hash("Mi-clave-segura-1");
        var b = PasswordHasher.Hash("Mi-clave-segura-1");
        Assert.DoesNotContain("Mi-clave-segura-1", a);
        Assert.NotEqual(a, b);
        Assert.StartsWith("v1.", a);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v2.1.AAAA.AAAA")]
    [InlineData("v1.1000.no-es-base64!.no-es-base64!")]
    public void Un_hash_mal_formado_nunca_valida_ni_lanza(string stored) =>
        Assert.False(PasswordHasher.Verify("lo-que-sea", stored));

    [Fact]
    public void Dummy_es_un_hash_valido_que_no_corresponde_a_ninguna_clave_real()
    {
        Assert.StartsWith("v1.", PasswordHasher.Dummy);
        Assert.False(PasswordHasher.Verify("Taller-Test-123", PasswordHasher.Dummy));
    }
}
