using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TallerChat.Api;

public static class Roles
{
    public const string Client = "client", Admin = "admin";
}

public static class Statuses
{
    public const string Pending = "pending", Confirmed = "confirmed", Completed = "completed", Cancelled = "cancelled";

    /// <summary>Estados que ocupan un hueco en la agenda (los que bloquean una franja).</summary>
    public static readonly string[] Active = [Pending, Confirmed];

    /// <summary>Estados que cuentan como trabajo real del taller: se usan para la ocupación del calendario,
    /// de modo que un día pasado y lleno no aparezca como libre solo porque sus citas ya están completadas.</summary>
    public static readonly string[] Booked = [Pending, Confirmed, Completed];
}

public static class TokenKinds
{
    public const string Verify = "verify", Reset = "reset";
}

public static class Fuels
{
    public const string Petrol = "gasolina", Diesel = "diesel", Hybrid = "hibrido",
        Plugin = "hibrido-enchufable", Electric = "electrico", Gas = "glp-gnc", Other = "otro";

    public static readonly string[] All = [Petrol, Diesel, Hybrid, Plugin, Electric, Gas, Other];
}

/// <summary>Dirección postal del cliente (toda opcional: el taller la usa para avisos y facturación).</summary>
public sealed class Address
{
    [BsonElement("street")] public string? Street { get; set; }
    [BsonElement("postal_code")] public string? PostalCode { get; set; }
    [BsonElement("city")] public string? City { get; set; }
    [BsonElement("province")] public string? Province { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(PostalCode)
        && string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Province);
}

/// <summary>Vehículo de un cliente. Vive dentro del documento del usuario (siempre se lee junto a él).</summary>
public sealed class Vehicle
{
    [BsonElement("id")] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonElement("make")] public string Make { get; set; } = "";
    [BsonElement("model")] public string Model { get; set; } = "";
    [BsonElement("plate")] public string Plate { get; set; } = "";
    [BsonElement("year")] public int? Year { get; set; }
    [BsonElement("km")] public int? Km { get; set; }
    [BsonElement("fuel")] public string? Fuel { get; set; }
    [BsonElement("vin")] public string? Vin { get; set; }
    [BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Descripción corta que se guarda en la cita y aparece en los correos.</summary>
    public string Describe() => $"{Make} {Model}".Trim();
}

/// <summary>Documento de taller_db.users.</summary>
[BsonIgnoreExtraElements]
public sealed class AppUser
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonElement("email")] public string Email { get; set; } = "";
    [BsonElement("name")] public string Name { get; set; } = "";
    [BsonElement("phone")] public string? Phone { get; set; }
    [BsonElement("address")] public Address? Address { get; set; }
    [BsonElement("vehicles")] public List<Vehicle> Vehicles { get; set; } = [];
    [BsonElement("password_hash")] public string PasswordHash { get; set; } = "";
    /// <summary>Hasta que no confirma su correo, la cuenta no puede iniciar sesión.</summary>
    [BsonElement("email_verified")] public bool EmailVerified { get; set; }
    /// <summary>
    /// Cambia al restablecer la contraseña y se guarda también en la cookie: así las sesiones abiertas
    /// en otros dispositivos dejan de valer. Vacío en las cuentas anteriores a esta función.
    /// </summary>
    [BsonElement("security_stamp")] public string SecurityStamp { get; set; } = "";
    [BsonElement("role")] public string Role { get; set; } = Roles.Client;
    [BsonElement("active")] public bool Active { get; set; } = true;
    [BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Documento de taller_db.auth_tokens: un enlace de un solo uso enviado por correo (verificar la cuenta o
/// restablecer la contraseña). Del token solo se guarda su hash, nunca el valor que viaja en el email.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AuthToken
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonElement("user_id"), BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    [BsonElement("kind")] public string Kind { get; set; } = "";
    [BsonElement("hash")] public string Hash { get; set; } = "";
    [BsonElement("expires_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ExpiresAt { get; set; }
    [BsonElement("used_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? UsedAt { get; set; }
    [BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Documento de taller_db.appointments. Cada cita ocupa un hueco (Start) en un elevador (Bay).</summary>
[BsonIgnoreExtraElements]
public sealed class Appointment
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonElement("user_id"), BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    [BsonElement("service_id")] public string ServiceId { get; set; } = "";
    [BsonElement("start"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime Start { get; set; }
    [BsonElement("bay")] public int Bay { get; set; }
    /// <summary>Vehículo del cliente, si la cita se hizo eligiendo uno guardado. Puede haberse borrado después.</summary>
    [BsonElement("vehicle_id")] public string? VehicleId { get; set; }
    /// <summary>Copia de los datos del vehículo en el momento de la cita (el historial no debe cambiar si luego se edita).</summary>
    [BsonElement("plate")] public string Plate { get; set; } = "";
    [BsonElement("vehicle")] public string Vehicle { get; set; } = "";
    [BsonElement("notes")] public string? Notes { get; set; }
    [BsonElement("status")] public string Status { get; set; } = Statuses.Pending;
    [BsonElement("reminder_sent")] public bool ReminderSent { get; set; }
    [BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updated_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
