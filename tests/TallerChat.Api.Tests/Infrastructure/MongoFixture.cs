using System.Diagnostics;
using Testcontainers.MongoDb;

namespace TallerChat.Api.Tests.Infrastructure;

/// <summary>
/// MongoDB para los tests de integración. Prioridad:
/// 1) la variable de entorno <c>TALLER_TEST_MONGO</c> (cadena de conexión de un MongoDB ya arrancado: útil en CI con
///    un servicio <c>mongo:7</c> o con tu Mongo local);
/// 2) un contenedor <c>mongo:7</c> levantado con Testcontainers (necesita Docker);
/// 3) si no hay ninguna de las dos, los tests marcados con <see cref="MongoFactAttribute"/> se omiten (skip).
/// Cada test de integración usa su propia base de datos (nombre único), así que comparten servidor sin molestarse.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    public const string EnvVar = "TALLER_TEST_MONGO";

    private MongoDbContainer? container;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(external)) { ConnectionString = external; return; }

        if (!Availability.Docker) return; // los tests se habrán omitido
        container = new MongoDbBuilder("mongo:7.0").Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
    }

    public static class Availability
    {
        private static readonly Lazy<bool> docker = new(ProbeDocker);

        public static bool Docker => docker.Value;
        public static bool Available => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)) || Docker;

        private static bool ProbeDocker()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
                });
                if (p is null) return false;
                if (!p.WaitForExit(8000)) { p.Kill(); return false; }
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}

/// <summary>Un solo MongoDB para todas las clases de integración.</summary>
[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "mongo";
}

/// <summary><c>[Fact]</c> que se omite (skip, no falla) si no hay MongoDB ni Docker en la máquina.</summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (!MongoFixture.Availability.Available)
            Skip = $"Sin MongoDB: define {MongoFixture.EnvVar} o arranca Docker (ver docs/testing.md).";
    }
}

/// <summary><c>[Theory]</c> que se omite si no hay MongoDB ni Docker.</summary>
public sealed class MongoTheoryAttribute : TheoryAttribute
{
    public MongoTheoryAttribute()
    {
        if (!MongoFixture.Availability.Available)
            Skip = $"Sin MongoDB: define {MongoFixture.EnvVar} o arranca Docker (ver docs/testing.md).";
    }
}
