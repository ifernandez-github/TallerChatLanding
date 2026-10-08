using System.Text;

namespace TallerChat.Api;

/// <summary>Correos de la cuenta: confirmar el alta, restablecer la contraseña y avisar de la baja.</summary>
public static class AccountEmails
{
    private static string E(string? s) => EmailLayout.E(s);

    public static RenderedEmail RenderVerify(AppUser user, string link, int hours, ShopOptions shop)
    {
        var intro = $"Hola {user.Name.Split(' ')[0]}, ya casi está. Pulsa el botón para confirmar que este correo es " +
                    $"tuyo y activar tu cuenta en {shop.Name}.";
        var body =
            $"<p style=\"margin:0 0 12px;line-height:1.6\">{E(intro)}</p>" +
            $"<p style=\"margin:0;color:{EmailLayout.Muted};font-size:13px\">El enlace caduca en {hours} horas. " +
            "Hasta que confirmes, no podrás iniciar sesión.</p>";

        var text = new StringBuilder($"CONFIRMA TU CUENTA EN {shop.Name.ToUpperInvariant()}\n\n{intro}\n\n")
            .Append($"Confirma aquí: {link}\n\nEl enlace caduca en {hours} horas.\n\n")
            .Append("Si no has creado ninguna cuenta, ignora este correo: sin pulsar el enlace no se activa nada.\n");

        return new RenderedEmail(
            $"Confirma tu cuenta · {shop.Name}",
            EmailLayout.Wrap(shop, "Confirma tu correo", body, link, "Confirmar mi cuenta",
                "Si no has creado ninguna cuenta, ignora este correo: sin pulsar el enlace no se activa nada."),
            text.ToString(), null);
    }

    public static RenderedEmail RenderReset(AppUser user, string link, int minutes, ShopOptions shop)
    {
        var intro = $"Hola {user.Name.Split(' ')[0]}, hemos recibido una petición para cambiar la contraseña de tu " +
                    "cuenta. Pulsa el botón y elige una nueva.";
        var body =
            $"<p style=\"margin:0 0 12px;line-height:1.6\">{E(intro)}</p>" +
            $"<p style=\"margin:0;color:{EmailLayout.Muted};font-size:13px\">El enlace caduca en {minutes} minutos y " +
            "solo se puede usar una vez. Tu contraseña actual seguirá funcionando mientras no elijas otra.</p>";

        var text = new StringBuilder($"RESTABLECER TU CONTRASEÑA EN {shop.Name.ToUpperInvariant()}\n\n{intro}\n\n")
            .Append($"Elige una nueva aquí: {link}\n\nEl enlace caduca en {minutes} minutos y solo vale una vez.\n\n")
            .Append("Si no has pedido este cambio, ignora este correo: tu contraseña no cambia.\n");

        return new RenderedEmail(
            $"Restablece tu contraseña · {shop.Name}",
            EmailLayout.Wrap(shop, "Restablece tu contraseña", body, link, "Elegir una contraseña nueva",
                "Si no has pedido este cambio, ignora este correo: tu contraseña no cambia."),
            text.ToString(), null);
    }

    /// <summary>
    /// Aviso de que la cuenta ya no existe. Se manda DESPUÉS de borrar, y por eso no enlaza a nada de la
    /// cuenta: el único destino que sigue teniendo sentido es la web del taller.
    /// </summary>
    public static RenderedEmail RenderDeleted(AppUser user, DeletionSummary summary, bool byAdmin, ShopOptions shop)
    {
        var what = Pieces(summary);
        var intro = byAdmin
            ? $"Hola {user.Name.Split(' ')[0]}, hemos dado de baja tu cuenta en {shop.Name} desde el taller. " +
              "Si no esperabas este aviso, llámanos y lo revisamos."
            : $"Hola {user.Name.Split(' ')[0]}, tu cuenta en {shop.Name} ya está borrada. Sentimos verte marchar.";

        var body =
            $"<p style=\"margin:0 0 12px;line-height:1.6\">{E(intro)}</p>" +
            $"<p style=\"margin:0 0 12px;line-height:1.6\">Hemos eliminado {E(what)}. " +
            "No conservamos ninguna copia, así que no se puede recuperar.</p>" +
            $"<p style=\"margin:0;color:{EmailLayout.Muted};font-size:13px\">Puedes volver a crear una cuenta " +
            "cuando quieras; empezará vacía.</p>";

        var text = new StringBuilder($"TU CUENTA EN {shop.Name.ToUpperInvariant()} SE HA ELIMINADO\n\n{intro}\n\n")
            .Append($"Hemos eliminado {what}. No conservamos ninguna copia, así que no se puede recuperar.\n\n")
            .Append($"Puedes volver a crear una cuenta cuando quieras: {shop.PublicBaseUrl}\n");

        return new RenderedEmail(
            $"Tu cuenta se ha eliminado · {shop.Name}",
            EmailLayout.Wrap(shop, "Tu cuenta se ha eliminado", body, shop.PublicBaseUrl, "Ir a la web del taller",
                byAdmin
                    ? "Si esta baja no la habías pedido tú, ponte en contacto con el taller."
                    : "Este es el último correo que recibirás de nosotros."),
            text.ToString(), null);
    }

    /// <summary>"tus 2 vehículos y 5 citas" — en una sola frase y sin enumerar lo que no había.</summary>
    private static string Pieces(DeletionSummary s)
    {
        var parts = new List<string>();
        if (s.Vehicles > 0) parts.Add(s.Vehicles == 1 ? "tu vehículo" : $"tus {s.Vehicles} vehículos");
        if (s.Appointments > 0) parts.Add(s.Appointments == 1 ? "tu cita" : $"tus {s.Appointments} citas");
        return parts.Count switch
        {
            0 => "tus datos de contacto",
            1 => $"{parts[0]} y tus datos de contacto",
            _ => $"{parts[0]}, {parts[1]} y tus datos de contacto"
        };
    }
}
