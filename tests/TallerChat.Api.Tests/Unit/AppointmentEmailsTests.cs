using TallerChat.Api.Tests.Infrastructure;

namespace TallerChat.Api.Tests.Unit;

/// <summary>Plantillas de correo: contenido, codificación HTML y enlaces con la URL base configurada.</summary>
public class AppointmentEmailsTests
{
    private static readonly BookingService Booking = TestDb.Booking();
    private static readonly AppUser Ana = new() { Id = "665f1c2e8f1b2c0012345678", Name = "Ana Pérez", Email = "ana@correo.test", Phone = "600123123" };

    private static Appointment Cita(string? notes = null) => new()
    {
        Id = "665f1c2e8f1b2c00aaaaaaaa", UserId = Ana.Id, ServiceId = "brake", Plate = "4821KJH", Vehicle = "Seat León",
        Start = new DateTime(2026, 7, 1, 22, 30, 0, DateTimeKind.Utc), Notes = notes
    };

    private static ShopOptions Shop(string url = "https://taller.example.com/") => new() { PublicBaseUrl = url };

    [Theory]
    [InlineData(EmailKind.Requested, "Hemos recibido tu solicitud")]
    [InlineData(EmailKind.Confirmed, "Cita confirmada")]
    [InlineData(EmailKind.Cancelled, "Cita cancelada")]
    [InlineData(EmailKind.Reminder, "Recordatorio")]
    public void El_asunto_depende_del_tipo(EmailKind kind, string asunto)
    {
        var m = AppointmentEmails.Render(kind, Ana, Cita(), Booking, Shop());
        Assert.StartsWith(asunto, m.Subject);
        Assert.Contains("Taller Torque", m.Subject);
    }

    [Theory]
    [InlineData(EmailKind.Requested, false)]
    [InlineData(EmailKind.Confirmed, true)]
    [InlineData(EmailKind.Cancelled, false)]
    [InlineData(EmailKind.Reminder, true)]
    public void Solo_confirmar_y_recordar_adjuntan_el_ics(EmailKind kind, bool hasIcs)
    {
        var m = AppointmentEmails.Render(kind, Ana, Cita(), Booking, Shop());
        Assert.Equal(hasIcs, m.Ics is not null);
    }

    [Fact]
    public void El_ics_tiene_el_formato_de_calendario_y_la_hora_en_UTC()
    {
        var ics = AppointmentEmails.Render(EmailKind.Confirmed, Ana, Cita(), Booking, Shop()).Ics!;
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("DTSTART:20260701T223000Z", ics);
        Assert.Contains("DTEND:20260701T233000Z", ics);
        Assert.Contains("UID:665f1c2e8f1b2c00aaaaaaaa@taller", ics);
        Assert.Contains("\r\n", ics);
    }

    [Theory]
    [InlineData("https://taller.example.com/")]
    [InlineData("https://taller.example.com")]
    public void El_enlace_ver_mis_citas_usa_la_url_base_configurada(string baseUrl)
    {
        var m = AppointmentEmails.Render(EmailKind.Requested, Ana, Cita(), Booking, Shop(baseUrl));
        Assert.Contains("https://taller.example.com/mi-cuenta", m.Text);
        Assert.Contains("href=\"https://taller.example.com/mi-cuenta\"", m.Html);
        Assert.DoesNotContain("localhost", m.Html + m.Text);
        Assert.DoesNotContain("//mi-cuenta", m.Text);
    }

    [Fact]
    public void Las_notas_del_cliente_se_codifican_en_HTML()
    {
        var m = AppointmentEmails.Render(EmailKind.Requested, Ana, Cita("<script>alert(1)</script> & \"hola\""), Booking, Shop());
        Assert.DoesNotContain("<script>", m.Html);
        Assert.Contains("&lt;script&gt;", m.Html);
        Assert.Contains("<script>", m.Text); // el texto plano no es HTML: va tal cual
    }

    [Fact]
    public void El_aviso_a_administracion_enlaza_al_dia_LOCAL_de_la_cita_y_a_la_cita()
    {
        // 22:30 UTC del 1 de julio es ya el día 2 en Madrid.
        var m = AppointmentEmails.RenderAdmin(EmailKind.Requested, Ana, Cita(), Booking, Shop());
        Assert.Contains("https://taller.example.com/admin?fecha=2026-07-02&cita=665f1c2e8f1b2c00aaaaaaaa", m.Text);
        Assert.Contains("Nueva solicitud de cita", m.Subject);
        Assert.Contains("Ana Pérez", m.Text);
        Assert.Contains("ana@correo.test", m.Text);
        Assert.Null(m.Ics);
    }

    [Fact]
    public void El_aviso_de_cancelacion_a_administracion_lo_dice()
    {
        var m = AppointmentEmails.RenderAdmin(EmailKind.Cancelled, Ana, Cita(), Booking, Shop());
        Assert.StartsWith("Cita cancelada", m.Subject);
        Assert.Contains("ha cancelado", m.Text);
    }

    [Fact]
    public void El_resumen_lista_todas_las_citas_y_enlaza_a_mi_cuenta()
    {
        var m = AppointmentEmails.RenderSummary(Ana, [Cita(), Cita()], Booking, Shop());
        Assert.Contains("2 próximas citas", m.Text);
        Assert.Contains("https://taller.example.com/mi-cuenta", m.Text);
    }

    [Fact]
    public void Los_correos_de_cuenta_llevan_el_enlace_tal_cual()
    {
        var link = "https://taller.example.com/verificar?token=abc";
        var verify = AccountEmails.RenderVerify(Ana, link, 48, Shop());
        Assert.Contains(link, verify.Text);
        Assert.Contains($"href=\"{link}\"", verify.Html);
        Assert.Contains("48 horas", verify.Text);

        var reset = AccountEmails.RenderReset(Ana, "https://taller.example.com/restablecer?token=zzz", 60, Shop());
        Assert.Contains("restablecer?token=zzz", reset.Text);
        Assert.Contains("60 minutos", reset.Text);
    }
}
