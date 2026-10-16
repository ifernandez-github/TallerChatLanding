using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Estadísticas del panel: acceso por rol, agrupación por periodo y filtro por categoría.</summary>
public sealed class StatsTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private static DateTime MidMonth(int monthsAgo)
    {
        var d = DateTime.UtcNow.AddMonths(-monthsAgo);
        return new DateTime(d.Year, d.Month, 15, 12, 0, 0, DateTimeKind.Utc);
    }

    private async Task NewClientAsync(DateTime createdAt)
    {
        var u = await App.UserAsync();
        await App.Data.Users.UpdateOneAsync(x => x.Id == u.Id, Builders<AppUser>.Update.Set(x => x.CreatedAt, createdAt));
    }

    private int minutes;

    private Task AppointmentAsync(string service, string status, DateTime createdAt) =>
        App.Data.Appointments.InsertOneAsync(new Appointment
        {
            UserId = ObjectId.GenerateNewId().ToString(), ServiceId = service, Status = status,
            Start = DateTime.UtcNow.AddDays(30).AddMinutes(Interlocked.Increment(ref minutes)), Bay = 1,
            Plate = "1234ABC", Vehicle = "Seat León", CreatedAt = createdAt
        });

    public static TheoryData<string> Rutas => new()
    {
        "/api/admin/stats/clients",
        "/api/admin/stats/appointments"
    };

    [MongoTheory]
    [MemberData(nameof(Rutas))]
    public async Task Las_estadisticas_son_para_gestor_y_administrador_pero_no_para_clientes_ni_anonimos(string url)
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));
        var client = await App.LoginAsync(await App.UserAsync(Roles.Client));

        Assert.Equal(HttpStatusCode.Unauthorized, (await App.NewClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
    }

    [MongoTheory]
    [MemberData(nameof(Rutas))]
    public async Task Los_parametros_no_validos_dan_400(string url)
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url + "?granularity=semana")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url + "?periods=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url + "?periods=500")).StatusCode);
    }

    [MongoFact]
    public async Task Los_clientes_nuevos_se_cuentan_por_mes_y_solo_los_de_rol_cliente()
    {
        await NewClientAsync(MidMonth(0));
        await NewClientAsync(MidMonth(0));
        await NewClientAsync(MidMonth(1));
        await NewClientAsync(MidMonth(30)); // fuera de los últimos 12 meses
        await App.UserAsync(Roles.Manager); // el personal no es un cliente nuevo
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));

        var stats = await (await admin.GetAsync("/api/admin/stats/clients?granularity=month&periods=12")).JsonAsync();
        var points = stats.GetProperty("points");

        Assert.Equal("month", stats.GetProperty("granularity").GetString());
        Assert.Equal(12, points.GetArrayLength());
        Assert.Equal(3, stats.GetProperty("total").GetInt32());
        Assert.Equal(2, points[11].GetProperty("count").GetInt32());
        Assert.Equal(1, points[10].GetProperty("count").GetInt32());
        Assert.Equal(0, points[0].GetProperty("count").GetInt32());
    }

    [MongoFact]
    public async Task Los_clientes_nuevos_se_pueden_agrupar_por_trimestre_y_por_año()
    {
        await NewClientAsync(MidMonth(0));
        await NewClientAsync(MidMonth(14));
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));

        var quarters = await (await manager.GetAsync("/api/admin/stats/clients?granularity=quarter")).JsonAsync();
        var years = await (await manager.GetAsync("/api/admin/stats/clients?granularity=year&periods=3")).JsonAsync();

        Assert.Equal(8, quarters.GetProperty("points").GetArrayLength());
        Assert.Equal(2, quarters.GetProperty("total").GetInt32());
        Assert.Equal(3, years.GetProperty("points").GetArrayLength());
        Assert.Equal(2, years.GetProperty("total").GetInt32());
    }

    [MongoFact]
    public async Task Las_citas_se_cuentan_por_estado_y_se_pueden_filtrar_por_categoria()
    {
        await AppointmentAsync("brake", Statuses.Pending, MidMonth(0));
        await AppointmentAsync("brake", Statuses.Confirmed, MidMonth(0));
        await AppointmentAsync("brake", Statuses.Completed, MidMonth(1));
        await AppointmentAsync("oil", Statuses.Cancelled, MidMonth(0));
        await AppointmentAsync("oil", Statuses.Completed, MidMonth(0));
        var manager = await App.LoginAsync(await App.UserAsync(Roles.Manager));

        var all = await (await manager.GetAsync("/api/admin/stats/appointments")).JsonAsync();
        var totals = all.GetProperty("totals");
        Assert.Equal(5, totals.GetProperty("created").GetInt32());
        Assert.Equal(1, totals.GetProperty("pending").GetInt32());
        Assert.Equal(1, totals.GetProperty("confirmed").GetInt32());
        Assert.Equal(2, totals.GetProperty("closed").GetInt32());
        Assert.Equal(1, totals.GetProperty("cancelled").GetInt32());
        Assert.Equal(Catalog.Services.Count, all.GetProperty("categories").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, all.GetProperty("category").ValueKind);

        var brakes = await (await manager.GetAsync("/api/admin/stats/appointments?category=brake")).JsonAsync();
        var points = brakes.GetProperty("points");
        Assert.Equal("brake", brakes.GetProperty("category").GetString());
        Assert.Equal(3, brakes.GetProperty("totals").GetProperty("created").GetInt32());
        Assert.Equal(2, points[11].GetProperty("created").GetInt32());
        Assert.Equal(1, points[10].GetProperty("closed").GetInt32());
    }

    [MongoFact]
    public async Task Una_categoria_desconocida_se_rechaza()
    {
        var admin = await App.LoginAsync(await App.UserAsync(Roles.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/stats/appointments?category=inventada")).StatusCode);
    }
}
