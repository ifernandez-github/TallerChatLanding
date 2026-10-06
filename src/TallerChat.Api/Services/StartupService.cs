using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Al arrancar: crea los índices, el administrador inicial y (opcional) los datos de ejemplo.</summary>
public sealed class StartupService(Db db, IOptions<SeedOptions> seedOptions, BookingService booking,
    ILogger<StartupService> log) : IHostedService
{
    private const string DemoPassword = "Demo1234!";

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await db.EnsureIndexesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Arrancar igualmente, pero avisando: sin el índice único la protección contra dobles reservas no actúa.
            log.LogCritical("No se pudieron crear los índices de MongoDB ({Type}). Revisa que el servidor admita " +
                "índices parciales con $in (MongoDB 7.0+) y que no exista ya otro índice con el mismo nombre y " +
                "distintas opciones. Mientras tanto, la protección contra reservas duplicadas NO está activa.",
                ex.GetType().Name);
        }

        var seed = seedOptions.Value;

        if (Validation.TryEmail(seed.AdminEmail, out var adminEmail) && Validation.StrongPassword(seed.AdminPassword))
        {
            if (await db.Users.CountDocumentsAsync(u => u.Email == adminEmail, cancellationToken: ct) == 0)
            {
                await db.Users.InsertOneAsync(new AppUser
                {
                    Email = adminEmail, Name = seed.AdminName, Role = Roles.Admin,
                    PasswordHash = PasswordHasher.Hash(seed.AdminPassword)
                }, cancellationToken: ct);
                log.LogInformation("Usuario administrador creado.");
            }
            else
            {
                log.LogInformation("El usuario administrador ya existe; no se ha vuelto a crear.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(seed.AdminEmail) || !string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            log.LogWarning("Seed:AdminEmail o Seed:AdminPassword no son válidos (la contraseña necesita 10+ caracteres con letras y números). No se creó el administrador.");
        }
        else
        {
            log.LogWarning("Seed:AdminEmail y Seed:AdminPassword están vacíos: no hay administrador. Configúralos en user-secrets.");
        }

        if (seed.Demo) await SeedDemoAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed record DemoPerson(string Name, string Email, string Phone, Address Address, Vehicle[] Vehicles);

    private async Task SeedDemoAsync(CancellationToken ct)
    {
        var people = new DemoPerson[]
        {
            new("Lucía Martín", "lucia.martin@demo.taller", "600111222",
                new Address { Street = "Calle Embajadores 112, 3º B", PostalCode = "28045", City = "Madrid", Province = "Madrid" },
                [new Vehicle { Make = "Seat", Model = "León 1.5 TSI", Plate = "4821KJH", Year = 2021, Km = 52400, Fuel = Fuels.Petrol, Vin = "VSSZZZ5FZMR123456" }]),
            new("Javier Ortega", "javier.ortega@demo.taller", "600333444",
                new Address { Street = "Avenida de Córdoba 18", PostalCode = "28026", City = "Madrid", Province = "Madrid" },
                [new Vehicle { Make = "Renault", Model = "Clio dCi", Plate = "7310LMN", Year = 2018, Km = 118900, Fuel = Fuels.Diesel }]),
            new("Marta Ruiz", "marta.ruiz@demo.taller", "600555666",
                new Address { Street = "Calle Alcalá 310", PostalCode = "28027", City = "Madrid", Province = "Madrid" },
                [new Vehicle { Make = "Toyota", Model = "Corolla Hybrid", Plate = "2194GTR", Year = 2022, Km = 38100, Fuel = Fuels.Hybrid },
                 new Vehicle { Make = "Dacia", Model = "Sandero", Plate = "5512HBD", Year = 2016, Km = 142300, Fuel = Fuels.Petrol }]),
            new("Carlos Beltrán", "carlos.beltran@demo.taller", "600777888",
                new Address { Street = "Paseo de Santa María de la Cabeza 44", PostalCode = "28045", City = "Madrid", Province = "Madrid" },
                [new Vehicle { Make = "Ford", Model = "Focus 1.0 EcoBoost", Plate = "9057BXC", Year = 2019, Km = 86500, Fuel = Fuels.Petrol },
                 new Vehicle { Make = "Tesla", Model = "Model 3", Plate = "6648DFS", Year = 2023, Km = 24700, Fuel = Fuels.Electric }])
        };
        if (await db.Users.CountDocumentsAsync(u => u.Email == people[0].Email, cancellationToken: ct) > 0) return;

        var hash = PasswordHasher.Hash(DemoPassword);
        var users = people.Select(p => new AppUser
        {
            Name = p.Name, Email = p.Email, Phone = p.Phone, Address = p.Address,
            Vehicles = p.Vehicles.ToList(), PasswordHash = hash
        }).ToList();
        await db.Users.InsertManyAsync(users, cancellationToken: ct);

        var today = booking.TodayLocal();
        // (cliente, vehículo del cliente, servicio, días desde hoy, nº de franja, elevador, estado, notas)
        var plan = new (int U, int V, string Svc, int Day, int Slot, int Bay, string Status, string? Notes)[]
        {
            (0, 0, "oil",        -21, 2, 1, Statuses.Completed, "Revisión de los 30.000 km"),
            (1, 0, "brake",      -14, 0, 1, Statuses.Completed, "Chirrido al frenar"),
            (2, 0, "snow",        -9, 3, 2, Statuses.Completed, null),
            (3, 0, "scan",        -6, 1, 1, Statuses.Cancelled, "Testigo de motor encendido"),
            (0, 0, "wheel",       -3, 4, 2, Statuses.Completed, "Equilibrado y alineado"),
            (2, 1, "check-list",  -2, 2, 1, Statuses.Completed, "Pre-ITV del Sandero"),
            (1, 0, "belt",         1, 1, 1, Statuses.Confirmed, "Cambio de distribución a los 120.000 km"),
            (2, 0, "check-list",   2, 0, 1, Statuses.Confirmed, "Pre-ITV"),
            (3, 1, "bolt",         3, 2, 1, Statuses.Pending,   "Revisión del circuito de alta tensión"),
            (0, 0, "oil",          5, 3, 2, Statuses.Pending,   null),
            (3, 0, "scan",         5, 3, 1, Statuses.Confirmed, "Testigo de motor encendido"),
            (2, 1, "wheel",        6, 2, 2, Statuses.Pending,   "Cambio de los cuatro neumáticos"),
            (1, 0, "snow",         8, 1, 2, Statuses.Pending,   "Carga de gas y filtro de habitáculo")
        };

        foreach (var p in plan)
        {
            var user = users[p.U];
            var car = user.Vehicles[p.V];
            var start = booking.SlotAt(today.AddDays(p.Day), p.Slot);
            try
            {
                await db.Appointments.InsertOneAsync(new Appointment
                {
                    UserId = user.Id, ServiceId = p.Svc, Start = start, Bay = p.Bay,
                    VehicleId = car.Id, Plate = car.Plate, Vehicle = car.Describe(),
                    Notes = p.Notes, Status = p.Status, ReminderSent = p.Day < 0
                }, cancellationToken: ct);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { }
        }
        log.LogInformation("Datos de ejemplo creados (clientes @demo.taller, contraseña de ejemplo documentada en el README).");
    }
}
