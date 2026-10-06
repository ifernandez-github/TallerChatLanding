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
}
