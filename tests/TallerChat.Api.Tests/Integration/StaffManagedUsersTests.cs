using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>
/// Alta de cuentas por el personal (ya confirmadas, sin correo de verificación) y gestión de las citas de cualquier cuenta
/// que el rol pueda tocar: el administrador, todas; el gestor, solo las de clientes.
/// </summary>
public sealed class StaffManagedUsersTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private const string StrongPassword = "Clave-segura-2026";

    private static string NewEmail() => $"alta-{Guid.NewGuid():N}@clientes.test";

    private async Task<HttpClient> AsAsync(string role) => await App.LoginAsync(await App.UserAsync(role));

    // ---------- Alta ----------

    [MongoFact]
    public async Task El_gestor_crea_un_cliente_ya_confirmado_que_puede_entrar_sin_verificar_el_correo()
    {
        var manager = await AsAsync(Roles.Manager);
        var email = NewEmail();

        var res = await manager.PostAsync("/api/admin/users", new { name = "Lucía Prado", email, phone = "611222333", password = StrongPassword });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var saved = await App.Data.Users.Find(u => u.Email == email).FirstAsync();
        Assert.True(saved.EmailVerified);
        Assert.True(saved.Active);
        Assert.Equal(Roles.Client, saved.Role);
        Assert.NotEqual(StrongPassword, saved.PasswordHash);

        var login = await App.NewClient().PostAsJsonAsync("/api/auth/login", new { email, password = StrongPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [MongoFact]
    public async Task El_gestor_no_puede_crear_gestores_ni_administradores()
    {
        var manager = await AsAsync(Roles.Manager);
        foreach (var role in new[] { Roles.Manager, Roles.Admin })
        {
            var email = NewEmail();
            var res = await manager.PostAsync("/api/admin/users", new { name = "Intruso", email, password = StrongPassword, role });
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.False(await App.Data.Users.Find(u => u.Email == email).AnyAsync());
        }
    }

    [MongoFact]
    public async Task El_administrador_crea_cuentas_de_cualquier_rol_confirmadas()
    {
        var admin = await AsAsync(Roles.Admin);
        foreach (var role in new[] { Roles.Client, Roles.Manager, Roles.Admin })
        {
            var email = NewEmail();
            var res = await admin.PostAsync("/api/admin/users", new { name = "Cuenta nueva", email, password = StrongPassword, role });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            var saved = await App.Data.Users.Find(u => u.Email == email).FirstAsync();
            Assert.Equal(role, saved.Role);
            Assert.True(saved.EmailVerified);
        }
    }

    [MongoFact]
    public async Task El_alta_valida_los_datos_y_rechaza_correos_repetidos()
    {
        var admin = await AsAsync(Roles.Admin);
        var existing = await App.UserAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/users", new { name = "A", email = NewEmail(), password = StrongPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/users", new { name = "Ana Díaz", email = "no-es-un-correo", password = StrongPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/users", new { name = "Ana Díaz", email = NewEmail(), password = "corta1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/users", new { name = "Ana Díaz", email = NewEmail(), password = StrongPassword, role = "superusuario" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/admin/users", new { name = "Ana Díaz", email = existing.Email, password = StrongPassword })).StatusCode);
    }

    [MongoFact]
    public async Task Un_cliente_no_puede_crear_cuentas()
    {
        var client = await App.LoginAsync(await App.UserAsync());
        var res = await client.PostAsync("/api/admin/users", new { name = "Ana Díaz", email = NewEmail(), password = StrongPassword });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- Citas de otra cuenta ----------

    [MongoFact]
    public async Task El_gestor_reserva_una_cita_a_nombre_de_un_cliente_y_la_ve_en_su_historial()
    {
        var manager = await AsAsync(Roles.Manager);
        var client = await App.UserAsync();
        var slot = await App.NextSlotAsync();

        var res = await manager.PostAsync($"/api/admin/users/{client.Id}/appointments",
            new { serviceId = "oil", start = slot, plate = "4821KJH", vehicle = "Seat León 1.5", notes = "Llamó por teléfono" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var stored = await App.Data.Appointments.Find(a => a.UserId == client.Id).ToListAsync();
        Assert.Single(stored);
        Assert.Equal(Statuses.Pending, stored[0].Status);

        var list = await (await manager.GetAsync($"/api/admin/users/{client.Id}/appointments")).JsonAsync();
        Assert.Equal(1, list.GetArrayLength());
        // El cliente la ve también desde su propia sesión.
        var mine = await (await (await App.LoginAsync(client)).GetAsync("/api/appointments/mine")).JsonAsync();
        Assert.Equal(1, mine.GetArrayLength());
    }

    [MongoFact]
    public async Task El_personal_cancela_una_cita_de_un_cliente()
    {
        var admin = await AsAsync(Roles.Admin);
        var client = await App.UserAsync();
        var res = await admin.PostAsync($"/api/admin/users/{client.Id}/appointments",
            new { serviceId = "oil", start = await App.NextSlotAsync(), plate = "4821KJH", vehicle = "Seat León 1.5" });
        var id = (await res.JsonAsync()).GetProperty("appointment").GetProperty("id").GetString();

        var cancel = await admin.PostAsync($"/api/admin/appointments/{id}/status", new { status = "cancelled" });

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(Statuses.Cancelled, (await App.Data.Appointments.Find(a => a.Id == id).FirstAsync()).Status);
    }

    [MongoFact]
    public async Task El_gestor_no_ve_ni_reserva_citas_de_administradores_o_gestores_pero_el_administrador_si()
    {
        var manager = await AsAsync(Roles.Manager);
        var admin = await AsAsync(Roles.Admin);
        var otherManager = await App.UserAsync(Roles.Manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/admin/users/{otherManager.Id}/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/admin/users/{otherManager.Id}/appointments",
            new { serviceId = "oil", start = await App.NextSlotAsync(), plate = "4821KJH", vehicle = "Seat León" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/users/{otherManager.Id}/appointments")).StatusCode);
    }

    [MongoFact]
    public async Task No_se_reserva_a_una_cuenta_desactivada_ni_con_el_vehiculo_de_otro_cliente()
    {
        var manager = await AsAsync(Roles.Manager);
        var inactive = await App.UserAsync(active: false);
        var client = await App.UserAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"/api/admin/users/{inactive.Id}/appointments",
            new { serviceId = "oil", start = await App.NextSlotAsync(), plate = "4821KJH", vehicle = "Seat León" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync($"/api/admin/users/{client.Id}/appointments",
            new { serviceId = "oil", start = await App.NextSlotAsync(), vehicleId = "000000000000000000000000" })).StatusCode);
    }

    [MongoFact]
    public async Task Un_cliente_no_puede_usar_la_gestion_de_citas_de_otros()
    {
        var client = await App.LoginAsync(await App.UserAsync());
        var other = await App.UserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/users/{other.Id}/appointments")).StatusCode);
    }

    // ---------- Bienvenida y contraseña provisional ----------

    private async Task<(string Email, HttpClient Session, string Welcome)> CreatedAsync()
    {
        var manager = await AsAsync(Roles.Manager);
        var email = NewEmail();
        var res = await manager.PostAsync("/api/admin/users", new { name = "Lucía Prado", email, password = StrongPassword });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.True((await res.JsonAsync()).GetProperty("emailSent").GetBoolean());
        var mail = Assert.Single(App.Google.To(email));
        var session = App.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsync("/api/auth/login", new { email, password = StrongPassword })).StatusCode);
        return (email, session, mail.TokenFor("/restablecer"));
    }

    [MongoFact]
    public async Task La_cuenta_creada_recibe_un_correo_de_bienvenida_con_enlace_para_elegir_contrasena()
    {
        var manager = await AsAsync(Roles.Manager);
        var email = NewEmail();

        await manager.PostAsync("/api/admin/users", new { name = "Lucía Prado", email, password = StrongPassword });

        var mail = Assert.Single(App.Google.To(email));
        Assert.StartsWith("Tu cuenta está creada", mail.Subject);
        Assert.StartsWith("https://taller.test/restablecer?token=", mail.LinkTo("/restablecer"));
        Assert.DoesNotContain(StrongPassword, mail.Subject);
        Assert.True((await App.Data.Users.Find(u => u.Email == email).FirstAsync()).MustChangePassword);
    }

    [MongoFact]
    public async Task Con_la_contrasena_provisional_solo_se_puede_cambiarla()
    {
        var (_, session, _) = await CreatedAsync();

        var me = await session.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True((await me.JsonAsync()).GetProperty("mustChangePassword").GetBoolean());

        var blocked = await session.GetAsync("/api/appointments/mine");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password_change_required", (await blocked.JsonAsync()).GetProperty("code").GetString());
    }

    [MongoFact]
    public async Task Cambiar_la_contrasena_libera_la_cuenta_y_la_provisional_deja_de_valer()
    {
        var (email, session, _) = await CreatedAsync();
        const string Mine = "Mi-clave-nueva-77";

        Assert.Equal(HttpStatusCode.Forbidden, (await session.PostAsync("/api/auth/change-password", new { currentPassword = "otra-Clave-1234", newPassword = Mine })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostAsync("/api/auth/change-password", new { currentPassword = StrongPassword, newPassword = StrongPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostAsync("/api/auth/change-password", new { currentPassword = StrongPassword, newPassword = "corta1" })).StatusCode);

        var ok = await session.PostAsync("/api/auth/change-password", new { currentPassword = StrongPassword, newPassword = Mine });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False((await ok.JsonAsync()).GetProperty("mustChangePassword").GetBoolean());

        // La misma sesión ya puede usar la aplicación.
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/appointments/mine")).StatusCode);
        // La provisional ya no entra; la nueva sí, y sin pedir cambio.
        Assert.Equal(HttpStatusCode.Unauthorized, (await App.NewClient().PostAsync("/api/auth/login", new { email, password = StrongPassword })).StatusCode);
        var again = App.NewClient();
        var login = await again.PostAsync("/api/auth/login", new { email, password = Mine });
        Assert.False((await login.JsonAsync()).GetProperty("mustChangePassword").GetBoolean());
    }

    [MongoFact]
    public async Task El_enlace_del_correo_de_bienvenida_tambien_libera_la_cuenta()
    {
        var (email, _, token) = await CreatedAsync();
        const string Mine = "Mi-clave-nueva-77";

        var reset = await App.NewClient().PostAsync("/api/auth/reset", new { token, password = Mine });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False((await App.Data.Users.Find(u => u.Email == email).FirstAsync()).MustChangePassword);
        var login = await App.NewClient().PostAsync("/api/auth/login", new { email, password = Mine });
        Assert.False((await login.JsonAsync()).GetProperty("mustChangePassword").GetBoolean());
    }

    [MongoFact]
    public async Task Un_usuario_normal_no_tiene_que_cambiar_la_contrasena()
    {
        var client = await App.LoginAsync(await App.UserAsync());
        var me = await (await client.GetAsync("/api/auth/me")).JsonAsync();
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/appointments/mine")).StatusCode);
    }
}
