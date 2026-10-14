using System.Net;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>«Enviar por email» del chat: asunto y plantilla fijos, validaciones, enfriamiento por destinatario y límite por IP.</summary>
public sealed class ChatEmailTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private static object Items(string question = "¿Cada cuánto cambio el aceite?", string answer = "Cada 15.000 km [1].", int sources = 1) => new[]
    {
        new
        {
            question, answer,
            sources = Enumerable.Range(1, sources).Select(i => new
            {
                @ref = i, chunkId = $"aceite-00{i}", title = "Cambio de aceite", category = "Mantenimiento", subCategory = "Aceite",
                tags = new[] { "aceite" }, score = 1.2, sections = new[] { new { label = "Intervalos", text = "Cada 15.000 km" } }
            }).ToArray()
        }
    };

    private static Task<HttpResponseMessage> Send(HttpClient c, string to, object? items = null) =>
        c.PostAsync("/api/email", new { to, items = items ?? Items() });

    private static string NewTo() => $"visitante-{Guid.NewGuid():N}@clientes.test";

    [MongoFact]
    public async Task Features_dice_si_el_email_esta_disponible()
    {
        var on = await (await App.NewClient().GetAsync("/api/features")).JsonAsync();
        Assert.True(on.GetProperty("email").GetBoolean());

        var off = Start(new TallerAppOptions { EmailEnabled = false });
        var disabled = await (await off.NewClient().GetAsync("/api/features")).JsonAsync();
        Assert.False(disabled.GetProperty("email").GetBoolean());
    }

    [MongoFact]
    public async Task Envia_un_correo_con_asunto_fijo_y_el_contenido_de_la_conversacion()
    {
        var to = NewTo();

        var res = await Send(App.NewClient(), to);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var mail = Assert.Single(App.Google.To(to));
        Assert.Equal("Respuesta del asistente del taller", mail.Subject);
        Assert.Contains("Cada 15.000 km", mail.Html);
        Assert.Contains("aceite-001", mail.Text);
    }

    [MongoFact]
    public async Task El_contenido_del_visitante_se_codifica_y_no_se_puede_inyectar_HTML()
    {
        var to = NewTo();

        await Send(App.NewClient(), to, Items(question: "<script>alert(1)</script>", answer: "<a href='http://malo'>pincha</a>"));

        var mail = App.Google.To(to).Single();
        Assert.DoesNotContain("<script>", mail.Html);
        Assert.DoesNotContain("<a href='http://malo'>", mail.Html);
        Assert.Contains("&lt;script&gt;", mail.Html);
    }

    [MongoFact]
    public async Task Hay_enfriamiento_por_destinatario_y_otro_destinatario_no_se_ve_afectado()
    {
        var to = NewTo();

        Assert.Equal(HttpStatusCode.OK, (await Send(App.NewClient(), to)).StatusCode);
        var again = await Send(App.NewClient(), to.ToUpperInvariant()); // otra IP, mismo destinatario
        var other = await Send(App.NewClient(), NewTo());

        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Single(App.Google.To(to));
    }

    [MongoFact]
    public async Task Limita_a_3_correos_por_hora_y_por_IP()
    {
        var client = App.NewClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++) codes.Add((await Send(client, NewTo())).StatusCode);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], codes);
    }

    [MongoTheory]
    [InlineData("no-es-un-correo")]
    [InlineData("a@b")]
    [InlineData("uno@x.test,dos@y.test")]
    [InlineData("victima@x.test\r\nBcc: otro@y.test")]
    [InlineData("")]
    public async Task Destinatario_invalido_da_400_y_no_envia_nada(string to)
    {
        var res = await Send(App.NewClient(), to);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(App.Emails);
    }

    [MongoFact]
    public async Task Contenido_invalido_o_excesivo_da_400()
    {
        var anon = App.NewClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync("/api/email", new { to = NewTo(), items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(App.NewClient(), NewTo(), Items(question: ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(App.NewClient(), NewTo(), Items(question: new string('q', 501)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(App.NewClient(), NewTo(), Items(answer: new string('a', 4001)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(App.NewClient(), NewTo(), Items(sources: 9))).StatusCode);

        var six = Enumerable.Range(0, 6).Select(_ => new { question = "q", answer = "a", sources = Array.Empty<object>() }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(App.NewClient(), NewTo(), six)).StatusCode);
        Assert.Empty(App.Emails);
    }

    [MongoFact]
    public async Task Si_Gmail_falla_da_502_y_el_destinatario_puede_reintentar_sin_esperar()
    {
        var to = NewTo();
        App.Google.FailSends = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await Send(App.NewClient(), to)).StatusCode);

        App.Google.FailSends = false;
        Assert.Equal(HttpStatusCode.OK, (await Send(App.NewClient(), to)).StatusCode); // el enfriamiento se retiró al fallar
    }

    [MongoFact]
    public async Task Sin_correo_configurado_da_503()
    {
        var app = Start(new TallerAppOptions { EmailEnabled = false });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(app.NewClient(), NewTo())).StatusCode);
    }
}
