using System.Net;
using MongoDB.Bson;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Arranque: configuración obligatoria, índices, administrador inicial, datos de ejemplo y recordatorios.</summary>
public sealed class StartupTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done, int seconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var value = await read();
        while (!done(value) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            value = await read();
        }
        return value;
    }

    // ---------- Configuración obligatoria ----------

    [MongoTheory]
    [InlineData("Mongo:ConnectionString", "Mongo:ConnectionString")]
    [InlineData("Gemini:ApiKey", "Gemini:ApiKey")]
    public void Sin_la_configuracion_obligatoria_la_API_se_niega_a_arrancar(string key, string expectedInMessage)
    {
        var app = Start(new TallerAppOptions { Settings = new Dictionary<string, string> { [key] = "" } });

        var ex = Assert.ThrowsAny<Exception>(() => app.CreateClient());

        Assert.Contains(expectedInMessage, ex.ToString());
    }

    // ---------- Índices ----------

    [MongoFact]
    public async Task Al_arrancar_crea_los_indices_de_usuarios_citas_y_tokens()
    {
        _ = App.Server;

        var users = await IndexNamesAsync("users");
        var appointments = await IndexNamesAsync("appointments");
        var tokens = await IndexNamesAsync("auth_tokens");

        Assert.Contains("users_email_unique", users);
        Assert.Contains("appointments_slot_bay_unique", appointments);
        Assert.Contains("appointments_user_start", appointments);
        Assert.Contains("tokens_hash_unique", tokens);
        Assert.Contains("tokens_ttl", tokens);
    }

    private async Task<List<string>> IndexNamesAsync(string collection)
    {
        var cursor = await App.Database.GetCollection<BsonDocument>(collection).Indexes.ListAsync();
        return (await cursor.ToListAsync()).Select(i => i["name"].AsString).ToList();
    }

    [MongoFact]
    public async Task El_indice_unico_impide_dos_cuentas_con_el_mismo_correo()
    {
        _ = App.Server;
        var a = await App.UserAsync(email: "repetido@clientes.test");

        var ex = await Assert.ThrowsAsync<MongoWriteException>(() => App.UserAsync(email: "repetido@clientes.test"));

        Assert.Equal(ServerErrorCategory.DuplicateKey, ex.WriteError.Category);
        Assert.NotNull(a);
    }

    [MongoFact]
    public async Task El_indice_parcial_bloquea_dos_citas_activas_en_el_mismo_hueco_y_elevador_pero_no_las_canceladas()
    {
        _ = App.Server;
        var at = DateTime.UtcNow.AddDays(3);
        var user = await App.UserAsync();
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, at, Statuses.Pending, bay: 1));

        var ex = await Assert.ThrowsAsync<MongoWriteException>(() =>
            App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, at, Statuses.Confirmed, bay: 1)));
        Assert.Equal(ServerErrorCategory.DuplicateKey, ex.WriteError.Category);

        // Cancelada o completada no ocupa el hueco; otro elevador tampoco choca.
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, at, Statuses.Cancelled, bay: 1));
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, at, Statuses.Pending, bay: 2));
    }

    // ---------- Administrador inicial ----------

    [MongoFact]
    public async Task El_administrador_inicial_se_crea_verificado_y_puede_entrar()
    {
        var app = Start(new TallerAppOptions { AdminEmail = "Jefe@Taller-Real.test", AdminPassword = "Admin-Inicial-123" });

        var session = app.NewClient();
        var login = await session.PostAsync("/api/auth/login", new { email = "jefe@taller-real.test", password = "Admin-Inicial-123" });

        Assert.True(login.IsSuccessStatusCode);
        var admin = await app.Data.Users.Find(u => u.Email == "jefe@taller-real.test").FirstAsync();
        Assert.Equal(Roles.Admin, admin.Role);
        Assert.True(admin.EmailVerified);
        Assert.StartsWith("v1.", admin.PasswordHash);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/admin/summary")).StatusCode);
    }

    [MongoFact]
    public async Task Con_credenciales_de_administrador_invalidas_no_se_crea_ninguno()
    {
        var app = Start(new TallerAppOptions { AdminEmail = "jefe@taller-real.test", AdminPassword = "corta" });
        _ = app.Server;

        Assert.Equal(0, await app.Data.Users.CountDocumentsAsync(_ => true));
    }

    [MongoFact]
    public async Task Las_cuentas_anteriores_a_la_verificacion_por_correo_se_dan_por_verificadas()
    {
        var app = Start(new TallerAppOptions
        {
            Arrange = db => db.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
            {
                ["email"] = "antigua@clientes.test", ["name"] = "Cuenta antigua", ["role"] = "client", ["active"] = true,
                ["password_hash"] = PasswordHasher.Hash("Taller-Test-123") // sin email_verified ni security_stamp
            })
        });
        _ = app.Server;

        var old = await app.Data.Users.Find(u => u.Email == "antigua@clientes.test").FirstAsync();

        Assert.True(old.EmailVerified);
        var login = await app.NewClient().PostAsync("/api/auth/login", new { email = "antigua@clientes.test", password = "Taller-Test-123" });
        Assert.True(login.IsSuccessStatusCode);
    }

    // ---------- Datos de ejemplo ----------

    [MongoFact]
    public async Task Los_datos_de_ejemplo_crean_clientes_demo_taller_con_citas_y_nunca_reciben_correo()
    {
        var app = Start(new TallerAppOptions { DemoSeed = true });
        _ = app.Server;

        var demo = await app.Data.Users.Find(u => u.Email.EndsWith("@demo.taller")).ToListAsync();
        Assert.Equal(4, demo.Count);
        Assert.All(demo, u => Assert.True(u.EmailVerified));
        Assert.True(await app.Data.Appointments.CountDocumentsAsync(_ => true) >= 10);

        // El recordatorio de las citas de ejemplo cercanas se "omite": ni correo ni reintentos eternos.
        await Task.Delay(1500);
        Assert.Empty(app.Emails);
    }

    // ---------- Recordatorios ----------

    private static Func<IMongoDatabase, Task> ArrangeReminders(string email, string soon, string later, string cancelled, string done) => async db =>
    {
        var data = new Db(db);
        var user = new AppUser
        {
            Email = email, Name = "Ana Recordatorio", Role = Roles.Client, EmailVerified = true,
            PasswordHash = "x", SecurityStamp = "s"
        };
        await data.Users.InsertOneAsync(user);
        Appointment A(string id, DateTime start, string status, bool sent) => new()
        {
            Id = id, UserId = user.Id, ServiceId = "oil", Start = start, Bay = 1, Plate = "4821KJH", Vehicle = "Seat León",
            Status = status, ReminderSent = sent
        };
        await data.Appointments.InsertManyAsync(
        [
            A(soon, DateTime.UtcNow.AddHours(5), Statuses.Confirmed, sent: false),      // dentro de 24 h: sí
            A(later, DateTime.UtcNow.AddHours(30), Statuses.Confirmed, sent: false),    // fuera de las 24 h: no
            A(cancelled, DateTime.UtcNow.AddHours(6), Statuses.Cancelled, sent: false), // cancelada: no
            A(done, DateTime.UtcNow.AddHours(7), Statuses.Confirmed, sent: true)        // ya avisada: no
        ]);
    };

    [MongoFact]
    public async Task El_recordatorio_sale_para_las_citas_de_las_proximas_24_horas_y_solo_una_vez()
    {
        var email = $"recuerdame-{Guid.NewGuid():N}@clientes.test";
        var ids = Enumerable.Range(0, 4).Select(_ => ObjectId.GenerateNewId().ToString()).ToArray();
        var app = Start(new TallerAppOptions { Arrange = ArrangeReminders(email, ids[0], ids[1], ids[2], ids[3]) });
        _ = app.Server;

        var mails = await EventuallyAsync(() => Task.FromResult(app.Google.To(email)), m => m.Count > 0);

        var mail = Assert.Single(mails);
        Assert.StartsWith("Recordatorio", mail.Subject);
        Assert.Equal(["cita.ics"], mail.Attachments);

        await Task.Delay(500);
        Assert.Single(app.Google.To(email)); // los otros tres no se avisan
        var sent = await app.Data.Appointments.Find(a => a.Id == ids[0]).FirstAsync();
        Assert.True(sent.ReminderSent);
        Assert.False((await app.Data.Appointments.Find(a => a.Id == ids[1]).FirstAsync()).ReminderSent);
        Assert.False((await app.Data.Appointments.Find(a => a.Id == ids[2]).FirstAsync()).ReminderSent);
    }

    [MongoFact]
    public async Task Si_el_envio_del_recordatorio_falla_la_cita_queda_pendiente_de_avisar()
    {
        var email = $"recuerdame-{Guid.NewGuid():N}@clientes.test";
        var ids = Enumerable.Range(0, 4).Select(_ => ObjectId.GenerateNewId().ToString()).ToArray();
        var app = Start(new TallerAppOptions { Arrange = ArrangeReminders(email, ids[0], ids[1], ids[2], ids[3]) });
        app.Google.FailSends = true;
        _ = app.Server;

        // Hay un intento de envío (falla) y la cita vuelve a «no avisada» para reintentarla en la siguiente pasada.
        await EventuallyAsync(() => Task.FromResult(app.Google.Requests.Count(r => r.Uri.Host == "gmail.googleapis.com")), n => n > 0);
        var appt = await EventuallyAsync(() => app.Data.Appointments.Find(a => a.Id == ids[0]).FirstAsync(), a => !a.ReminderSent);

        Assert.False(appt.ReminderSent);
        Assert.Empty(app.Emails);
    }

    [MongoFact]
    public async Task Sin_correo_configurado_no_hay_recordatorios()
    {
        var email = $"recuerdame-{Guid.NewGuid():N}@clientes.test";
        var ids = Enumerable.Range(0, 4).Select(_ => ObjectId.GenerateNewId().ToString()).ToArray();
        var app = Start(new TallerAppOptions { EmailEnabled = false, Arrange = ArrangeReminders(email, ids[0], ids[1], ids[2], ids[3]) });
        _ = app.Server;

        await Task.Delay(1500);

        Assert.Empty(app.Google.Requests);
        Assert.False((await app.Data.Appointments.Find(a => a.Id == ids[0]).FirstAsync()).ReminderSent);
    }
}
