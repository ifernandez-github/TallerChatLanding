using System.Net;

namespace TallerChat.Api;

/// <summary>
/// Maquetación común de todos los correos. Todo el contenido variable se codifica en HTML:
/// nunca se inserta HTML que venga del cliente.
/// </summary>
public static class EmailLayout
{
    public const string Brand = "#0e8f80", Muted = "#5c7080";

    public static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Wrap(ShopOptions shop, string title, string body, string ctaUrl, string ctaText, string footerNote)
    {
        var link = E(ctaUrl);
        return "<div style=\"background:#f3f5f7;padding:24px 12px;font-family:'Segoe UI',Arial,sans-serif;color:#0f1b24\">" +
               "<div style=\"max-width:600px;margin:0 auto\">" +
               $"<h1 style=\"font-size:20px;margin:0 0 16px;color:{Brand}\">{E(title)}</h1>" +
               $"<div style=\"background:#fff;border:1px solid #e2e8ec;border-radius:14px;padding:18px 20px\">{body}" +
               $"<p style=\"margin:18px 0 0\"><a href=\"{link}\" style=\"display:inline-block;background:{Brand};color:#fff;text-decoration:none;font-weight:600;padding:10px 18px;border-radius:10px\">{E(ctaText)}</a></p></div>" +
               $"<p style=\"color:{Muted};font-size:12px;line-height:1.5\">{E(shop.Name)} · {E(shop.Address)} · {E(shop.Phone)}<br>{E(footerNote)}</p>" +
               "</div></div>";
    }
}
