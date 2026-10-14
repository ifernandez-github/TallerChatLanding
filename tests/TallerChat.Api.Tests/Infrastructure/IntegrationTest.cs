using System.Net.Http.Json;
using System.Text.Json;

namespace TallerChat.Api.Tests.Infrastructure;

/// <summary>Base de las clases de integración: una API propia (con su base de datos) por clase de test... y por test.</summary>
[Collection(MongoCollection.Name)]
public abstract class IntegrationTest(MongoFixture mongo) : IDisposable
{
    private TallerApp? app;

    protected MongoFixture Mongo { get; } = mongo;

    /// <summary>La API por defecto. Los tests que necesitan otra configuración crean la suya con <see cref="Start"/>.</summary>
    protected TallerApp App => app ??= Start();

    private readonly List<TallerApp> started = [];

    protected TallerApp Start(TallerAppOptions? options = null)
    {
        var a = new TallerApp(Mongo, options);
        started.Add(a);
        return a;
    }

    public void Dispose()
    {
        foreach (var a in started) a.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static object NewAccount(string email, string? password = null, string? confirm = null) => new
    {
        name = "Ana Pérez", email, emailConfirm = confirm ?? email, phone = "600123123", password = password ?? TallerApp.Password
    };
}

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    public static Task<HttpResponseMessage> PostAsync(this HttpClient c, string url, object? body = null) =>
        c.PostAsJsonAsync(url, body ?? new { });

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
}
