using System.Net;
using Microsoft.AspNetCore.Builder;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace TallerChat.Api.Tests.Infrastructure;

/// <summary>Variaciones de arranque que cambian de un test a otro (todas con valores por defecto sensatos).</summary>
public sealed class TallerAppOptions
{
    /// <summary>Entorno de ASP.NET. «Testing» se comporta como producción (Development salta el correo y la URL de petición).</summary>
    public string Environment { get; init; } = "Testing";
    public string PublicBaseUrl { get; init; } = "https://taller.test";
    /// <summary>Valor de RENDER_EXTERNAL_URL (lo que Render define solo en producción).</summary>
    public string? RenderExternalUrl { get; init; }
    public bool EmailEnabled { get; init; } = true;
    public string AdminEmail { get; init; } = "";
    public string AdminPassword { get; init; } = "";
    public string FromAddress { get; init; } = "taller@correo-real.test";
    public string? PlaceholderDomain { get; init; }
    public bool DemoSeed { get; init; }
    /// <summary>Se ejecuta con la base de datos ya elegida pero ANTES de arrancar la API (para datos que los servicios en segundo plano leen al empezar).</summary>
    public Func<IMongoDatabase, Task>? Arrange { get; init; }
    public string? MongoConnectionOverride { get; init; }
    /// <summary>Ajustes de configuración que se aplican al final y pisan a todos los anteriores (clave «Seccion:Clave»).</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// La API real arrancada en memoria (TestServer) contra una base de datos MongoDB propia, con Gemini y Gmail
/// sustituidos por dobles HTTP. Se arranca al primer uso y al terminar borra su base de datos.
/// </summary>
public sealed class TallerApp : WebApplicationFactory<Program>
{
    public const string Password = "Taller-Test-123";

    private readonly TallerAppOptions o;
    private readonly string connectionString;
    private int slotCursor;
    private List<DateTime>? slots;

    public TallerApp(MongoFixture mongo, TallerAppOptions? options = null)
    {
        o = options ?? new TallerAppOptions();
        connectionString = o.MongoConnectionOverride ?? mongo.ConnectionString;
        DatabaseName = "taller_test_" + Guid.NewGuid().ToString("N")[..12];
        Mongo = new MongoClient(connectionString);
        Database = Mongo.GetDatabase(DatabaseName);
        o.Arrange?.Invoke(Database).GetAwaiter().GetResult();
    }

    public string DatabaseName { get; }
    public IMongoClient Mongo { get; }
    public IMongoDatabase Database { get; }
    public Db Data => new(Database);

    public FakeHttpHandler Gemini { get; } = new();
    public GoogleFake Google { get; } = new();
    public IReadOnlyList<CapturedEmail> Emails => Google.Emails;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(o.Environment);

        // UseSetting y no ConfigureAppConfiguration: Program.cs lee Mongo:ConnectionString y Gemini:ApiKey nada más
        // crear el builder, antes de que se apliquen las fuentes añadidas más tarde.
        void Set(string key, string? value) { if (value is not null) builder.UseSetting(key, value); }
        Set("Mongo:ConnectionString", connectionString);
        Set("Mongo:Database", DatabaseName);
        Set("Gemini:ApiKey", "clave-de-prueba");
        Set("Gemini:Models:0", "modelo-a");
        Set("Gemini:Models:1", "modelo-b");
        Set("Gemini:MaxRetries", "1");
        Set("Gemini:RetryBaseDelayMs", "1");
        Set("Gemini:AttemptTimeoutSeconds", "5");
        Set("Gemini:TotalTimeoutSeconds", "20");
        Set("Shop:PublicBaseUrl", o.PublicBaseUrl);
        Set("RENDER_EXTERNAL_URL", o.RenderExternalUrl);
        if (o.PlaceholderDomain is not null) Set("Shop:PlaceholderDomain", o.PlaceholderDomain);
        Set("Seed:AdminEmail", o.AdminEmail);
        Set("Seed:AdminPassword", o.AdminPassword);
        Set("Seed:Demo", o.DemoSeed ? "true" : "false");
        Set("Smtp:From", o.EmailEnabled ? o.FromAddress : "");
        if (o.EmailEnabled)
        {
            // Con RefreshToken el envío va por la API de Gmail (HTTPS): es lo que interceptamos.
            Set("Smtp:Gmail:ClientId", "cid");
            Set("Smtp:Gmail:ClientSecret", "secret");
            Set("Smtp:Gmail:RefreshToken", "refresh");
        }

        foreach (var (key, value) in o.Settings) Set(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => Gemini);
            services.AddHttpClient<GmailApiSender>().ConfigurePrimaryHttpMessageHandler(() => Google);
            // Cada cliente de prueba declara su IP con X-Test-Ip: los límites de peticiones son por IP y, sin esto,
            // todos los tests compartirían la misma («anon») y se limitarían entre sí.
            services.AddTransient<IStartupFilter, TestIpStartupFilter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { Mongo.DropDatabase(DatabaseName); } catch { /* limpieza best-effort */ }
        }
    }

    // ---------- Clientes HTTP ----------

    /// <summary>Cliente anónimo con su propia IP (y su propia cookie jar).</summary>
    public HttpClient NewClient(string? host = null)
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        c.DefaultRequestHeaders.Add("X-Test-Ip", RandomIp());
        if (host is not null) c.DefaultRequestHeaders.Host = host;
        return c;
    }

    private static string RandomIp() =>
        $"10.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    /// <summary>Cliente con la sesión de <paramref name="user"/> abierta, entrando por /api/auth/login como lo haría la web.</summary>
    public async Task<HttpClient> LoginAsync(AppUser user, string? password = null)
    {
        var c = NewClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = password ?? Password });
        Assert.True(res.IsSuccessStatusCode, $"El login de {user.Role} falló: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return c;
    }

    // ---------- Datos de partida ----------

    /// <summary>Inserta una cuenta directamente en MongoDB (verificada y activa salvo que se diga lo contrario).</summary>
    public async Task<AppUser> UserAsync(string role = Roles.Client, bool active = true, bool verified = true,
        string? email = null, string? name = null)
    {
        var u = new AppUser
        {
            Email = email ?? $"{role}-{Guid.NewGuid():N}@clientes.test".ToLowerInvariant(),
            Name = name ?? $"{role} de prueba",
            Role = role, Active = active, EmailVerified = verified,
            Phone = "600123123",
            PasswordHash = PasswordHasher.Hash(Password),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        await Data.Users.InsertOneAsync(u);
        return u;
    }

    /// <summary>Siguiente hueco libre de la agenda que ningún otro test de esta API ha usado todavía.</summary>
    public async Task<DateTime> NextSlotAsync()
    {
        if (slots is null)
        {
            var list = new List<DateTime>();
            var client = NewClient();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            for (var d = 0; d < 20 && list.Count < 40; d++)
            {
                var day = today.AddDays(d).ToString("yyyy-MM-dd");
                var res = await client.GetFromJsonAsync<AvailabilityDto>($"/api/appointments/availability?date={day}");
                if (res is null) continue;
                list.AddRange(res.Slots.Where(s => s.Available).Select(s => s.Start));
            }
            slots = list.Distinct().OrderBy(s => s).ToList();
        }
        var i = Interlocked.Increment(ref slotCursor) - 1;
        return slots[i];
    }
}

/// <summary>Pone <c>Connection.RemoteIpAddress</c> a partir de la cabecera X-Test-Ip (TestServer no tiene IP de origen).</summary>
internal sealed class TestIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (ctx, nxt) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Ip", out var ip) && IPAddress.TryParse(ip.ToString(), out var addr))
                    ctx.Connection.RemoteIpAddress = addr;
                await nxt();
            });
            next(app);
        };
}
