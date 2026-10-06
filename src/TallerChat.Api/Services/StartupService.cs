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
        await db.EnsureIndexesAsync(ct);
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
        }
        else if (!string.IsNullOrWhiteSpace(seed.AdminEmail) || !string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            log.LogWarning("Seed:AdminEmail o Seed:AdminPassword no son válidos (la contraseña necesita 10+ caracteres con letras y números). No se creó el administrador.");
        }

        if (seed.Demo) await SeedDemoAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task SeedDemoAsync(CancellationToken ct)
    {
        var people = new[]
        {
            (Name: "Lucía Martín", Email: "lucia.martin@demo.taller", Phone: "600111222"),
            (Name: "Javier Ortega", Email: "javier.ortega@demo.taller", Phone: "600333444"),
            (Name: "Marta Ruiz", Email: "marta.ruiz@demo.taller", Phone: "600555666"),
            (Name: "Carlos Beltrán", Email: "carlos.beltran@demo.taller", Phone: "600777888")
        };
        if (await db.Users.CountDocumentsAsync(u => u.Email == people[0].Email, cancellationToken: ct) > 0) return;

        var hash = PasswordHasher.Hash(DemoPassword);
        var users = people.Select(p => new AppUser { Name = p.Name, Email = p.Email, Phone = p.Phone, PasswordHash = hash }).ToList();
        await db.Users.InsertManyAsync(users, cancellationToken: ct);

        var today = booking.TodayLocal();
        // (cliente, servicio, días desde hoy, nº de franja, elevador, matrícula, vehículo, estado, notas)
        var plan = new (int U, string Svc, int Day, int Slot, int Bay, string Plate, string Car, string Status, string? Notes)[]
        {
            (0, "oil",        -21, 2, 1, "4821KJH", "Seat León 1.5 TSI",        Statuses.Completed, "Revisión de los 30.000 km"),
            (1, "brake",      -14, 0, 1, "7310LMN", "Renault Clio dCi",         Statuses.Completed, "Chirrido al frenar"),
            (2, "snow",        -9, 3, 2, "2194GTR", "Toyota Corolla Hybrid",    Statuses.Completed, null),
            (3, "scan",        -6, 1, 1, "9057BXC", "Ford Focus 1.0 EcoBoost",  Statuses.Cancelled, "Testigo de motor encendido"),
            (0, "wheel",       -3, 4, 2, "4821KJH", "Seat León 1.5 TSI",        Statuses.Completed, "Equilibrado y alineado"),
            (1, "belt",         1, 1, 1, "7310LMN", "Renault Clio dCi",         Statuses.Confirmed, "Cambio de distribución a los 120.000 km"),
            (2, "check-list",   2, 0, 1, "2194GTR", "Toyota Corolla Hybrid",    Statuses.Confirmed, "Pre-ITV"),
            (3, "bolt",         3, 2, 1, "6648DFS", "Tesla Model 3",            Statuses.Pending,   "Revisión del circuito de alta tensión"),
            (0, "oil",          5, 3, 2, "4821KJH", "Seat León 1.5 TSI",        Statuses.Pending,   null),
            (3, "scan",         5, 3, 1, "9057BXC", "Ford Focus 1.0 EcoBoost",  Statuses.Confirmed, "Testigo de motor encendido"),
            (1, "snow",         8, 1, 2, "7310LMN", "Renault Clio dCi",         Statuses.Pending,   "Carga de gas y filtro de habitáculo")
        };

        foreach (var p in plan)
        {
            var start = booking.SlotAt(today.AddDays(p.Day), p.Slot);
            try
            {
                await db.Appointments.InsertOneAsync(new Appointment
                {
                    UserId = users[p.U].Id, ServiceId = p.Svc, Start = start, Bay = p.Bay, Plate = p.Plate, Vehicle = p.Car,
                    Notes = p.Notes, Status = p.Status, ReminderSent = p.Day < 0
                }, cancellationToken: ct);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
        }
        log.LogInformation("Datos de ejemplo creados (clientes @demo.taller, contraseña de ejemplo documentada en el README).");
    }
}
