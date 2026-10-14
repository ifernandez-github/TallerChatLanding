using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Quién entra dónde: anónimo, Cliente, Gestor (manager) y Administrador frente a cada grupo de rutas.</summary>
public sealed class RoleAuthorizationTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    public static TheoryData<string> PanelDeGestion => new()
    {
        "/api/admin/summary",
        "/api/admin/calendar",
        "/api/admin/appointments?pending=true",
        "/api/admin/users"
    };

    [MongoTheory]
    [MemberData(nameof(PanelDeGestion))]
    public async Task El_panel_de_gestion_es_para_gestor_y_administrador_pero_no_para_clientes_ni_anonimos(string url)
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));
        var client = await App.LoginAsync(await App.UserAsync(Roles.Client));

        Assert.Equal(HttpStatusCode.Unauthorized, (await App.NewClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
    }

    [MongoFact]
    public async Task Un_cliente_no_puede_usar_ninguna_accion_de_administracion()
    {
        var target = await App.UserAsync();
        var client = await App.LoginAsync(await App.UserAsync());
        var id = target.Id;

        var attempts = new[]
        {
            await client.PutAsJsonAsync($"/api/admin/users/{id}", new { name = "Hackeado" }),
            await client.PostAsync($"/api/admin/users/{id}/active", new { active = false }),
            await client.PostAsync($"/api/admin/users/{id}/delete", new { confirmEmail = target.Email }),
            await client.GetAsync($"/api/admin/users/{id}/deletion"),
            await client.PostAsync($"/api/admin/users/{id}/vehicles", new { make = "Seat", model = "Ibiza", plate = "1234ABC" }),
            await client.PostAsync($"/api/admin/appointments/{id}/status", new { status = "confirmed" })
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.NotEqual("Hackeado", (await App.Data.Users.Find(u => u.Id == id).FirstAsync()).Name);
    }

    [MongoFact]
    public async Task Las_rutas_del_area_de_cliente_exigen_sesion()
    {
        var anon = App.NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/appointments/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/appointments", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/appointments/mine/email")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync("/api/profile", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/profile/vehicles", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/profile/delete", new { password = "x" })).StatusCode);
    }

    [MongoFact]
    public async Task Las_rutas_publicas_no_piden_sesion()
    {
        var anon = App.NewClient();

        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/features")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/appointments/services")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/appointments/first-available")).StatusCode);
    }

    [MongoFact]
    public async Task El_rol_viaja_en_la_sesion_y_la_API_lo_devuelve_en_me()
    {
        foreach (var role in Roles.All)
        {
            var session = await App.LoginAsync(await App.UserAsync(role));
            var me = await (await session.GetAsync("/api/auth/me")).JsonAsync();
            Assert.Equal(role, me.GetProperty("role").GetString());
        }
    }

    [MongoFact]
    public async Task Una_cookie_manipulada_o_ajena_no_da_acceso()
    {
        var client = App.NewClient();
        client.DefaultRequestHeaders.Add("Cookie", "taller.auth=CfDJ8-cookie-inventada-con-rol-admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [MongoFact]
    public async Task Una_ruta_de_API_inexistente_da_404_y_no_el_HTML_de_la_SPA()
    {
        var res = await App.NewClient().GetAsync("/api/esto-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.DoesNotContain("text/html", res.Content.Headers.ContentType?.ToString() ?? "");
    }

    [MongoFact]
    public async Task Las_rutas_solo_de_desarrollo_no_existen_en_produccion_y_si_en_desarrollo()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await App.NewClient().GetAsync("/api/models")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await App.NewClient().GetAsync("/openapi/v1.json")).StatusCode);

        var dev = Start(new TallerAppOptions { Environment = "Development" });
        Assert.Equal(HttpStatusCode.OK, (await dev.NewClient().GetAsync("/openapi/v1.json")).StatusCode);
    }
}
