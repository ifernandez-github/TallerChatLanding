using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TallerChat.Api;

/// <summary>
/// Envía por la API de Gmail (HTTPS, puerto 443). Sirve donde el hosting bloquea los puertos SMTP (el plan gratuito de
/// Render, por ejemplo). Usa un refresh token OAuth2 con el ámbito gmail.send; el access token se cachea hasta que caduca.
/// </summary>
public sealed class GmailApiSender(HttpClient http, IOptions<SmtpOptions> options)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? accessToken;
    private DateTime expiresAt;

    public async Task SendAsync(MimeMessage msg, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await msg.WriteToAsync(ms, ct);
        var raw = Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send")
        {
            Content = JsonContent.Create(new { raw })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode(); // el cuerpo no se registra: puede traer la dirección del destinatario
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (accessToken is not null && DateTime.UtcNow < expiresAt) return accessToken;
            var g = options.Value.Gmail;
            using var res = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = g.ClientId,
                ["client_secret"] = g.ClientSecret,
                ["refresh_token"] = g.RefreshToken,
                ["grant_type"] = "refresh_token",
            }), ct);
            res.EnsureSuccessStatusCode();
            var t = await res.Content.ReadFromJsonAsync<TokenResponse>(ct) ?? throw new InvalidOperationException("Respuesta de token vacía.");
            accessToken = t.AccessToken;
            expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, t.ExpiresIn - 60));
            return accessToken;
        }
        finally { gate.Release(); }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
