using System.Globalization;
using System.Net;
using System.Text;

namespace TallerChat.Api;

public enum EmailKind { Requested, Confirmed, Cancelled, Reminder }

public sealed record RenderedEmail(string Subject, string Html, string Text, string? Ics);

/// <summary>Plantillas fijas de los correos de citas. Todo dato variable se codifica en HTML.</summary>
public static class AppointmentEmails
{
    private const string Brand = "#0e8f80", Muted = "#5c7080";

    private static string E(string? s) => EmailLayout.E(s);

    public static RenderedEmail Render(EmailKind kind, AppUser user, Appointment a, BookingService booking, ShopOptions shop)
    {
        var service = Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId;
        var when = booking.Describe(a.Start);
        var (subject, title, intro) = kind switch
        {
            EmailKind.Requested => ($"Hemos recibido tu solicitud de cita · {shop.Name}", "Solicitud de cita recibida",
                "Hemos recibido tu solicitud. El taller la revisará y te avisaremos por correo cuando quede confirmada."),
            EmailKind.Confirmed => ($"Cita confirmada: {when} · {shop.Name}", "Tu cita está confirmada",
                "Te esperamos en el taller. Si no puedes venir, cancélala desde tu área de cliente para liberar el hueco."),
            EmailKind.Cancelled => ($"Cita cancelada · {shop.Name}", "Tu cita ha sido cancelada",
                "Esta cita ya no está en la agenda. Cuando quieras, puedes reservar otra desde la web."),
            _ => ($"Recordatorio: tu cita es {when} · {shop.Name}", "Recordatorio de tu cita",
                "Te recordamos que tienes una cita en las próximas 24 horas.")
        };

        var rows = new List<(string Label, string Value)>
        {
            ("Servicio", service), ("Fecha y hora", when), ("Vehículo", $"{a.Vehicle} · {a.Plate}"), ("Taller", $"{shop.Name}, {shop.Address}")
        };
        if (!string.IsNullOrWhiteSpace(a.Notes)) rows.Add(("Tus notas", a.Notes));

        var html = Layout(shop, title, intro, rows);
        var text = new StringBuilder($"{title.ToUpperInvariant()}\n\n{intro}\n\n");
        foreach (var (l, v) in rows) text.Append($"{l}: {v}\n");
        text.Append($"\nVer mis citas: {shop.PublicBaseUrl.TrimEnd('/')}/mi-cuenta\n\nTeléfono del taller: {shop.Phone}\n");

        var ics = kind is EmailKind.Confirmed or EmailKind.Reminder ? Ics(a, service, shop, booking.SlotMinutes) : null;
        return new RenderedEmail(subject, html, text.ToString(), ics);
    }

    /// <summary>
    /// Aviso a la administración: nueva solicitud o cancelación. El cliente nunca ve este correo, y por eso
    /// lleva datos que el correo del cliente no necesita (su nombre, su correo, su teléfono).
    /// </summary>
    public static RenderedEmail RenderAdmin(EmailKind kind, AppUser customer, Appointment a, BookingService booking, ShopOptions shop)
    {
        var service = Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId;
        var when = booking.Describe(a.Start);
        var (subject, title, intro) = kind == EmailKind.Cancelled
            ? ($"Cita cancelada: {when} · {shop.Name}", "Un cliente ha cancelado una cita",
               "El cliente ha cancelado esta cita desde su área personal. El hueco ya está libre en la agenda.")
            : ($"Nueva solicitud de cita: {when} · {shop.Name}", "Nueva solicitud de cita",
               "Un cliente ha pedido cita. Revísala y confírmala (o recházala) desde la administración.");

        var rows = new List<(string Label, string Value)> { ("Cliente", customer.Name), ("Correo", customer.Email) };
        if (!string.IsNullOrWhiteSpace(customer.Phone)) rows.Add(("Teléfono", customer.Phone));
        rows.Add(("Servicio", service));
        rows.Add(("Fecha y hora", when));
        rows.Add(("Vehículo", $"{a.Vehicle} · {a.Plate}"));
        if (!string.IsNullOrWhiteSpace(a.Notes)) rows.Add(("Notas del cliente", a.Notes));

        var link = AdminLink(shop, booking, a);
        var html = EmailLayout.Wrap(shop, title, RowsHtml(intro, rows), link, "Ver en la agenda",
            "Aviso automático para la administración del taller.");
        var text = new StringBuilder($"{title.ToUpperInvariant()}\n\n{intro}\n\n");
        foreach (var (l, v) in rows) text.Append($"{l}: {v}\n");
        text.Append($"\nVer en la agenda: {link}\n");

        return new RenderedEmail(subject, html, text.ToString(), null);
    }

    /// <summary>Enlace directo a la agenda de administración, abierta ya en el día de esa cita.</summary>
    private static string AdminLink(ShopOptions shop, BookingService booking, Appointment a) =>
        $"{shop.PublicBaseUrl.TrimEnd('/')}/admin?fecha={booking.LocalDateIso(a.Start)}&cita={a.Id}";

    public static RenderedEmail RenderSummary(AppUser user, IReadOnlyList<Appointment> upcoming, BookingService booking, ShopOptions shop)
    {
        var intro = upcoming.Count == 1 ? "Esta es tu próxima cita en el taller." : $"Estas son tus {upcoming.Count} próximas citas en el taller.";
        var h = new StringBuilder();
        var t = new StringBuilder($"MIS CITAS EN {shop.Name.ToUpperInvariant()}\n\n{intro}\n\n");
        foreach (var a in upcoming)
        {
            var service = Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId;
            var state = a.Status == Statuses.Confirmed ? "Confirmada" : "Pendiente de confirmar";
            h.Append("<div style=\"border-top:1px solid #e2e8ec;padding:12px 0\">")
             .Append($"<p style=\"margin:0;font-weight:600\">{E(booking.Describe(a.Start))}</p>")
             .Append($"<p style=\"margin:2px 0 0\">{E(service)} · {E(a.Vehicle)} ({E(a.Plate)})</p>")
             .Append($"<p style=\"margin:2px 0 0;color:{Muted};font-size:13px\">{E(state)}</p></div>");
            t.Append($"- {booking.Describe(a.Start)} · {service} · {a.Vehicle} ({a.Plate}) · {state}\n");
        }
        t.Append($"\nGestiona tus citas: {shop.PublicBaseUrl.TrimEnd('/')}/mi-cuenta\n");
        var body = $"<p style=\"margin:0 0 8px;line-height:1.6\">{E(intro)}</p>{h}";
        return new RenderedEmail($"Tus próximas citas · {shop.Name}", Wrap(shop, "Tus próximas citas", body), t.ToString(), null);
    }

    private static string Layout(ShopOptions shop, string title, string intro, List<(string Label, string Value)> rows) =>
        Wrap(shop, title, RowsHtml(intro, rows));

    private static string RowsHtml(string intro, List<(string Label, string Value)> rows)
    {
        var b = new StringBuilder($"<p style=\"margin:0 0 14px;line-height:1.6\">{E(intro)}</p>");
        foreach (var (l, v) in rows)
            b.Append($"<div style=\"border-top:1px solid #e2e8ec;padding:10px 0\"><p style=\"margin:0;color:{Muted};font-size:13px\">{E(l)}</p><p style=\"margin:0;font-weight:600\">{E(v)}</p></div>");
        return b.ToString();
    }

    private static string Wrap(ShopOptions shop, string title, string body) =>
        EmailLayout.Wrap(shop, title, body, shop.PublicBaseUrl.TrimEnd('/') + "/mi-cuenta", "Ver mis citas",
            "Recibes este correo porque tienes una cuenta en la web del taller. Si no esperabas este mensaje, puedes ignorarlo.");

    private static string IcsEscape(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static string Ics(Appointment a, string service, ShopOptions shop, int minutes)
    {
        string F(DateTime d) => d.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        return string.Join("\r\n",
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Taller//Citas//ES", "CALSCALE:GREGORIAN", "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{a.Id}@taller",
            $"DTSTAMP:{F(DateTime.UtcNow)}",
            $"DTSTART:{F(a.Start)}",
            $"DTEND:{F(a.Start.AddMinutes(minutes))}",
            $"SUMMARY:{IcsEscape($"{service} · {shop.Name}")}",
            $"LOCATION:{IcsEscape(shop.Address)}",
            $"DESCRIPTION:{IcsEscape($"{a.Vehicle} ({a.Plate})")}",
            "END:VEVENT", "END:VCALENDAR") + "\r\n";
    }
}
