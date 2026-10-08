using System.Text.RegularExpressions;
using MimeKit;

namespace TallerChat.Api;

public static partial class Validation
{
    [GeneratedRegex(@"^[^@\s,;<>()\[\]\\""]+@[^@\s,;<>()\[\]\\""]+\.[^@\s,;<>()\[\]\\""]{2,}$")]
    private static partial Regex EmailRx();

    [GeneratedRegex(@"^[+0-9 ()-]{6,20}$")]
    private static partial Regex PhoneRx();

    [GeneratedRegex(@"^[A-Z0-9]{4,10}$")]
    private static partial Regex PlateRx();

    [GeneratedRegex(@"^[0-9]{4,10}$")]
    private static partial Regex PostalRx();

    // El VIN real excluye I, O y Q para no confundirlas con 1 y 0.
    [GeneratedRegex(@"^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex VinRx();

    /// <summary>Normaliza (recorta y pasa a minúsculas) y valida el correo.</summary>
    public static bool TryEmail(string? value, out string email)
    {
        email = (value ?? "").Trim().ToLowerInvariant();
        return email.Length <= 254 && EmailRx().IsMatch(email) && MailboxAddress.TryParse(email, out _);
    }

    public static bool TryPhone(string? value, out string? phone)
    {
        phone = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return phone is null || PhoneRx().IsMatch(phone);
    }

    /// <summary>Matrícula sin espacios ni guiones, en mayúsculas.</summary>
    public static bool TryPlate(string? value, out string plate)
    {
        plate = new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return PlateRx().IsMatch(plate);
    }

    public static bool StrongPassword(string? p) =>
        p is { Length: >= 10 and <= 100 } && p.Any(char.IsLetter) && p.Any(char.IsDigit);

    public static bool TryName(string? value, out string name)
    {
        name = (value ?? "").Trim();
        return name.Length is >= 2 and <= 80;
    }

    private static string? Clean(string? s, int max)
    {
        var v = (s ?? "").Trim();
        return v.Length == 0 ? null : v.Length <= max ? v : v[..max];
    }

    /// <summary>Dirección: todos los campos son opcionales; solo se comprueba el formato de los que vengan.</summary>
    public static bool TryAddress(AddressDto? dto, out Address? address, out string problem)
    {
        address = null;
        problem = "";
        if (dto is null) return true;

        var postal = Clean(dto.PostalCode, 10);
        if (postal is not null && !PostalRx().IsMatch(postal))
            return Fail("El código postal no es válido.", out problem);

        var a = new Address
        {
            Street = Clean(dto.Street, 120),
            PostalCode = postal,
            City = Clean(dto.City, 80),
            Province = Clean(dto.Province, 80)
        };
        address = a.IsEmpty ? null : a;
        return true;
    }

    /// <summary>Valida un vehículo y devuelve la entidad lista para guardar, conservando su id si es una edición.</summary>
    public static bool TryVehicle(VehicleRequest? req, string? keepId, out Vehicle vehicle, out string problem)
    {
        vehicle = new Vehicle();
        problem = "";
        if (req is null) return Fail("Faltan los datos del vehículo.", out problem);

        var make = (req.Make ?? "").Trim();
        var model = (req.Model ?? "").Trim();
        if (make.Length is < 1 or > 40) return Fail("Indica la marca del vehículo.", out problem);
        if (model.Length > 40) return Fail("El modelo del vehículo es demasiado largo.", out problem);
        if (!TryPlate(req.Plate, out var plate)) return Fail("La matrícula no es válida.", out problem);

        if (req.Year is { } year && (year < 1900 || year > DateTime.UtcNow.Year + 1))
            return Fail("El año del vehículo no es válido.", out problem);
        if (req.Km is { } km && (km < 0 || km > 2_000_000))
            return Fail("El kilometraje no es válido.", out problem);

        var fuel = Clean(req.Fuel, 24);
        if (fuel is not null && !Fuels.All.Contains(fuel)) return Fail("El tipo de combustible no es válido.", out problem);

        var vin = Clean(req.Vin, 17)?.ToUpperInvariant();
        if (vin is not null && !VinRx().IsMatch(vin))
            return Fail("El VIN debe tener 17 caracteres (sin las letras I, O ni Q).", out problem);

        vehicle = new Vehicle
        {
            Id = string.IsNullOrWhiteSpace(keepId) ? new Vehicle().Id : keepId!,
            Make = make, Model = model, Plate = plate, Year = req.Year, Km = req.Km, Fuel = fuel, Vin = vin
        };
        return true;
    }

    private static bool Fail(string message, out string problem) { problem = message; return false; }
}
