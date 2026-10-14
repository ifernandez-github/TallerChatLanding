using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Reserva, cancelación, agenda y avisos por correo (al cliente y a administración/gestores).</summary>
public sealed class AppointmentTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private static readonly BookingService Calendar = TestDb.Booking();

    private static Task<HttpResponseMessage> Book(HttpClient c, DateTime slot, string plate = "4821KJH", string vehicle = "Seat León 1.5",
        string service = "oil", string? notes = null, string? vehicleId = null) =>
        c.PostAsync("/api/appointments", new { serviceId = service, start = slot, plate, vehicle, notes, vehicleId });

    private async Task<(AppUser User, HttpClient Session)> ClientAsync()
    {
        var u = await App.UserAsync();
        return (u, await App.LoginAsync(u));
    }

    private static async Task<string> IdOf(HttpResponseMessage res) => (await res.JsonAsync()).GetProperty("appointment").GetProperty("id").GetString()!;

    // ---------- Disponibilidad pública ----------

    [MongoFact]
    public async Task El_catalogo_de_servicios_es_publico()
    {
        var services = await (await App.NewClient().GetAsync("/api/appointments/services")).JsonAsync();
        Assert.Equal(Catalog.Services.Count, services.GetArrayLength());
    }

    [MongoFact]
    public async Task La_disponibilidad_valida_la_fecha_y_el_rango_reservable()
    {
        var anon = App.NewClient();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/appointments/availability?date=ayer")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync($"/api/appointments/availability?date={today.AddDays(-3):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync($"/api/appointments/availability?date={today.AddDays(90):yyyy-MM-dd}")).StatusCode);
    }

    [MongoFact]
    public async Task El_domingo_figura_como_cerrado_y_sin_franjas()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (day.DayOfWeek != DayOfWeek.Sunday) day = day.AddDays(1);

        var availability = await (await App.NewClient().GetAsync($"/api/appointments/availability?date={day:yyyy-MM-dd}")).JsonAsync();

        Assert.True(availability.GetProperty("closed").GetBoolean());
        Assert.Equal(0, availability.GetProperty("slots").GetArrayLength());
    }

    [MongoFact]
    public async Task Primer_hueco_libre_devuelve_una_franja_valida_con_la_antelacion_minima()
    {
        var first = await (await App.NewClient().GetAsync("/api/appointments/first-available")).JsonAsync();

        Assert.True(first.GetProperty("found").GetBoolean());
        Assert.True(first.GetProperty("start").GetDateTime() >= DateTime.UtcNow.AddHours(2));
    }

    // ---------- Reservar ----------

    [MongoFact]
    public async Task Reservar_crea_la_cita_pendiente_guarda_el_vehiculo_y_avisa_al_cliente()
    {
        var (user, session) = await ClientAsync();
        var slot = await App.NextSlotAsync();

        var res = await Book(session, slot, plate: "4821-kjh", notes: "  Chirrido al frenar  ");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.JsonAsync();
        Assert.True(body.GetProperty("emailSent").GetBoolean());
        var appt = body.GetProperty("appointment");
        Assert.Equal("pending", appt.GetProperty("status").GetString());
        Assert.Equal("4821KJH", appt.GetProperty("plate").GetString());
        Assert.Equal("Chirrido al frenar", appt.GetProperty("notes").GetString());
        Assert.True(appt.GetProperty("canCancel").GetBoolean());

        // El vehículo queda en la ficha del cliente.
        var saved = await App.Data.Users.Find(u => u.Id == user.Id).FirstAsync();
        Assert.Equal("4821KJH", Assert.Single(saved.Vehicles).Plate);

        var mail = Assert.Single(App.Google.To(user.Email));
        Assert.StartsWith("Hemos recibido tu solicitud", mail.Subject);
        Assert.Empty(mail.Attachments); // solo confirmada/recordatorio llevan el .ics
    }

    [MongoFact]
    public async Task Reservar_con_un_vehiculo_guardado_copia_sus_datos_en_la_cita()
    {
        var (user, session) = await ClientAsync();
        var vehicle = new Vehicle { Make = "Renault", Model = "Clio dCi", Plate = "7310LMN" };
        await App.Data.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<AppUser>.Update.Push(u => u.Vehicles, vehicle));

        var res = await Book(session, await App.NextSlotAsync(), plate: "", vehicle: "", vehicleId: vehicle.Id);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var stored = await App.Data.Appointments.Find(a => a.UserId == user.Id).FirstAsync();
        Assert.Equal("7310LMN", stored.Plate);
        Assert.Equal("Renault Clio dCi", stored.Vehicle);
        Assert.Equal(vehicle.Id, stored.VehicleId);

        // Borrar el vehículo después no toca el historial de la cita.
        await session.DeleteAsync($"/api/profile/vehicles/{vehicle.Id}");
        var after = await App.Data.Appointments.Find(a => a.Id == stored.Id).FirstAsync();
        Assert.Equal("7310LMN", after.Plate);
    }

    [MongoFact]
    public async Task Reservar_sin_sesion_da_401()
    {
        var res = await Book(App.NewClient(), await App.NextSlotAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [MongoFact]
    public async Task Reservar_con_datos_invalidos_da_400_y_no_ocupa_hueco()
    {
        var (_, session) = await ClientAsync();
        var slot = await App.NextSlotAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, slot, service: "no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, slot, plate: "!!")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, slot, vehicle: "X")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, slot, notes: new string('n', 501))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, slot, vehicleId: "665f1c2e8f1b2c0012345678")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostAsync("/api/appointments", new { serviceId = "oil", plate = "4821KJH", vehicle = "Seat León" })).StatusCode);
        Assert.Equal(0, await App.Data.Appointments.CountDocumentsAsync(_ => true));
    }

    [MongoFact]
    public async Task Una_hora_que_no_es_el_inicio_de_una_franja_se_rechaza()
    {
        var (_, session) = await ClientAsync();
        var slot = await App.NextSlotAsync();

        var res = await Book(session, slot.AddMinutes(17));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("franja", (await res.JsonAsync()).GetProperty("error").GetString());
    }

    [MongoFact]
    public async Task No_se_puede_reservar_mas_alla_del_maximo_de_dias()
    {
        var (_, session) = await ClientAsync();
        var far = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        while (Calendar.Hours(far.DayOfWeek) is null) far = far.AddDays(1);

        var res = await Book(session, Calendar.SlotsUtc(far).First());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [MongoFact]
    public async Task No_se_puede_reservar_en_el_pasado()
    {
        var (_, session) = await ClientAsync();
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        while (Calendar.Hours(past.DayOfWeek) is null) past = past.AddDays(-1);

        Assert.Equal(HttpStatusCode.BadRequest, (await Book(session, Calendar.SlotsUtc(past).First())).StatusCode);
    }

    // ---------- Doble reserva ----------

    [MongoFact]
    public async Task Un_hueco_admite_tantas_citas_como_elevadores_y_la_siguiente_da_409()
    {
        var slot = await App.NextSlotAsync(); // Booking:Bays = 2
        var results = new List<HttpStatusCode>();
        foreach (var _ in Enumerable.Range(0, 3))
        {
            var (_, session) = await ClientAsync();
            results.Add((await Book(session, slot)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.Conflict], results);
        var bays = await App.Data.Appointments.Find(a => a.Start == slot).Project(a => a.Bay).ToListAsync();
        Assert.Equal([1, 2], bays.Order());
    }

    [MongoFact]
    public async Task Reservas_simultaneas_del_mismo_hueco_nunca_superan_los_elevadores()
    {
        var slot = await App.NextSlotAsync();
        var sessions = new List<HttpClient>();
        foreach (var _ in Enumerable.Range(0, 8)) sessions.Add((await ClientAsync()).Session);

        var results = await Task.WhenAll(sessions.Select(s => Book(s, slot)));

        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(6, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2, await App.Data.Appointments.CountDocumentsAsync(a => a.Start == slot));
    }

    [MongoFact]
    public async Task Un_cliente_no_puede_tener_dos_citas_a_la_misma_hora()
    {
        var (_, session) = await ClientAsync();
        var slot = await App.NextSlotAsync();

        Assert.Equal(HttpStatusCode.Created, (await Book(session, slot)).StatusCode);
        var second = await Book(session, slot, plate: "1111AAA", vehicle: "Dacia Sandero");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("a esa hora", (await second.JsonAsync()).GetProperty("error").GetString());
    }

    [MongoFact]
    public async Task Un_cliente_tiene_un_maximo_de_citas_activas()
    {
        var (_, session) = await ClientAsync();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await Book(session, await App.NextSlotAsync())).StatusCode);

        var sixth = await Book(session, await App.NextSlotAsync());

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
    }

    [MongoFact]
    public async Task Cancelar_libera_el_hueco_para_otro_cliente()
    {
        var slot = await App.NextSlotAsync();
        var (_, a) = await ClientAsync();
        var (_, b) = await ClientAsync();
        var (_, c) = await ClientAsync();
        var first = await Book(a, slot);
        await Book(b, slot);
        Assert.Equal(HttpStatusCode.Conflict, (await Book(c, slot)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await a.PostAsync($"/api/appointments/{await IdOf(first)}/cancel")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await Book(c, slot)).StatusCode);
    }

    // ---------- Mis citas y cancelar ----------

    [MongoFact]
    public async Task Mis_citas_solo_devuelve_las_propias()
    {
        var (_, mine) = await ClientAsync();
        var (_, theirs) = await ClientAsync();
        await Book(mine, await App.NextSlotAsync());
        await Book(theirs, await App.NextSlotAsync());

        var list = await (await mine.GetAsync("/api/appointments/mine")).JsonAsync();

        Assert.Equal(1, list.GetArrayLength());
    }

    [MongoFact]
    public async Task Cancelar_la_propia_cita_avisa_al_cliente_y_no_se_puede_cancelar_dos_veces()
    {
        var (user, session) = await ClientAsync();
        var id = await IdOf(await Book(session, await App.NextSlotAsync()));
        App.Google.ClearEmails();

        var res = await session.PostAsync($"/api/appointments/{id}/cancel");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("cancelled", (await res.JsonAsync()).GetProperty("appointment").GetProperty("status").GetString());
        Assert.StartsWith("Cita cancelada", Assert.Single(App.Google.To(user.Email)).Subject);
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync($"/api/appointments/{id}/cancel")).StatusCode);
    }

    [MongoFact]
    public async Task No_se_puede_cancelar_la_cita_de_otro_cliente()
    {
        var (_, owner) = await ClientAsync();
        var (_, intruder) = await ClientAsync();
        var id = await IdOf(await Book(owner, await App.NextSlotAsync()));

        var res = await intruder.PostAsync($"/api/appointments/{id}/cancel");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(Statuses.Pending, (await App.Data.Appointments.Find(a => a.Id == id).FirstAsync()).Status);
    }

    [MongoFact]
    public async Task Cancelar_un_id_que_no_es_valido_da_404()
    {
        var (_, session) = await ClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await session.PostAsync("/api/appointments/no-es-un-id/cancel")).StatusCode);
    }

    [MongoFact]
    public async Task No_se_puede_cancelar_una_cita_que_ya_paso()
    {
        var (user, session) = await ClientAsync();
        var past = UserManagementTests.Cita(user.Id, DateTime.UtcNow.AddDays(-1), Statuses.Confirmed, bay: 1);
        await App.Data.Appointments.InsertOneAsync(past);

        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync($"/api/appointments/{past.Id}/cancel")).StatusCode);
    }

    // ---------- Avisos a administración y gestores ----------

    [MongoFact]
    public async Task Una_solicitud_avisa_a_administradores_y_gestores_activos_sin_repetir_buzon()
    {
        var app = Start(new TallerAppOptions { PlaceholderDomain = "torque.es" });
        var admin = await app.UserAsync(Roles.Admin, email: "admin@torque.es");          // dominio de ejemplo -> va a Smtp:From
        var admin2 = await app.UserAsync(Roles.Admin, email: "otro-admin@torque.es");   // mismo buzón real: no se repite
        var realAdmin = await app.UserAsync(Roles.Admin, email: "jefa@taller-real.test");
        var manager = await app.UserAsync(Roles.Manager, email: "gestor@taller-real.test");
        var inactiveManager = await app.UserAsync(Roles.Manager, active: false, email: "baja@taller-real.test");
        var customer = await app.UserAsync(name: "Ana Pérez");
        var session = await app.LoginAsync(customer);

        var res = await Book(session, await app.NextSlotAsync(), notes: "Ruido al girar");
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var from = Assert.Single(app.Google.To("taller@correo-real.test"));
        Assert.StartsWith("Nueva solicitud de cita", from.Subject);
        Assert.Contains("Ana Pérez", from.Text);
        Assert.Contains("Ruido al girar", from.Text);
        Assert.StartsWith("Nueva solicitud de cita", Assert.Single(app.Google.To("jefa@taller-real.test")).Subject);
        Assert.StartsWith("Nueva solicitud de cita", Assert.Single(app.Google.To("gestor@taller-real.test")).Subject);
        Assert.Empty(app.Google.To("baja@taller-real.test"));
        Assert.Empty(app.Google.To(admin.Email));
        Assert.Empty(app.Google.To(admin2.Email));
        // El cliente recibe SU correo, no el de administración.
        Assert.StartsWith("Hemos recibido", Assert.Single(app.Google.To(customer.Email)).Subject);
    }

    [MongoFact]
    public async Task Cancelar_tambien_avisa_a_administracion()
    {
        var manager = await App.UserAsync(Roles.Manager);
        var (_, session) = await ClientAsync();
        var id = await IdOf(await Book(session, await App.NextSlotAsync()));
        App.Google.ClearEmails();

        await session.PostAsync($"/api/appointments/{id}/cancel");

        var notice = Assert.Single(App.Google.To(manager.Email));
        Assert.StartsWith("Cita cancelada", notice.Subject);
    }

    [MongoFact]
    public async Task Si_Gmail_cae_la_reserva_y_la_cancelacion_siguen_funcionando()
    {
        var (_, session) = await ClientAsync();
        App.Google.FailSends = true;

        var res = await Book(session, await App.NextSlotAsync());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.JsonAsync();
        Assert.False(body.GetProperty("emailSent").GetBoolean());
        var cancel = await session.PostAsync($"/api/appointments/{body.GetProperty("appointment").GetProperty("id").GetString()}/cancel");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.False((await cancel.JsonAsync()).GetProperty("emailSent").GetBoolean());
    }

    [MongoFact]
    public async Task Sin_correo_configurado_se_reserva_igualmente_sin_enviar_nada()
    {
        var app = Start(new TallerAppOptions { Environment = "Development", EmailEnabled = false });
        var user = await app.UserAsync();
        var session = await app.LoginAsync(user);

        var res = await Book(session, await app.NextSlotAsync());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.False((await res.JsonAsync()).GetProperty("emailSent").GetBoolean());
        Assert.Empty(app.Emails);
    }

    [MongoFact]
    public async Task Las_cuentas_de_ejemplo_demo_taller_nunca_reciben_correo()
    {
        var demo = await App.UserAsync(email: "lucia@demo.taller");
        var session = await App.LoginAsync(demo);

        var res = await Book(session, await App.NextSlotAsync());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.False((await res.JsonAsync()).GetProperty("emailSent").GetBoolean());
        Assert.Empty(App.Google.To(demo.Email));
    }

    // ---------- Enviarme mis citas ----------

    [MongoFact]
    public async Task Enviarme_mis_citas_va_siempre_al_correo_de_la_cuenta()
    {
        var (user, session) = await ClientAsync();
        await Book(session, await App.NextSlotAsync());
        await Book(session, await App.NextSlotAsync(), plate: "1111AAA", vehicle: "Dacia Sandero");
        App.Google.ClearEmails();

        // Aunque el cliente intente colar un destinatario, la API lo ignora.
        var res = await session.PostAsync("/api/appointments/mine/email", new { to = "otro@victima.test", email = "otro@victima.test" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var mail = Assert.Single(App.Emails);
        Assert.Equal(user.Email, mail.To);
        Assert.StartsWith("Tus próximas citas", mail.Subject);
        Assert.Contains("https://taller.test/mi-cuenta", mail.Text);
    }

    [MongoFact]
    public async Task Enviarme_mis_citas_sin_citas_proximas_da_400()
    {
        var (_, session) = await ClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostAsync("/api/appointments/mine/email")).StatusCode);
    }

    // ---------- Panel: estados de la cita ----------

    private async Task<(AppUser Client, Appointment Appt)> PendingAsync(TallerApp? app = null)
    {
        app ??= App;
        var client = await app.UserAsync();
        var appt = UserManagementTests.Cita(client.Id, await app.NextSlotAsync(), Statuses.Pending, bay: 1);
        await app.Data.Appointments.InsertOneAsync(appt);
        return (client, appt);
    }

    [MongoFact]
    public async Task El_gestor_confirma_y_el_cliente_recibe_el_correo_con_el_ics()
    {
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));
        var (client, appt) = await PendingAsync();

        var res = await manager.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.JsonAsync()).GetProperty("emailSent").GetBoolean());
        var mail = Assert.Single(App.Google.To(client.Email));
        Assert.StartsWith("Cita confirmada", mail.Subject);
        Assert.Equal(["cita.ics"], mail.Attachments);
    }

    [MongoFact]
    public async Task Flujo_completo_pendiente_confirmada_completada()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var (_, appt) = await PendingAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = "confirmed" })).StatusCode);
        var done = await admin.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = "completed" });

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Equal("completed", (await done.JsonAsync()).GetProperty("appointment").GetProperty("status").GetString());
    }

    [MongoTheory]
    [InlineData("pending", "completed", HttpStatusCode.Conflict)]   // hay que confirmar antes
    [InlineData("pending", "pending", HttpStatusCode.BadRequest)]   // nadie "vuelve" a pendiente
    [InlineData("confirmed", "pending", HttpStatusCode.BadRequest)]
    [InlineData("completed", "cancelled", HttpStatusCode.Conflict)] // lo completado no se cancela
    [InlineData("cancelled", "confirmed", HttpStatusCode.Conflict)] // lo cancelado no resucita
    [InlineData("pending", "inventado", HttpStatusCode.BadRequest)]
    public async Task Transiciones_de_estado_no_permitidas(string from, string to, HttpStatusCode expected)
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var client = await App.UserAsync();
        var appt = UserManagementTests.Cita(client.Id, await App.NextSlotAsync(), from, bay: 1);
        await App.Data.Appointments.InsertOneAsync(appt);

        var res = await admin.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = to });

        Assert.Equal(expected, res.StatusCode);
        Assert.Equal(from, (await App.Data.Appointments.Find(a => a.Id == appt.Id).FirstAsync()).Status);
    }

    [MongoFact]
    public async Task No_se_puede_confirmar_una_cita_que_ya_paso()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var client = await App.UserAsync();
        var past = UserManagementTests.Cita(client.Id, DateTime.UtcNow.AddHours(-5), Statuses.Pending, bay: 1);
        await App.Data.Appointments.InsertOneAsync(past);

        var res = await admin.PostAsync($"/api/admin/appointments/{past.Id}/status", new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [MongoFact]
    public async Task Cancelar_desde_el_panel_avisa_al_cliente_y_un_id_malo_da_404()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var (client, appt) = await PendingAsync();

        var res = await admin.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = "cancelled" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith("Cita cancelada", Assert.Single(App.Google.To(client.Email)).Subject);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/appointments/xx/status", new { status = "confirmed" })).StatusCode);
    }

    [MongoFact]
    public async Task Completar_no_envia_correo()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var client = await App.UserAsync();
        var appt = UserManagementTests.Cita(client.Id, await App.NextSlotAsync(), Statuses.Confirmed, bay: 1);
        await App.Data.Appointments.InsertOneAsync(appt);

        await admin.PostAsync($"/api/admin/appointments/{appt.Id}/status", new { status = "completed" });

        Assert.Empty(App.Emails);
    }

    // ---------- Panel: agenda y calendario ----------

    [MongoFact]
    public async Task La_agenda_del_dia_incluye_los_datos_del_cliente_y_valida_la_fecha()
    {
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));
        var client = await App.UserAsync(name: "Ana Pérez");
        var slot = await App.NextSlotAsync();
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(client.Id, slot, Statuses.Pending, bay: 1));

        var day = Calendar.LocalDateIso(slot);
        var agenda = await (await manager.GetAsync($"/api/admin/appointments?date={day}")).JsonAsync();

        var item = agenda.EnumerateArray().Single(a => a.GetProperty("userEmail").GetString() == client.Email);
        Assert.Equal("Ana Pérez", item.GetProperty("userName").GetString());
        Assert.Equal("Mantenimiento y aceite", item.GetProperty("serviceName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/admin/appointments?date=hoy")).StatusCode);
    }

    [MongoFact]
    public async Task Un_cliente_borrado_no_rompe_la_agenda()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var slot = await App.NextSlotAsync();
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita("665f1c2e8f1b2c0012345678", slot, Statuses.Pending, bay: 1));

        var agenda = await (await admin.GetAsync($"/api/admin/appointments?date={Calendar.LocalDateIso(slot)}")).JsonAsync();

        Assert.Contains(agenda.EnumerateArray(), a => a.GetProperty("userName").GetString() == "(usuario eliminado)");
    }

    [MongoFact]
    public async Task El_calendario_marca_dias_libres_parciales_y_completos_y_valida_el_mes()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var client = await App.UserAsync();
        var slot = await App.NextSlotAsync();
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(client.Id, slot, Statuses.Pending, bay: 1));
        var day = Calendar.LocalDateIso(slot);

        var calendar = await (await admin.GetAsync($"/api/admin/calendar?month={day[..7]}")).JsonAsync();

        var entry = calendar.GetProperty("days").EnumerateArray().Single(d => d.GetProperty("date").GetString() == day);
        Assert.Equal("partial", entry.GetProperty("status").GetString());
        Assert.Equal(1, entry.GetProperty("booked").GetInt32());
        Assert.Equal(1, entry.GetProperty("pending").GetInt32());
        Assert.Contains(calendar.GetProperty("days").EnumerateArray(), d => d.GetProperty("status").GetString() == "closed"); // domingos
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/calendar?month=2026-13")).StatusCode);
    }

    [MongoFact]
    public async Task Las_pendientes_futuras_se_listan_con_pending_true()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var client = await App.UserAsync();
        await App.Data.Appointments.InsertManyAsync(
        [
            UserManagementTests.Cita(client.Id, await App.NextSlotAsync(), Statuses.Pending, bay: 1),
            UserManagementTests.Cita(client.Id, await App.NextSlotAsync(), Statuses.Confirmed, bay: 1)
        ]);

        var pending = await (await admin.GetAsync("/api/admin/appointments?pending=true")).JsonAsync();
        var summary = await (await admin.GetAsync("/api/admin/summary")).JsonAsync();

        Assert.Equal(1, pending.GetArrayLength());
        Assert.Equal(1, summary.GetProperty("pending").GetInt32());
    }
}
