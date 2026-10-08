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

/// <summary>Reglas de la agenda de citas.</summary>
public sealed class BookingOptions
{
    public string TimeZone { get; set; } = "Europe/Madrid";
    /// <summary>Duración de cada franja (minutos).</summary>
    public int SlotMinutes { get; set; } = 60;
    /// <summary>Elevadores/puestos de trabajo: nº máximo de citas simultáneas en una franja.</summary>
    public int Bays { get; set; } = 2;
    public int MaxDaysAhead { get; set; } = 45;
    public int MinHoursAhead { get; set; } = 2;
    /// <summary>Citas activas (pendientes o confirmadas) que puede tener un cliente a la vez.</summary>
    public int MaxActivePerUser { get; set; } = 5;
    public string WeekdayOpen { get; set; } = "08:30";
    public string WeekdayClose { get; set; } = "18:30";
    public string SaturdayOpen { get; set; } = "09:00";
    public string SaturdayClose { get; set; } = "13:30";
}

/// <summary>Datos del taller que aparecen en los correos.</summary>
public sealed class ShopOptions
{
    public string Name { get; set; } = "Taller Torque";
    public string Address { get; set; } = "Calle del Motor 24, 28045 Madrid";
    public string Phone { get; set; } = "+34 910 000 000";
    /// <summary>URL pública de la web (enlace "Ver mis citas" de los correos).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
    /// <summary>
    /// Dominio de ejemplo de las cuentas del taller (como "torque.es" en "admin@torque.es"): no es un buzón real,
    /// igual que "@demo.taller" para los clientes de ejemplo. Los avisos para una cuenta con este dominio se
    /// mandan en su lugar a Smtp:From, que sí es una dirección real.
    /// </summary>
    public string PlaceholderDomain { get; set; } = "torque.es";
}

/// <summary>Caducidad de los enlaces que se envían por correo y freno al reenvío.</summary>
public sealed class AuthOptions
{
    /// <summary>Horas que vale el enlace de confirmación de la cuenta.</summary>
    public int VerifyTokenHours { get; set; } = 48;
    /// <summary>Minutos que vale el enlace para elegir una contraseña nueva.</summary>
    public int ResetTokenMinutes { get; set; } = 60;
    /// <summary>Espera mínima entre dos correos de cuenta al mismo destinatario.</summary>
    public int EmailCooldownMinutes { get; set; } = 2;
}

/// <summary>Datos iniciales. Las credenciales del administrador van en user-secrets, nunca en git.</summary>
public sealed class SeedOptions
{
    public string AdminName { get; set; } = "Administración";
    public string AdminEmail { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    /// <summary>Crea clientes y citas de ejemplo (correos @demo.taller, a los que nunca se envían emails).</summary>
    public bool Demo { get; set; }
}
