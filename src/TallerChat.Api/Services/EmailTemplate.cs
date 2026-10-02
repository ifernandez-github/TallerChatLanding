using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TallerChat.Api;

/// <summary>Plantilla fija del correo. Todo el contenido se codifica en HTML (no se acepta HTML del cliente).</summary>
public static partial class EmailTemplate
{
    private const string Brand = "#0e8f80";
    private const string Muted = "#5c7080";

    [GeneratedRegex(@"\[(\d+)\]")]
    private static partial Regex CiteRegex();

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static (string Html, string Text) Render(IReadOnlyList<EmailItem> items)
    {
        var h = new StringBuilder();
        var t = new StringBuilder("RESPUESTA DEL ASISTENTE DEL TALLER\n\n");

        h.Append("<div style=\"background:#f3f5f7;padding:24px 12px;font-family:'Segoe UI',Arial,sans-serif;color:#0f1b24\">")
         .Append("<div style=\"max-width:640px;margin:0 auto\">")
         .Append($"<h1 style=\"font-size:20px;margin:0 0 16px;color:{Brand}\">Respuesta del asistente del taller</h1>");

        foreach (var item in items)
        {
            h.Append("<div style=\"background:#fff;border:1px solid #e2e8ec;border-radius:14px;padding:18px 20px;margin-bottom:16px\">")
             .Append($"<p style=\"margin:0 0 4px;color:{Muted};font-size:13px\">Tu pregunta</p>")
             .Append($"<p style=\"margin:0 0 14px;font-weight:600\">{E(item.Question)}</p>");

            var answer = CiteRegex().Replace(E(item.Answer).Replace("\n", "<br>"),
                m => $"<span style=\"background:#f5b301;color:#2b2100;font-weight:700;font-size:12px;padding:1px 6px;border-radius:4px\">{m.Groups[1].Value}</span>");
            h.Append($"<p style=\"margin:0 0 14px;line-height:1.6\">{answer}</p>");
            t.Append("PREGUNTA: ").Append(item.Question).Append("\n\n").Append(item.Answer).Append("\n\n");

            if (item.Sources is { Count: > 0 })
            {
                h.Append($"<p style=\"margin:0 0 6px;color:{Muted};font-size:13px\">Fuentes en la base de conocimiento</p>");
                t.Append("FUENTES:\n");
                foreach (var s in item.Sources)
                {
                    h.Append("<div style=\"border-top:1px solid #e2e8ec;padding:10px 0\">")
                     .Append($"<p style=\"margin:0;font-weight:600\">[{s.Ref}] {E(s.Title)}</p>")
                     .Append($"<p style=\"margin:0 0 6px;color:{Muted};font-size:13px\">{E(s.Category)} / {E(s.SubCategory)} · {E(s.ChunkId)}</p>");
                    t.Append($"[{s.Ref}] {s.Title} ({s.Category} / {s.SubCategory}, {s.ChunkId})\n");

                    foreach (var x in s.Sections ?? new List<Section>())
                    {
                        h.Append($"<p style=\"margin:0 0 4px;font-size:14px\"><strong>{E(x.Label)}:</strong> {E(x.Text)}</p>");
                        t.Append($"  - {x.Label}: {x.Text}\n");
                    }
                    h.Append("</div>");
                    t.Append('\n');
                }
            }
            h.Append("</div>");
        }

        const string legal = "Información orientativa basada en la base de conocimiento del taller; no sustituye el diagnóstico de un profesional. " +
                             "Este correo se envió a petición de un visitante del sitio web. Si no lo esperabas, puedes ignorarlo.";
        h.Append($"<p style=\"color:{Muted};font-size:12px;line-height:1.5\">{legal}</p></div></div>");
        t.Append(legal).Append('\n');
        return (h.ToString(), t.ToString());
    }
}
