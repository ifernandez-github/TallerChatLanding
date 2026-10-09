using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TallerChat.Api;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

if (string.IsNullOrWhiteSpace(cfg["Mongo:ConnectionString"]))
    throw new InvalidOperationException("Falta Mongo:ConnectionString (user-secrets o variable de entorno Mongo__ConnectionString).");
if (string.IsNullOrWhiteSpace(cfg["Gemini:ApiKey"]))
    throw new InvalidOperationException("Falta Gemini:ApiKey (user-secrets o variable de entorno Gemini__ApiKey).");

builder.Services.Configure<MongoOptions>(cfg.GetSection("Mongo"));
builder.Services.Configure<GeminiOptions>(cfg.GetSection("Gemini"));
builder.Services.Configure<SmtpOptions>(cfg.GetSection("Smtp"));
builder.Services.Configure<BookingOptions>(cfg.GetSection("Booking"));
builder.Services.Configure<ShopOptions>(cfg.GetSection("Shop"));
builder.Services.Configure<SeedOptions>(cfg.GetSection("Seed"));
builder.Services.Configure<AuthOptions>(cfg.GetSection("Auth"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<GmailApiSender>(http => http.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<EmailService>();

// MongoClient es thread-safe y debe ser singleton.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<KnowledgeService>();

// Usuarios y citas
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<BookingService>();
builder.Services.AddSingleton<Notifier>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AccountDeletion>();
builder.Services.AddHostedService<StartupService>();   // índices, administrador y datos de ejemplo
builder.Services.AddHostedService<ReminderService>();  // recordatorios 24 h antes
builder.Services.AddTallerAuth();

builder.Services.AddHttpClient<GeminiClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<GeminiOptions>>().Value.BaseUrl);
    http.Timeout = Timeout.InfiniteTimeSpan; // los tiempos se controlan por intento y en total dentro de GeminiClient
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? [])
     .AllowAnyHeader().AllowAnyMethod()
     .AllowCredentials())); // la sesión viaja en cookie: sin esto el navegador la descarta

static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "anon";
// Si hay sesión, el límite es por usuario; si no, por IP.
static string Who(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Ip(c);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("chat", ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Envío de emails del chat: límite estricto por IP (además hay un enfriamiento por destinatario en EmailEndpoints).
    o.AddPolicy("email", ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromHours(1) }));
    // Registro e inicio de sesión: frena la fuerza bruta y el alta masiva de cuentas.
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("booking", ctx => RateLimitPartition.GetFixedWindowLimiter(Who(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
    // Baja de cuentas: por usuario, no por IP. Pocas peticiones bastan (abrir el aviso + confirmar).
    o.AddPolicy("account", ctx => RateLimitPartition.GetFixedWindowLimiter(Who(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) }));
    // "Enviarme mis citas por email": va siempre a la dirección de la cuenta, aun así se limita.
    o.AddPolicy("mailme", ctx => RateLimitPartition.GetFixedWindowLimiter(Who(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromHours(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
// Los estáticos van ANTES de UseRouting: si el enrutado va primero, el fallback del SPA ya ha elegido endpoint para
// /assets/*.js y UseStaticFiles se lo salta, con lo que el navegador recibe index.html en lugar del JavaScript.
app.UseDefaultFiles();
app.UseStaticFiles(); // sirve el build de Vue desde wwwroot
app.UseRouting();
app.UseCors();
app.UseAuthentication(); // antes del límite de peticiones para poder limitar por usuario
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Diagnóstico: modelos de Gemini disponibles para tu clave (usa uno de ellos en Gemini:Models).
    app.MapGet("/api/models", (GeminiClient g, CancellationToken ct) => g.ListModelsAsync(ct));
}
app.MapChatEndpoints();
app.MapEmailEndpoints();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapAppointmentEndpoints();
app.MapAdminEndpoints();
// El SPA responde a cualquier ruta MENOS /api/*: así una ruta de API inexistente devuelve 404 y no HTML.
app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html");

app.Run();
