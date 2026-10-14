using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Alta, confirmación por correo, inicio de sesión, contraseña olvidada y sesiones (flujo completo de un cliente).</summary>
public sealed class AuthFlowTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private static string NewEmail() => $"cliente-{Guid.NewGuid():N}@clientes.test";

    [MongoFact]
    public async Task Registro_envia_el_correo_de_confirmacion_y_no_deja_entrar_hasta_confirmarlo()
    {
        var email = NewEmail();
        var anon = App.NewClient();

        var reg = await anon.PostAsync("/api/auth/register", NewAccount(email));

        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var body = await reg.JsonAsync();
        Assert.True(body.GetProperty("registered").GetBoolean());
        Assert.True(body.GetProperty("emailSent").GetBoolean());
        Assert.False(body.GetProperty("signedIn").GetBoolean());

        var mail = Assert.Single(App.Google.To(email));
        Assert.StartsWith("Confirma tu cuenta", mail.Subject);
        Assert.StartsWith("https://taller.test/verificar?token=", mail.LinkTo("/verificar"));

        // Sin confirmar no entra, y la respuesta lleva el código que la web usa para ofrecer «reenviar».
        var login = await anon.PostAsync("/api/auth/login", new { email, password = TallerApp.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("email_not_verified", (await login.JsonAsync()).GetProperty("code").GetString());

        // Confirma con el enlace del correo y ya puede entrar.
        var verify = await anon.PostAsync("/api/auth/verify", new { token = mail.TokenFor("/verificar") });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var ok = await anon.PostAsync("/api/auth/login", new { email, password = TallerApp.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var me = await anon.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.JsonAsync();
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal(Roles.Client, user.GetProperty("role").GetString());
    }

    [MongoFact]
    public async Task El_token_de_confirmacion_solo_se_puede_usar_una_vez_y_en_la_base_solo_se_guarda_su_hash()
    {
        var email = NewEmail();
        var anon = App.NewClient();
        await anon.PostAsync("/api/auth/register", NewAccount(email));
        var token = App.Google.To(email).Single().TokenFor("/verificar");

        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync("/api/auth/verify", new { token })).StatusCode);
        var again = await anon.PostAsync("/api/auth/verify", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("invalid_token", (await again.JsonAsync()).GetProperty("code").GetString());

        var stored = await App.Data.Tokens.Find(_ => true).ToListAsync();
        Assert.All(stored, t => Assert.NotEqual(token, t.Hash));
    }

    [MongoFact]
    public async Task Un_token_inventado_o_vacio_no_confirma_nada()
    {
        var anon = App.NewClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync("/api/auth/verify", new { token = "inventado" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync("/api/auth/verify", new { token = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync("/api/auth/verify", new { token = new string('a', 500) })).StatusCode);
    }

    [MongoTheory]
    [InlineData("nombre", "")]
    [InlineData("email", "no-es-un-correo")]
    [InlineData("confirm", "otro@clientes.test")]
    [InlineData("telefono", "abc")]
    [InlineData("clave", "corta1")]
    [InlineData("clave", "sinnumeroslargaaa")]
    [InlineData("demo", "alguien@demo.taller")]
    public async Task Registro_con_datos_invalidos_da_400_y_no_crea_la_cuenta(string campo, string valor)
    {
        var email = campo == "demo" ? valor : NewEmail();
        object body = campo switch
        {
            "nombre" => new { name = valor, email, emailConfirm = email, phone = "600123123", password = TallerApp.Password },
            "email" => new { name = "Ana", email = valor, emailConfirm = valor, phone = "600123123", password = TallerApp.Password },
            "confirm" => new { name = "Ana", email, emailConfirm = valor, phone = "600123123", password = TallerApp.Password },
            "telefono" => new { name = "Ana", email, emailConfirm = email, phone = valor, password = TallerApp.Password },
            _ => new { name = "Ana", email, emailConfirm = email, phone = "600123123", password = valor }
        };

        var res = await App.NewClient().PostAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(0, await App.Data.Users.CountDocumentsAsync(u => u.Email == email));
        Assert.Empty(App.Emails);
    }

    [MongoFact]
    public async Task Registro_con_un_correo_ya_usado_da_409()
    {
        var email = NewEmail();
        var anon = App.NewClient();
        Assert.Equal(HttpStatusCode.Created, (await anon.PostAsync("/api/auth/register", NewAccount(email))).StatusCode);

        var dup = await anon.PostAsync("/api/auth/register", NewAccount(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [MongoFact]
    public async Task Registro_guarda_la_contrasena_con_hash_y_normaliza_el_correo()
    {
        var email = NewEmail();
        await App.NewClient().PostAsync("/api/auth/register", NewAccount("  " + email.ToUpperInvariant() + " ", confirm: email));

        var user = await App.Data.Users.Find(u => u.Email == email).FirstAsync();
        Assert.StartsWith("v1.", user.PasswordHash);
        Assert.DoesNotContain(TallerApp.Password, user.PasswordHash);
        Assert.False(user.EmailVerified);
        Assert.Equal(Roles.Client, user.Role); // nadie se registra con un rol superior
        Assert.NotEmpty(user.SecurityStamp);
    }

    [MongoFact]
    public async Task El_registro_no_deja_elegir_el_rol()
    {
        var email = NewEmail();
        await App.NewClient().PostAsync("/api/auth/register", new
        {
            name = "Intruso", email, emailConfirm = email, phone = "600123123", password = TallerApp.Password, role = "admin"
        });

        var user = await App.Data.Users.Find(u => u.Email == email).FirstAsync();
        Assert.Equal(Roles.Client, user.Role);
    }

    [MongoFact]
    public async Task Login_con_clave_mala_o_usuario_inexistente_da_exactamente_el_mismo_401()
    {
        var user = await App.UserAsync();
        var anon = App.NewClient();

        var bad = await anon.PostAsync("/api/auth/login", new { email = user.Email, password = "Equivocada-123" });
        var ghost = await anon.PostAsync("/api/auth/login", new { email = "nadie@clientes.test", password = "Equivocada-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ghost.StatusCode);
        Assert.Equal(await bad.Content.ReadAsStringAsync(), await ghost.Content.ReadAsStringAsync());
    }

    [MongoFact]
    public async Task Una_cuenta_desactivada_no_puede_entrar()
    {
        var user = await App.UserAsync(active: false);
        var res = await App.NewClient().PostAsync("/api/auth/login", new { email = user.Email, password = TallerApp.Password });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [MongoFact]
    public async Task Sin_sesion_me_da_401_y_tras_logout_tambien()
    {
        var anon = App.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);

        var user = await App.UserAsync();
        var session = await App.LoginAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me")).StatusCode);

        // Nota: el handler de logout recibe HttpContext y el compilador lo trata como RequestDelegate (aviso ASP0016),
        // así que el Results.NoContent() se descarta y responde 200 en vez de 204. Se acepta cualquier 2xx.
        Assert.True((await session.PostAsync("/api/auth/logout")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
    }

    [MongoFact]
    public async Task La_cookie_de_sesion_es_HttpOnly_y_SameSite_Lax()
    {
        var user = await App.UserAsync();
        var res = await App.NewClient().PostAsync("/api/auth/login", new { email = user.Email, password = TallerApp.Password });

        var cookie = Assert.Single(res.Headers.GetValues("Set-Cookie"), c => c.StartsWith("taller.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [MongoFact]
    public async Task Login_limita_la_fuerza_bruta_a_10_intentos_por_minuto_y_por_IP()
    {
        var anon = App.NewClient(); // misma IP en todas las peticiones
        HttpStatusCode last = default;
        for (var i = 0; i < 11; i++)
            last = (await anon.PostAsync("/api/auth/login", new { email = "x@clientes.test", password = "Equivocada-123" })).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        // Otra IP no queda bloqueada por la primera.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await App.NewClient().PostAsync("/api/auth/login", new { email = "x@clientes.test", password = "Equivocada-123" })).StatusCode);
    }

    // ---------- Contraseña olvidada ----------

    [MongoFact]
    public async Task Forgot_responde_igual_exista_o_no_la_cuenta_y_solo_envia_correo_a_las_que_existen()
    {
        var user = await App.UserAsync();
        var anon = App.NewClient();

        var real = await anon.PostAsync("/api/auth/forgot", new { email = user.Email });
        var ghost = await anon.PostAsync("/api/auth/forgot", new { email = "nadie@clientes.test" });
        var garbage = await anon.PostAsync("/api/auth/forgot", new { email = "no-es-un-correo" });

        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        Assert.Equal(await real.Content.ReadAsStringAsync(), await ghost.Content.ReadAsStringAsync());
        Assert.Equal(await real.Content.ReadAsStringAsync(), await garbage.Content.ReadAsStringAsync());

        var mail = Assert.Single(App.Emails);
        Assert.Equal(user.Email, mail.To);
        Assert.StartsWith("Restablece tu contraseña", mail.Subject);
        Assert.StartsWith("https://taller.test/restablecer?token=", mail.LinkTo("/restablecer"));
    }

    [MongoFact]
    public async Task Forgot_tiene_enfriamiento_por_destinatario_y_sigue_respondiendo_igual()
    {
        var user = await App.UserAsync();
        var anon = App.NewClient();

        var first = await anon.PostAsync("/api/auth/forgot", new { email = user.Email });
        var second = await anon.PostAsync("/api/auth/forgot", new { email = user.Email });

        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Single(App.Emails); // el segundo no envía otro correo
    }

    [MongoFact]
    public async Task Restablecer_cambia_la_clave_invalida_el_enlace_y_cierra_las_otras_sesiones()
    {
        var user = await App.UserAsync();
        var oldSession = await App.LoginAsync(user);
        await App.NewClient().PostAsync("/api/auth/forgot", new { email = user.Email });
        var token = App.Google.To(user.Email).Single().TokenFor("/restablecer");

        var reset = await App.NewClient().PostAsync("/api/auth/reset", new { token, password = "Nueva-clave-456" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // La clave vieja ya no vale, la nueva sí; el enlace no se puede reutilizar.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await App.NewClient().PostAsync("/api/auth/login", new { email = user.Email, password = TallerApp.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await App.NewClient().PostAsync("/api/auth/login", new { email = user.Email, password = "Nueva-clave-456" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await App.NewClient().PostAsync("/api/auth/reset", new { token, password = "Otra-clave-789" })).StatusCode);

        // La sesión abierta antes del cambio (otro dispositivo) cae: el sello de seguridad cambió.
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/auth/me")).StatusCode);
    }

    [MongoFact]
    public async Task Restablecer_con_clave_debil_da_400_y_no_gasta_el_enlace()
    {
        var user = await App.UserAsync();
        await App.NewClient().PostAsync("/api/auth/forgot", new { email = user.Email });
        var token = App.Google.To(user.Email).Single().TokenFor("/restablecer");

        var weak = await App.NewClient().PostAsync("/api/auth/reset", new { token, password = "corta" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var ok = await App.NewClient().PostAsync("/api/auth/reset", new { token, password = "Nueva-clave-456" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [MongoFact]
    public async Task Un_token_de_confirmacion_no_sirve_para_restablecer_la_clave()
    {
        var email = NewEmail();
        var anon = App.NewClient();
        await anon.PostAsync("/api/auth/register", NewAccount(email));
        var verifyToken = App.Google.To(email).Single().TokenFor("/verificar");

        var res = await anon.PostAsync("/api/auth/reset", new { token = verifyToken, password = "Nueva-clave-456" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [MongoFact]
    public async Task Resend_responde_siempre_igual_y_solo_reenvia_a_cuentas_sin_confirmar()
    {
        var email = NewEmail();
        var anon = App.NewClient();
        await anon.PostAsync("/api/auth/register", NewAccount(email));
        App.Google.ClearEmails();
        var verified = await App.UserAsync();

        var pending = await anon.PostAsync("/api/auth/resend", new { email });
        var alreadyVerified = await anon.PostAsync("/api/auth/resend", new { email = verified.Email });
        var ghost = await anon.PostAsync("/api/auth/resend", new { email = "nadie@clientes.test" });

        Assert.Equal(await pending.Content.ReadAsStringAsync(), await alreadyVerified.Content.ReadAsStringAsync());
        Assert.Equal(await pending.Content.ReadAsStringAsync(), await ghost.Content.ReadAsStringAsync());
        var mail = Assert.Single(App.Emails);
        Assert.Equal(email, mail.To);
    }

    [MongoFact]
    public async Task Si_Gmail_falla_el_registro_no_se_cae_y_avisa_de_que_el_correo_no_salio()
    {
        App.Google.FailSends = true;
        var email = NewEmail();

        var res = await App.NewClient().PostAsync("/api/auth/register", NewAccount(email));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.False((await res.JsonAsync()).GetProperty("emailSent").GetBoolean());
        Assert.Equal(1, await App.Data.Users.CountDocumentsAsync(u => u.Email == email));
    }

    [MongoFact]
    public async Task Sin_correo_configurado_fuera_de_desarrollo_no_se_pueden_crear_cuentas()
    {
        var app = Start(new TallerAppOptions { EmailEnabled = false });

        var res = await app.NewClient().PostAsync("/api/auth/register", NewAccount(NewEmail()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await app.NewClient().PostAsync("/api/auth/forgot", new { email = "a@b.test" })).StatusCode);
    }

    [MongoFact]
    public async Task En_desarrollo_sin_correo_la_cuenta_nace_verificada_y_con_sesion_abierta()
    {
        var app = Start(new TallerAppOptions { Environment = "Development", EmailEnabled = false });
        var email = NewEmail();
        var client = app.NewClient();

        var res = await client.PostAsync("/api/auth/register", NewAccount(email));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.True((await res.JsonAsync()).GetProperty("signedIn").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
