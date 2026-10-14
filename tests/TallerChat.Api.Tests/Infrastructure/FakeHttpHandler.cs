using System.Net;
using System.Text;

namespace TallerChat.Api.Tests.Infrastructure;

/// <summary>Una petición saliente que la API intentó hacer (a Gemini, a Gmail...), con su cuerpo ya leído.</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Doble de HttpMessageHandler: responde con lo que diga <see cref="Responder"/> y guarda las peticiones recibidas.
/// Sustituye a Gemini y a Gmail/OAuth sin salir nunca a internet.
/// </summary>
public class FakeHttpHandler : HttpMessageHandler
{
    private readonly object gate = new();
    private readonly List<RecordedRequest> requests = [];

    /// <summary>Decide la respuesta de cada petición. Por defecto, 200 sin cuerpo.</summary>
    public Func<RecordedRequest, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

    public IReadOnlyList<RecordedRequest> Requests { get { lock (gate) return [.. requests]; } }

    public void Reset() { lock (gate) requests.Clear(); }

    /// <summary>Responde en orden con cada elemento; el último se repite si se acaban.</summary>
    public void Script(params Func<RecordedRequest, HttpResponseMessage>[] steps)
    {
        var i = 0;
        Responder = r => steps[Math.Min(Interlocked.Increment(ref i) - 1, steps.Length - 1)](r);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, body, headers);
        lock (gate) requests.Add(recorded);
        return Responder(recorded);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("{}") };
}

/// <summary>Respuestas de Gemini ya construidas.</summary>
public static class GeminiReply
{
    public static HttpResponseMessage Text(string text) => FakeHttpHandler.Json(
        System.Text.Json.JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text } } } } }
        }));

    public static HttpResponseMessage Empty() => FakeHttpHandler.Json("""{"candidates":[]}""");
}
