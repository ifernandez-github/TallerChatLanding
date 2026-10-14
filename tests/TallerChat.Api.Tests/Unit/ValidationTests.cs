namespace TallerChat.Api.Tests.Unit;

public class ValidationTests
{
    [Theory]
    [InlineData("  Ana@Correo.ES ", "ana@correo.es")]
    [InlineData("a.b+c@sub.dominio.com", "a.b+c@sub.dominio.com")]
    public void Email_valido_se_normaliza(string input, string expected)
    {
        Assert.True(Validation.TryEmail(input, out var email));
        Assert.Equal(expected, email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-arroba.com")]
    [InlineData("dos@@arrobas.com")]
    [InlineData("espacio en@medio.com")]
    [InlineData("a@b.c")] // TLD de una letra
    [InlineData("uno@x.com,dos@y.com")] // inyección de varios destinatarios
    [InlineData("uno@x.com;dos@y.com")]
    [InlineData("<uno@x.com>")]
    public void Email_invalido(string? input) => Assert.False(Validation.TryEmail(input, out _));

    [Fact]
    public void Email_demasiado_largo() =>
        Assert.False(Validation.TryEmail(new string('a', 250) + "@x.com", out _));

    [Theory]
    [InlineData("Abcdefgh12", true)]
    [InlineData("corta1A", false)]            // < 10
    [InlineData("soloLetrasLargas", false)]   // sin número
    [InlineData("12345678901234", false)]     // sin letra
    [InlineData(null, false)]
    public void Contrasena_fuerte(string? p, bool expected) => Assert.Equal(expected, Validation.StrongPassword(p));

    [Fact]
    public void Contrasena_de_mas_de_100_caracteres_no_vale() =>
        Assert.False(Validation.StrongPassword(new string('a', 100) + "1"));

    [Theory]
    [InlineData("4821 kjh", "4821KJH")]
    [InlineData("4821-KJH", "4821KJH")]
    [InlineData("m-1234-zx", "M1234ZX")]
    public void Matricula_se_normaliza(string input, string expected)
    {
        Assert.True(Validation.TryPlate(input, out var plate));
        Assert.Equal(expected, plate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("!!!!")]
    public void Matricula_invalida(string input) => Assert.False(Validation.TryPlate(input, out _));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("600 123 123", true)]
    [InlineData("+34 910 000 000", true)]
    [InlineData("abc", false)]
    [InlineData("123", false)]
    public void Telefono(string? input, bool expected) => Assert.Equal(expected, Validation.TryPhone(input, out _));

    [Theory]
    [InlineData("Al", true)]
    [InlineData("A", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Nombre(string? input, bool expected) => Assert.Equal(expected, Validation.TryName(input, out _));

    [Fact]
    public void Nombre_de_mas_de_80_caracteres_no_vale() => Assert.False(Validation.TryName(new string('a', 81), out _));

    [Fact]
    public void Direccion_nula_o_vacia_es_valida_y_queda_en_null()
    {
        Assert.True(Validation.TryAddress(null, out var a1, out _));
        Assert.Null(a1);
        Assert.True(Validation.TryAddress(new AddressDto(" ", "", null, null), out var a2, out _));
        Assert.Null(a2);
    }

    [Fact]
    public void Direccion_con_codigo_postal_malo_se_rechaza()
    {
        Assert.False(Validation.TryAddress(new AddressDto("Calle 1", "ABC", "Madrid", null), out _, out var problem));
        Assert.Contains("postal", problem);
    }

    [Fact]
    public void Direccion_recorta_los_campos()
    {
        Assert.True(Validation.TryAddress(new AddressDto("  Calle del Motor 24 ", " 28045 ", "Madrid", null), out var a, out _));
        Assert.Equal("Calle del Motor 24", a!.Street);
        Assert.Equal("28045", a.PostalCode);
    }

    private static VehicleRequest Car(string? make = "Seat", string? model = "León", string? plate = "4821KJH", int? year = 2021,
        int? km = 1000, string? fuel = "gasolina", string? vin = null) => new(make, model, plate, year, km, fuel, vin);

    [Fact]
    public void Vehiculo_valido_se_normaliza()
    {
        Assert.True(Validation.TryVehicle(Car(plate: "4821-kjh", vin: "vsszzz5fzmr123456"), null, out var v, out _));
        Assert.Equal("4821KJH", v.Plate);
        Assert.Equal("VSSZZZ5FZMR123456", v.Vin);
    }

    [Fact]
    public void Vehiculo_conserva_el_id_al_editar()
    {
        Assert.True(Validation.TryVehicle(Car(), "abc123", out var v, out _));
        Assert.Equal("abc123", v.Id);
    }

    [Theory]
    [InlineData("marca", "")]
    [InlineData("plate", "!!")]
    [InlineData("year", "1850")]
    [InlineData("km", "-1")]
    [InlineData("km", "3000000")]
    [InlineData("fuel", "agua")]
    [InlineData("vin", "VSSZZZ5FZMR12345O")] // contiene O
    [InlineData("vin", "CORTO")]
    public void Vehiculo_invalido(string field, string value)
    {
        var req = field switch
        {
            "marca" => Car(make: value),
            "plate" => Car(plate: value),
            "year" => Car(year: int.Parse(value)),
            "km" => Car(km: int.Parse(value)),
            "fuel" => Car(fuel: value),
            _ => Car(vin: value)
        };
        Assert.False(Validation.TryVehicle(req, null, out _, out var problem));
        Assert.False(string.IsNullOrEmpty(problem));
    }

    [Fact]
    public void Vehiculo_nulo_se_rechaza() => Assert.False(Validation.TryVehicle(null, null, out _, out _));
}
