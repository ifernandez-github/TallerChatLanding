using System.Net;
using System.Net.Http.Json;
using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Integration;

/// <summary>Área del cliente: sus datos, su dirección y sus vehículos.</summary>
public sealed class ProfileTests(MongoFixture mongo) : IntegrationTest(mongo)
{
    private async Task<(AppUser User, HttpClient Session)> ClientAsync()
    {
        var u = await App.UserAsync();
        return (u, await App.LoginAsync(u));
    }

    private static object Car(string plate = "4821KJH", string make = "Seat", string model = "León") => new { make, model, plate, year = 2021, km = 52000, fuel = "gasolina" };

    [MongoFact]
    public async Task El_cliente_edita_sus_datos_y_su_direccion_pero_no_puede_cambiarse_rol_ni_correo()
    {
        var (user, session) = await ClientAsync();

        var res = await session.PutAsJsonAsync("/api/profile", new
        {
            name = "Ana Nueva", phone = "699111222", role = "admin", email = "otro@clientes.test",
            address = new { street = "Calle Motor 24", postalCode = "28045", city = "Madrid", province = "Madrid" }
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var saved = await App.Data.Users.Find(u => u.Id == user.Id).FirstAsync();
        Assert.Equal("Ana Nueva", saved.Name);
        Assert.Equal("699111222", saved.Phone);
        Assert.Equal("28045", saved.Address?.PostalCode);
        Assert.Equal(Roles.Client, saved.Role);   // el DTO de perfil no tiene rol: se ignora
        Assert.Equal(user.Email, saved.Email);    // ni correo
    }

    [MongoTheory]
    [InlineData("A", "600123123")]
    [InlineData("Ana", "teléfono")]
    public async Task Datos_de_perfil_invalidos_dan_400(string name, string phone)
    {
        var (_, session) = await ClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PutAsJsonAsync("/api/profile", new { name, phone })).StatusCode);
    }

    [MongoFact]
    public async Task Alta_edicion_y_baja_de_un_vehiculo()
    {
        var (user, session) = await ClientAsync();

        var add = await session.PostAsync("/api/profile/vehicles", Car("4821-kjh"));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var id = (await add.JsonAsync()).GetProperty("vehicles")[0].GetProperty("id").GetString()!;

        var edit = await session.PutAsJsonAsync($"/api/profile/vehicles/{id}", Car(model: "León FR"));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var stored = (await App.Data.Users.Find(u => u.Id == user.Id).FirstAsync()).Vehicles.Single();
        Assert.Equal("León FR", stored.Model);
        Assert.Equal("4821KJH", stored.Plate);
        Assert.Equal(id, stored.Id);

        var del = await session.DeleteAsync($"/api/profile/vehicles/{id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.Empty((await App.Data.Users.Find(u => u.Id == user.Id).FirstAsync()).Vehicles);
    }

    [MongoFact]
    public async Task No_se_admiten_dos_vehiculos_con_la_misma_matricula_ni_mas_de_diez()
    {
        var (_, session) = await ClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsync("/api/profile/vehicles", Car("1000AAA"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync("/api/profile/vehicles", Car("1000-aaa"))).StatusCode);

        for (var i = 1; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await session.PostAsync("/api/profile/vehicles", Car($"{2000 + i}BBB"))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync("/api/profile/vehicles", Car("9999ZZZ"))).StatusCode);
    }

    [MongoFact]
    public async Task Un_vehiculo_invalido_da_400_y_editar_uno_ajeno_da_404()
    {
        var (_, session) = await ClientAsync();
        var (_, other) = await ClientAsync();
        var otherCar = await (await other.PostAsync("/api/profile/vehicles", Car("5555CCC"))).JsonAsync();
        var otherCarId = otherCar.GetProperty("vehicles")[0].GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostAsync("/api/profile/vehicles", Car("!!"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.PutAsJsonAsync($"/api/profile/vehicles/{otherCarId}", Car("6666DDD"))).StatusCode);
    }

    [MongoFact]
    public async Task Preview_de_la_baja_propia_cuenta_lo_que_se_pierde()
    {
        var (user, session) = await ClientAsync();
        await session.PostAsync("/api/profile/vehicles", Car());
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, DateTime.UtcNow.AddDays(2), Statuses.Pending, bay: 1));

        var preview = await (await session.GetAsync("/api/profile/deletion")).JsonAsync();

        Assert.Equal(1, preview.GetProperty("vehicles").GetInt32());
        Assert.Equal(1, preview.GetProperty("appointments").GetInt32());
        Assert.Equal(1, preview.GetProperty("upcoming").GetInt32());
    }

    [MongoFact]
    public async Task La_baja_propia_borra_sus_citas_y_le_manda_el_aviso_de_despedida()
    {
        var (user, session) = await ClientAsync();
        await App.Data.Appointments.InsertOneAsync(UserManagementTests.Cita(user.Id, DateTime.UtcNow.AddDays(2), Statuses.Pending, bay: 1));

        var res = await session.PostAsync("/api/profile/delete", new { password = TallerApp.Password });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(0, await App.Data.Appointments.CountDocumentsAsync(a => a.UserId == user.Id));
        Assert.Single(App.Google.To(user.Email));
    }
}
