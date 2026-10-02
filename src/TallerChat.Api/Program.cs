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
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailService>();

// MongoClient es thread-safe y debe ser singleton.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<KnowledgeService>();

builder.Services.AddHttpClient<GeminiClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<GeminiOptions>>().Value.BaseUrl);
    http.Timeout = Timeout.InfiniteTimeSpan; // los tiempos se controlan por intento y en total dentro de GeminiClient
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("chat", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Envío de emails: límite estricto por IP (además hay un enfriamiento por destinatario en EmailEndpoints).
    o.AddPolicy("email", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromHours(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles(); // sirve el build de Vue desde wwwroot

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Diagnóstico: modelos de Gemini disponibles para tu clave (usa uno de ellos en Gemini:Models).
    app.MapGet("/api/models", (GeminiClient g, CancellationToken ct) => g.ListModelsAsync(ct));
}
app.MapChatEndpoints();
app.MapEmailEndpoints();
app.MapFallbackToFile("index.html");

app.Run();
