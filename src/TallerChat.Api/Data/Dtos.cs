namespace TallerChat.Api;

public sealed record UserDto(string Id, string Name, string Email, string? Phone, string Role);
public sealed record RegisterRequest(string? Name, string? Email, string? Phone, string? Password);
public sealed record LoginRequest(string? Email, string? Password);

public sealed record SlotDto(DateTime Start, string Time, int Free, bool Available);
public sealed record AvailabilityDto(string Date, bool Closed, List<SlotDto> Slots);

public sealed record BookRequest(string? ServiceId, DateTime? Start, string? Plate, string? Vehicle, string? Notes);
public sealed record AppointmentDto(string Id, string ServiceId, string ServiceName, DateTime Start, string Plate,
    string Vehicle, string? Notes, string Status, bool CanCancel);
public sealed record BookResponse(AppointmentDto Appointment, bool EmailSent);

public sealed record StatusRequest(string? Status);
public sealed record ActiveRequest(bool? Active);
public sealed record AdminAppointmentDto(string Id, string UserName, string UserEmail, string? UserPhone, string ServiceId,
    string ServiceName, DateTime Start, int Bay, string Plate, string Vehicle, string? Notes, string Status);
public sealed record AdminUserDto(string Id, string Name, string Email, string? Phone, string Role, bool Active,
    DateTime CreatedAt, int Appointments);
public sealed record SummaryDto(int Pending, int Today, int Users);

public static class ApiResults
{
    public static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
