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

    /// <summary>Estados que ocupan un hueco en la agenda.</summary>
    public static readonly string[] Active = [Pending, Confirmed];
}

/// <summary>Documento de taller_db.users.</summary>
[BsonIgnoreExtraElements]
public sealed class AppUser
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonElement("email")] public string Email { get; set; } = "";
    [BsonElement("name")] public string Name { get; set; } = "";
    [BsonElement("phone")] public string? Phone { get; set; }
    [BsonElement("password_hash")] public string PasswordHash { get; set; } = "";
    [BsonElement("role")] public string Role { get; set; } = Roles.Client;
    [BsonElement("active")] public bool Active { get; set; } = true;
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
    [BsonElement("plate")] public string Plate { get; set; } = "";
    [BsonElement("vehicle")] public string Vehicle { get; set; } = "";
    [BsonElement("notes")] public string? Notes { get; set; }
    [BsonElement("status")] public string Status { get; set; } = Statuses.Pending;
    [BsonElement("reminder_sent")] public bool ReminderSent { get; set; }
    [BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updated_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
