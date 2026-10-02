using MailKit.Security;

namespace TallerChat.Api;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "";
    public string Database { get; set; } = "taller_db";
    public string Collection { get; set; } = "conocimiento";
    public int MaxResults { get; set; } = 5;
}

public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public double Temperature { get; set; } = 0.2;

    /// <summary>Modelos por orden de preferencia. Si uno falla o no está disponible, se prueba el siguiente.</summary>
    public string[] Models { get; set; } = [];

    /// <summary>Reintentos por modelo ante errores transitorios (429, 5xx, timeout).</summary>
    public int MaxRetries { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 800;
    public int AttemptTimeoutSeconds { get; set; } = 20;

    /// <summary>Tope de tiempo para toda la consulta al LLM (todos los reintentos y modelos).</summary>
    public int TotalTimeoutSeconds { get; set; } = 45;
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    /// <summary>Auto, StartTls, SslOnConnect o None (None solo para pruebas locales con smtp4dev/MailHog).</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Asistente del taller";

    /// <summary>Si falta Host o From, la función de email queda desactivada y la web oculta el botón.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
