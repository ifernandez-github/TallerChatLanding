using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>
/// Gestión de cuentas desde el panel: el administrador gestiona a todos; el gestor solo a los clientes.
/// Reglas de seguridad: nadie se quita su propio rol, el último administrador no se pierde y las sesiones caen al instante.
/// </summary>
public sealed class UserManagementTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private async Task<(HttpClient Admin, HttpClient Manager, AppUser AdminUser, AppUser ManagerUser, AppUser Client)> ElencoAsync()
    {
        var adminUser = await App.UserAsync(Roles.Admin);
        var managerUser = await App.UserAsync(Roles.Manager);
        var client = await App.UserAsync();
        return (await App.LoginAsync(adminUser), await App.LoginAsync(managerUser), adminUser, managerUser, client);
    }

    private static async Task<List<string>> RolesListedAsync(HttpClient c)
    {
        var users = await (await c.GetAsync("/api/admin/users")).JsonAsync();
        return users.EnumerateArray().Select(u => u.GetProperty("role").GetString()!).ToList();
    }

    // ---------- Qué ve cada uno ----------

    [MongoFact]
    public async Task El_gestor_solo_ve_clientes_y_el_administrador_ve_todos_los_roles()
    {
        var (admin, manager, _, _, _) = await ElencoAsync();

        var seenByManager = await RolesListedAsync(manager);
        var seenByAdmin = await RolesListedAsync(admin);

        Assert.NotEmpty(seenByManager);
        Assert.All(seenByManager, r => Assert.Equal(Roles.Client, r));
        Assert.Contains(Roles.Admin, seenByAdmin);
        Assert.Contains(Roles.Manager, seenByAdmin);
        Assert.Contains(Roles.Client, seenByAdmin);
    }

    [MongoFact]
    public async Task El_resumen_cuenta_solo_clientes()
    {
        var (admin, _, _, _, _) = await ElencoAsync();
        var summary = await (await admin.GetAsync("/api/admin/summary")).JsonAsync();
        Assert.Equal(1, summary.GetProperty("users").GetInt32()); // un único cliente, aunque haya administrador y gestor
    }

    // ---------- Editar fichas ----------

    [MongoFact]
    public async Task El_gestor_edita_la_ficha_de_un_cliente()
    {
        var (_, manager, _, _, client) = await ElencoAsync();

        var res = await manager.PutAsJsonAsync($"/api/admin/users/{client.Id}",
            new { name = "Ana Editada", phone = "699000111", address = new { street = "Calle Nueva 1", postalCode = "28001", city = "Madrid" } });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var saved = await App.Data.Users.Find(u => u.Id == client.Id).FirstAsync();
        Assert.Equal("Ana Editada", saved.Name);
        Assert.Equal("699000111", saved.Phone);
        Assert.Equal("Madrid", saved.Address?.City);
        Assert.Equal(Roles.Client, saved.Role);
    }

    [MongoFact]
    public async Task El_gestor_no_puede_tocar_a_administradores_ni_a_otros_gestores()
    {
        var (_, manager, adminUser, _, _) = await ElencoAsync();
        var otherManager = await App.UserAsync(Roles.Manager);

        foreach (var target in new[] { adminUser, otherManager })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/admin/users/{target.Id}", new { name = "Hackeado" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/admin/users/{target.Id}/active", new { active = false })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/admin/users/{target.Id}/deletion")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/admin/users/{target.Id}/delete", new { confirmEmail = target.Email })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/admin/users/{target.Id}/vehicles", new { make = "Seat", model = "X", plate = "1234ABC" })).StatusCode);
        }
        var untouched = await App.Data.Users.Find(u => u.Id == adminUser.Id).FirstAsync();
        Assert.True(untouched.Active);
        Assert.NotEqual("Hackeado", untouched.Name);
    }

    [MongoFact]
    public async Task El_gestor_no_puede_repartir_roles_ni_siquiera_a_un_cliente()
    {
        var (_, manager, _, _, client) = await ElencoAsync();

        var res = await manager.PutAsJsonAsync($"/api/admin/users/{client.Id}", new { name = client.Name, role = Roles.Admin });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(Roles.Client, (await App.Data.Users.Find(u => u.Id == client.Id).FirstAsync()).Role);
    }

    [MongoFact]
    public async Task El_administrador_cambia_el_rol_y_la_sesion_del_afectado_se_revalida_al_instante()
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        var clientSession = await App.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await clientSession.GetAsync("/api/admin/summary")).StatusCode);

        var res = await admin.PutAsJsonAsync($"/api/admin/users/{client.Id}", new { name = client.Name, role = Roles.Manager });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // La cookie antigua llevaba el rol «client»: ya no coincide con la base de datos y la sesión cae.
        Assert.Equal(HttpStatusCode.Unauthorized, (await clientSession.GetAsync("/api/auth/me")).StatusCode);
        // Al entrar de nuevo ya es gestor.
        var again = await App.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/admin/summary")).StatusCode);
    }

    [MongoFact]
    public async Task Un_rol_inventado_se_rechaza()
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        var res = await admin.PutAsJsonAsync($"/api/admin/users/{client.Id}", new { name = client.Name, role = "superusuario" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [MongoFact]
    public async Task Un_administrador_no_puede_quitarse_a_si_mismo_el_rol()
    {
        var (admin, _, adminUser, _, _) = await ElencoAsync();
        var res = await admin.PutAsJsonAsync($"/api/admin/users/{adminUser.Id}", new { name = adminUser.Name, role = Roles.Client });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(Roles.Admin, (await App.Data.Users.Find(u => u.Id == adminUser.Id).FirstAsync()).Role);
    }

    [MongoFact]
    public async Task Cambiar_el_correo_a_uno_ya_usado_da_409()
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        var other = await App.UserAsync();

        var res = await admin.PutAsJsonAsync($"/api/admin/users/{client.Id}", new { name = client.Name, email = other.Email });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [MongoTheory]
    [InlineData("nombre")]
    [InlineData("correo")]
    [InlineData("telefono")]
    [InlineData("postal")]
    public async Task Datos_invalidos_en_la_ficha_dan_400(string campo)
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        object body = campo switch
        {
            "nombre" => new { name = "A" },
            "correo" => new { name = client.Name, email = "no-es-correo" },
            "telefono" => new { name = client.Name, phone = "abc" },
            _ => new { name = client.Name, address = new { postalCode = "XX" } }
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{client.Id}", body)).StatusCode);
    }

    [MongoFact]
    public async Task Un_id_que_no_es_valido_o_no_existe_da_404()
    {
        var (admin, _, _, _, _) = await ElencoAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/users/no-es-un-objectid", new { name = "Ana" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/users/665f1c2e8f1b2c0012345678", new { name = "Ana" })).StatusCode);
    }

    // ---------- Activar / desactivar ----------

    [MongoFact]
    public async Task Desactivar_una_cuenta_cierra_su_sesion_de_inmediato_y_reactivarla_deja_volver_a_entrar()
    {
        var (_, manager, _, _, client) = await ElencoAsync();
        var session = await App.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me")).StatusCode);

        var off = await manager.PostAsync($"/api/admin/users/{client.Id}/active", new { active = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await App.NewClient().PostAsync("/api/auth/login", new { email = client.Email, password = TallerApp.Password })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/admin/users/{client.Id}/active", new { active = true })).StatusCode);
        var back = await App.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await back.GetAsync("/api/auth/me")).StatusCode);
    }

    [MongoFact]
    public async Task Active_sin_valor_da_400_y_nadie_se_desactiva_a_si_mismo()
    {
        var (admin, _, adminUser, _, client) = await ElencoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/admin/users/{client.Id}/active", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/admin/users/{adminUser.Id}/active", new { active = false })).StatusCode);
    }

    [MongoFact]
    public async Task Un_administrador_puede_desactivar_a_otro_si_queda_alguno_con_el_que_entrar()
    {
        var (admin, _, _, _, _) = await ElencoAsync();
        var other = await App.UserAsync(Roles.Admin);

        var res = await admin.PostAsync($"/api/admin/users/{other.Id}/active", new { active = false });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Borrar cuentas ----------

    [MongoFact]
    public async Task El_borrado_muestra_antes_lo_que_se_perdera()
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        await App.Data.Users.UpdateOneAsync(u => u.Id == client.Id, Builders<AppUser>.Update.Push(u => u.Vehicles, new Vehicle { Make = "Seat", Model = "Ibiza", Plate = "1111AAA" }));
        await App.Data.Appointments.InsertManyAsync(
        [
            Cita(client.Id, DateTime.UtcNow.AddDays(3), Statuses.Confirmed, bay: 1),
            Cita(client.Id, DateTime.UtcNow.AddDays(-3), Statuses.Completed, bay: 1)
        ]);

        var preview = await (await admin.GetAsync($"/api/admin/users/{client.Id}/deletion")).JsonAsync();

        Assert.Equal(1, preview.GetProperty("vehicles").GetInt32());
        Assert.Equal(2, preview.GetProperty("appointments").GetInt32());
        Assert.Equal(1, preview.GetProperty("upcoming").GetInt32());
    }

    [MongoFact]
    public async Task Borrar_exige_el_correo_exacto_y_se_lleva_citas_y_tokens_y_avisa_al_cliente()
    {
        var (admin, _, _, _, client) = await ElencoAsync();
        await App.Data.Appointments.InsertOneAsync(Cita(client.Id, DateTime.UtcNow.AddDays(3), Statuses.Pending, bay: 1));
        await App.Data.Tokens.InsertOneAsync(new AuthToken { UserId = client.Id, Kind = TokenKinds.Reset, Hash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddHours(1) });
        var session = await App.LoginAsync(client);

        var wrong = await admin.PostAsync($"/api/admin/users/{client.Id}/delete", new { confirmEmail = "otro@clientes.test" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(1, await App.Data.Users.CountDocumentsAsync(u => u.Id == client.Id));

        var ok = await admin.PostAsync($"/api/admin/users/{client.Id}/delete", new { confirmEmail = client.Email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(0, await App.Data.Users.CountDocumentsAsync(u => u.Id == client.Id));
        Assert.Equal(0, await App.Data.Appointments.CountDocumentsAsync(a => a.UserId == client.Id));
        Assert.Equal(0, await App.Data.Tokens.CountDocumentsAsync(t => t.UserId == client.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        var mail = Assert.Single(App.Google.To(client.Email));
        Assert.Contains("baja", mail.Subject + mail.Text, StringComparison.OrdinalIgnoreCase);
    }

    [MongoFact]
    public async Task El_gestor_puede_borrar_clientes_pero_no_a_si_mismo_ni_a_nadie_mas()
    {
        var (_, manager, _, managerUser, client) = await ElencoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync($"/api/admin/users/{managerUser.Id}/delete", new { confirmEmail = managerUser.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/admin/users/{client.Id}/delete", new { confirmEmail = client.Email })).StatusCode);
    }

    [MongoFact]
    public async Task Un_administrador_no_se_borra_a_si_mismo_desde_administracion()
    {
        var (admin, _, adminUser, _, _) = await ElencoAsync();
        var res = await admin.PostAsync($"/api/admin/users/{adminUser.Id}/delete", new { confirmEmail = adminUser.Email });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Baja de la propia cuenta ----------

    [MongoFact]
    public async Task La_baja_propia_exige_la_contrasena_y_cierra_la_sesion()
    {
        var client = await App.UserAsync();
        var session = await App.LoginAsync(client);

        var noPassword = await session.PostAsync("/api/profile/delete", new { password = "" });
        var wrong = await session.PostAsync("/api/profile/delete", new { password = "Equivocada-123" });
        Assert.Equal(HttpStatusCode.Forbidden, noPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(1, await App.Data.Users.CountDocumentsAsync(u => u.Id == client.Id)); // la cookie sola nunca basta

        var ok = await session.PostAsync("/api/profile/delete", new { password = TallerApp.Password });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(0, await App.Data.Users.CountDocumentsAsync(u => u.Id == client.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
    }

    [MongoFact]
    public async Task El_unico_administrador_no_puede_darse_de_baja()
    {
        var adminUser = await App.UserAsync(Roles.Admin);
        var session = await App.LoginAsync(adminUser);

        var res = await session.PostAsync("/api/profile/delete", new { password = TallerApp.Password });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(1, await App.Data.Users.CountDocumentsAsync(u => u.Id == adminUser.Id));
    }

    [MongoFact]
    public async Task Con_dos_administradores_uno_si_puede_darse_de_baja_pero_el_segundo_ya_no()
    {
        var a = await App.UserAsync(Roles.Admin);
        var b = await App.UserAsync(Roles.Admin);
        var sa = await App.LoginAsync(a);
        var sb = await App.LoginAsync(b);

        Assert.Equal(HttpStatusCode.OK, (await sa.PostAsync("/api/profile/delete", new { password = TallerApp.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await sb.PostAsync("/api/profile/delete", new { password = TallerApp.Password })).StatusCode);
    }

    [MongoFact]
    public async Task Un_administrador_sin_confirmar_o_desactivado_no_cuenta_como_otro_administrador()
    {
        var real = await App.UserAsync(Roles.Admin);
        await App.UserAsync(Roles.Admin, verified: false);
        await App.UserAsync(Roles.Admin, active: false);
        var session = await App.LoginAsync(real);

        var res = await session.PostAsync("/api/profile/delete", new { password = TallerApp.Password });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode); // los otros dos no superan el login: no son una salida
    }

    [MongoFact]
    public async Task Las_bajas_simultaneas_de_dos_administradores_nunca_dejan_el_taller_sin_ninguno()
    {
        var a = await App.UserAsync(Roles.Admin);
        var b = await App.UserAsync(Roles.Admin);
        var sa = await App.LoginAsync(a);
        var sb = await App.LoginAsync(b);

        var results = await Task.WhenAll(
            sa.PostAsync("/api/profile/delete", new { password = TallerApp.Password }),
            sb.PostAsync("/api/profile/delete", new { password = TallerApp.Password }));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await App.Data.Users.CountDocumentsAsync(u => u.Role == Roles.Admin));
    }

    // ---------- Vehículos desde administración ----------

    [MongoFact]
    public async Task El_gestor_gestiona_los_vehiculos_de_un_cliente()
    {
        var (_, manager, _, _, client) = await ElencoAsync();

        var add = await manager.PostAsync($"/api/admin/users/{client.Id}/vehicles",
            new { make = "Seat", model = "León", plate = "4821-kjh", year = 2021, fuel = "gasolina" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var vehicle = (await add.JsonAsync()).GetProperty("vehicles")[0];
        var vehicleId = vehicle.GetProperty("id").GetString();
        Assert.Equal("4821KJH", vehicle.GetProperty("plate").GetString());

        var edit = await manager.PutAsJsonAsync($"/api/admin/users/{client.Id}/vehicles/{vehicleId}",
            new { make = "Seat", model = "León FR", plate = "4821KJH" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var del = await manager.DeleteAsync($"/api/admin/users/{client.Id}/vehicles/{vehicleId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.Empty((await App.Data.Users.Find(u => u.Id == client.Id).FirstAsync()).Vehicles);
    }

    internal static Appointment Cita(string userId, DateTime start, string status, int bay) => new()
    {
        UserId = userId, ServiceId = "oil", Start = start, Bay = bay, Plate = "4821KJH", Vehicle = "Seat León", Status = status
    };
}
