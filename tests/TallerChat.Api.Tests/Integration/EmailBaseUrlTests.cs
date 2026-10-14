using System.Net;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>
/// La URL base de los enlaces de los correos (confirmar cuenta, restablecer clave, ver mis citas, agenda de admin):
/// Shop:PublicBaseUrl → RENDER_EXTERNAL_URL → origen de la primera petición. Un enlace a localhost en un correo real no abre.
/// </summary>
public sealed class EmailBaseUrlTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private const string Local = "http://localhost:5173";

    private static string NewEmail() => $"cliente-{Guid.NewGuid():N}@clientes.test";

    /// <summary>Registra una cuenta (opcionalmente con otro Host) y devuelve el enlace de confirmación del correo.</summary>
    private static async Task<string> VerifyLinkAsync(TallerApp app, string? host = null)
    {
        var email = NewEmail();
        var res = await app.NewClient(host).PostAsync("/api/auth/register", NewAccount(email));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return app.Google.To(email).Single().LinkTo("/verificar");
    }

    [MongoTheory]
    [InlineData("https://taller.example.com")]
    [InlineData("https://taller.example.com/")]
    [InlineData("taller.example.com")] // sin esquema
    public async Task Si_hay_URL_publica_configurada_los_enlaces_cuelgan_de_ella(string configured)
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = configured });

        var link = await VerifyLinkAsync(app);

        Assert.StartsWith("https://taller.example.com/verificar?token=", link);
        Assert.DoesNotContain("//verificar", link);
    }

    [MongoFact]
    public async Task Si_la_configurada_es_localhost_se_usa_RENDER_EXTERNAL_URL()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = Local, RenderExternalUrl = "https://tallerchat.onrender.com" });

        var link = await VerifyLinkAsync(app);

        Assert.StartsWith("https://tallerchat.onrender.com/verificar?token=", link);
    }

    [MongoFact]
    public async Task La_URL_configurada_gana_a_la_de_Render()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = "https://taller.example.com", RenderExternalUrl = "https://tallerchat.onrender.com" });

        Assert.StartsWith("https://taller.example.com/", await VerifyLinkAsync(app));
    }

    [MongoFact]
    public async Task Sin_configuracion_la_primera_peticion_fija_la_URL_y_las_siguientes_no_la_cambian()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = Local });

        var first = await VerifyLinkAsync(app, host: "taller.example.org");
        var second = await VerifyLinkAsync(app, host: "otro-host-falso.example.net"); // cabecera Host manipulada

        Assert.StartsWith("http://taller.example.org/verificar?token=", first);
        Assert.StartsWith("http://taller.example.org/verificar?token=", second);
    }

    [MongoFact]
    public async Task Si_la_peticion_tambien_es_localhost_el_enlace_sigue_siendo_el_configurado()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = Local });

        Assert.StartsWith(Local + "/verificar?token=", await VerifyLinkAsync(app));
    }

    [MongoFact]
    public async Task En_desarrollo_la_URL_de_la_peticion_no_se_usa_nunca()
    {
        var app = Start(new TallerAppOptions { Environment = "Development", PublicBaseUrl = Local });

        // En desarrollo la cuenta se verifica sola si no hay correo; aquí hay correo, así que sale el mail.
        var link = await VerifyLinkAsync(app, host: "taller.example.org");

        Assert.StartsWith(Local + "/verificar?token=", link);
    }

    [MongoFact]
    public async Task El_enlace_de_restablecer_usa_la_misma_URL_base()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = "https://taller.example.com/" });
        var user = await app.UserAsync();

        await app.NewClient().PostAsync("/api/auth/forgot", new { email = user.Email });

        Assert.StartsWith("https://taller.example.com/restablecer?token=", app.Google.To(user.Email).Single().LinkTo("/restablecer"));
    }

    [MongoFact]
    public async Task Los_correos_de_citas_enlazan_a_mi_cuenta_y_a_la_agenda_con_la_URL_base()
    {
        var app = Start(new TallerAppOptions { PublicBaseUrl = "https://taller.example.com/", PlaceholderDomain = "torque.es" });
        var admin = await app.UserAsync(Roles.Admin, email: "admin@torque.es");
        var client = await app.UserAsync();
        var session = await app.LoginAsync(client);
        var slot = await app.NextSlotAsync();

        var res = await session.PostAsync("/api/appointments", new { serviceId = "oil", start = slot, plate = "4821KJH", vehicle = "Seat León" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var toClient = app.Google.To(client.Email).Single();
        Assert.Contains("https://taller.example.com/mi-cuenta", toClient.Text);
        Assert.DoesNotContain("localhost", toClient.Text + toClient.Html);

        // La cuenta de admin con dominio de ejemplo no es un buzón real: el aviso va al remitente configurado.
        var toAdmin = app.Google.To("taller@correo-real.test").Single();
        var localDay = TestDb.Booking().LocalDateIso(slot);
        Assert.Contains($"https://taller.example.com/admin?fecha={localDay}&cita=", toAdmin.Text);
        Assert.Empty(app.Google.To(admin.Email));
    }
}
