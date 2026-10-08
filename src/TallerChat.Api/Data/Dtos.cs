namespace TallerChat.Api;

// ---------- Usuarios ----------
public sealed record AddressDto(string? Street, string? PostalCode, string? City, string? Province);
public sealed record VehicleDto(string Id, string Make, string Model, string Plate, int? Year, int? Km, string? Fuel, string? Vin);
public sealed record UserDto(string Id, string Name, string Email, string? Phone, string Role, AddressDto? Address, List<VehicleDto> Vehicles);

public sealed record RegisterRequest(string? Name, string? Email, string? EmailConfirm, string? Phone, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record AccountEmailRequest(string? Email);
public sealed record TokenRequest(string? Token);
public sealed record ResetRequest(string? Token, string? Password);
public sealed record RegisterResponse(bool Registered, bool EmailSent, bool SignedIn, string Email);
public sealed record ProfileRequest(string? Name, string? Phone, AddressDto? Address);
public sealed record VehicleRequest(string? Make, string? Model, string? Plate, int? Year, int? Km, string? Fuel, string? Vin);
public sealed record AdminUserRequest(string? Name, string? Email, string? Phone, AddressDto? Address, string? Role);

// ---------- Baja de cuenta ----------
/// <summary>Lo que desaparece al borrar una cuenta. Se enseña ANTES de borrar, en el aviso de confirmación.</summary>
public sealed record DeletionSummary(int Vehicles, int Appointments, int Upcoming);
/// <summary>Baja de la propia cuenta: se pide la contraseña, porque una sesión robada no debe bastar.</summary>
public sealed record DeleteAccountRequest(string? Password);
/// <summary>Baja desde administración: hay que teclear el correo de la cuenta, para no borrar la fila de al lado.</summary>
public sealed record AdminDeleteUserRequest(string? ConfirmEmail);

// ---------- Agenda ----------
public sealed record SlotDto(DateTime Start, string Time, int Free, bool Available);
public sealed record AvailabilityDto(string Date, bool Closed, List<SlotDto> Slots);
public sealed record FirstSlotDto(bool Found, string? Date, DateTime? Start, string? Time);

/// <summary>Ocupación de un día para el calendario de administración.</summary>
public sealed record CalendarDayDto(string Date, bool Closed, bool Past, int Booked, int Capacity, int Pending, string Status);
public sealed record CalendarDto(string Month, List<CalendarDayDto> Days);

// ---------- Citas ----------
public sealed record BookRequest(string? ServiceId, DateTime? Start, string? VehicleId, string? Plate, string? Vehicle, string? Notes);
public sealed record AppointmentDto(string Id, string ServiceId, string ServiceName, DateTime Start, string Plate,
    string Vehicle, string? Notes, string Status, bool CanCancel);
public sealed record BookResponse(AppointmentDto Appointment, bool EmailSent);

public sealed record StatusRequest(string? Status);
public sealed record ActiveRequest(bool? Active);
public sealed record AdminAppointmentDto(string Id, string UserId, string UserName, string UserEmail, string? UserPhone,
    string ServiceId, string ServiceName, DateTime Start, int Bay, string Plate, string Vehicle, string? Notes, string Status);
public sealed record AdminUserDto(string Id, string Name, string Email, string? Phone, string Role, bool Active,
    bool EmailVerified, DateTime CreatedAt, int Appointments, AddressDto? Address, List<VehicleDto> Vehicles);
public sealed record SummaryDto(int Pending, int Today, int Users);

public static class ApiResults
{
    /// <summary>
    /// Respuesta de error. <paramref name="code"/> permite a la web distinguir casos que necesitan una acción
    /// concreta (por ejemplo, ofrecer reenviar el correo de confirmación) sin tener que mirar el texto.
    /// </summary>
    public static IResult Error(int status, string message, string? code = null) =>
        Results.Json(new { error = message, code }, statusCode: status);
}

/// <summary>Conversión de entidades a DTO en un solo sitio, para que todas las respuestas sean coherentes.</summary>
public static class Mappers
{
    public static AddressDto? ToDto(this Address? a) =>
        a is null || a.IsEmpty ? null : new AddressDto(a.Street, a.PostalCode, a.City, a.Province);

    public static VehicleDto ToDto(this Vehicle v) => new(v.Id, v.Make, v.Model, v.Plate, v.Year, v.Km, v.Fuel, v.Vin);

    public static UserDto ToDto(this AppUser u) =>
        new(u.Id, u.Name, u.Email, u.Phone, u.Role, u.Address.ToDto(), u.Vehicles.Select(v => v.ToDto()).ToList());

    public static AdminUserDto ToAdminDto(this AppUser u, int appointments) =>
        new(u.Id, u.Name, u.Email, u.Phone, u.Role, u.Active, u.EmailVerified, u.CreatedAt, appointments,
            u.Address.ToDto(), u.Vehicles.Select(v => v.ToDto()).ToList());
}
