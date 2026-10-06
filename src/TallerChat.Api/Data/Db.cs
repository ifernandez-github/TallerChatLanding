using MongoDB.Bson;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Colecciones de la aplicación (usuarios y citas) y sus índices. Singleton: reutiliza el IMongoDatabase.</summary>
public sealed class Db(IMongoDatabase database)
{
    public IMongoCollection<AppUser> Users { get; } = database.GetCollection<AppUser>("users");
    public IMongoCollection<Appointment> Appointments { get; } = database.GetCollection<Appointment>("appointments");

    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        // Un correo = una cuenta (el correo se guarda normalizado en minúsculas).
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<AppUser>(
            Builders<AppUser>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions<AppUser> { Unique = true, Name = "users_email_unique" }), cancellationToken: ct);

        // Un hueco + un elevador solo puede estar ocupado por UNA cita activa. Es lo que evita las reservas dobles
        // aunque dos clientes pulsen "confirmar" a la vez: el segundo insert falla y se prueba el siguiente elevador.
        var active = new BsonDocument("status", new BsonDocument("$in", new BsonArray(Statuses.Active)));
        await Appointments.Indexes.CreateOneAsync(new CreateIndexModel<Appointment>(
            Builders<Appointment>.IndexKeys.Ascending(a => a.Start).Ascending(a => a.Bay),
            new CreateIndexOptions<Appointment>
            {
                Unique = true,
                Name = "appointments_slot_bay_unique",
                PartialFilterExpression = new BsonDocumentFilterDefinition<Appointment>(active)
            }), cancellationToken: ct);

        await Appointments.Indexes.CreateOneAsync(new CreateIndexModel<Appointment>(
            Builders<Appointment>.IndexKeys.Ascending(a => a.UserId).Descending(a => a.Start),
            new CreateIndexOptions<Appointment> { Name = "appointments_user_start" }), cancellationToken: ct);
    }
}
