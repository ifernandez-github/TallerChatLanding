using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TallerChat.Api;

public static class AdminEndpoints
{
    // estado actual -> estados a los que puede pasar
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Statuses.Pending] = [Statuses.Confirmed, Statuses.Cancelled],
        [Statuses.Confirmed] = [Statuses.Completed, Statuses.Cancelled]
    };

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Administrador y gestor. Los gestores solo llegan a las cuentas de cliente (ver Roles.CanManage y GuardTargetAsync).
        var g = app.MapGroup("/api/admin").RequireAuthorization("staff");
        g.MapGet("/summary", SummaryAsync);
        g.MapGet("/calendar", CalendarAsync);
        g.MapGet("/appointments", AppointmentsAsync);
        g.MapPost("/appointments/{id}/status", SetStatusAsync);
        g.MapGet("/users", UsersAsync);
        g.MapPost("/users", CreateUserAsync);
        g.MapGet("/users/{id}/appointments", UserAppointmentsAsync);
        g.MapPost("/users/{id}/appointments", BookForUserAsync).RequireRateLimiting("booking");
        g.MapPut("/users/{id}", UpdateUserAsync);
        g.MapPost("/users/{id}/active", SetActiveAsync);
        g.MapGet("/users/{id}/deletion", DeletionPreviewAsync).RequireRateLimiting("account");
        // POST y no DELETE: el correo de confirmación viaja en el cuerpo, nunca en la URL.
        g.MapPost("/users/{id}/delete", DeleteUserAsync).RequireRateLimiting("account");
        g.MapPost("/users/{id}/vehicles", AddVehicleAsync);
        g.MapPut("/users/{id}/vehicles/{vehicleId}", UpdateVehicleAsync);
        g.MapDelete("/users/{id}/vehicles/{vehicleId}", DeleteVehicleAsync);
    }

    private static async Task<IResult> SummaryAsync(Db db, BookingService booking, CancellationToken ct)
    {
        var (from, to) = booking.DayRangeUtc(booking.TodayLocal());
        var f = Builders<Appointment>.Filter;
        var pending = await db.Appointments.CountDocumentsAsync(f.Eq(a => a.Status, Statuses.Pending) & f.Gt(a => a.Start, DateTime.UtcNow), cancellationToken: ct);
        var today = await db.Appointments.CountDocumentsAsync(f.Gte(a => a.Start, from) & f.Lt(a => a.Start, to) & f.In(a => a.Status, Statuses.Active), cancellationToken: ct);
        var users = await db.Users.CountDocumentsAsync(u => u.Role == Roles.Client, cancellationToken: ct);
        return Results.Ok(new SummaryDto((int)pending, (int)today, (int)users));
    }

    /// <summary>Ocupación de todo un mes (?month=AAAA-MM) para el calendario con código de colores.</summary>
    private static async Task<IResult> CalendarAsync(string? month, BookingService booking, CancellationToken ct)
    {
        var today = booking.TodayLocal();
        int year = today.Year, m = today.Month;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return ApiResults.Error(400, "Mes no válido (usa AAAA-MM).");
            year = parsed.Year; m = parsed.Month;
        }
        return Results.Ok(await booking.CalendarAsync(year, m, ct));
    }

    /// <summary>?date=AAAA-MM-DD → agenda del día; ?pending=true → todas las pendientes futuras.</summary>
    private static async Task<IResult> AppointmentsAsync(string? date, bool? pending, Db db, BookingService booking, CancellationToken ct)
    {
        var f = Builders<Appointment>.Filter;
        FilterDefinition<Appointment> filter;
        if (pending == true)
            filter = f.Eq(a => a.Status, Statuses.Pending) & f.Gt(a => a.Start, DateTime.UtcNow);
        else
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return ApiResults.Error(400, "Fecha no válida (usa AAAA-MM-DD).");
            var (from, to) = booking.DayRangeUtc(day);
            filter = f.Gte(a => a.Start, from) & f.Lt(a => a.Start, to);
        }

        var items = await db.Appointments.Find(filter).SortBy(a => a.Start).ThenBy(a => a.Bay).Limit(300).ToListAsync(ct);
        var ids = items.Select(a => a.UserId).Distinct().ToList();
        var users = (await db.Users.Find(Builders<AppUser>.Filter.In(u => u.Id, ids)).ToListAsync(ct)).ToDictionary(u => u.Id);

        return Results.Ok(items.Select(a =>
        {
            users.TryGetValue(a.UserId, out var u);
            return new AdminAppointmentDto(a.Id, a.UserId, u?.Name ?? "(usuario eliminado)", u?.Email ?? "", u?.Phone, a.ServiceId,
                Catalog.Find(a.ServiceId)?.Name ?? a.ServiceId, a.Start, a.Bay, a.Plate, a.Vehicle, a.Notes, a.Status);
        }));
    }

    private static async Task<IResult> SetStatusAsync(string id, StatusRequest req, Db db, Notifier notifier, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Cita no encontrada.");
        var target = req.Status ?? "";
        var from = Transitions.Where(t => t.Value.Contains(target)).Select(t => t.Key).ToArray();
        if (from.Length == 0) return ApiResults.Error(400, "Estado no válido.");

        var f = Builders<Appointment>.Filter;
        var filter = f.Eq(a => a.Id, id) & f.In(a => a.Status, from);
        if (target == Statuses.Confirmed) filter &= f.Gt(a => a.Start, DateTime.UtcNow);

        var updated = await db.Appointments.FindOneAndUpdateAsync(
            filter,
            Builders<Appointment>.Update.Set(a => a.Status, target).Set(a => a.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Appointment> { ReturnDocument = ReturnDocument.After }, ct);
        if (updated is null) return ApiResults.Error(409, target == Statuses.Confirmed
            ? "No se puede confirmar: la cita ya ha pasado o no está pendiente."
            : "Ese cambio de estado no es posible para la cita actual.");

        var sent = false;
        if (target is Statuses.Confirmed or Statuses.Cancelled)
        {
            var user = await db.Users.Find(u => u.Id == updated.UserId).FirstOrDefaultAsync(ct);
            if (user is not null)
                sent = await notifier.TrySendAsync(user, updated, target == Statuses.Confirmed ? EmailKind.Confirmed : EmailKind.Cancelled, CancellationToken.None);
        }
        return Results.Ok(new BookResponse(AppointmentEndpoints.ToDto(updated), sent));
    }

    /// <summary>
    /// Comprueba que quien llama puede gestionar la cuenta <paramref name="id"/>: el administrador, cualquiera;
    /// el gestor, solo clientes. Devuelve la respuesta de error, o null si puede seguir.
    /// </summary>
    private static async Task<IResult?> GuardTargetAsync(string id, ClaimsPrincipal principal, Db db, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return ApiResults.Error(404, "Usuario no encontrado.");
        var targetRole = await db.Users.Find(u => u.Id == id).Project(u => u.Role).FirstOrDefaultAsync(ct);
        if (targetRole is null) return ApiResults.Error(404, "Usuario no encontrado.");
        return Roles.CanManage(principal.FindFirstValue(ClaimTypes.Role), targetRole)
            ? null
            : ApiResults.Error(403, "Tu rol de gestor solo permite gestionar cuentas de cliente.");
    }

    private static async Task<IResult> UsersAsync(ClaimsPrincipal principal, Db db, CancellationToken ct)
    {
        // El gestor solo ve a los clientes: ni administradores ni otros gestores.
        var filter = principal.IsInRole(Roles.Admin)
            ? Builders<AppUser>.Filter.Empty
            : Builders<AppUser>.Filter.Eq(u => u.Role, Roles.Client);
        var users = await db.Users.Find(filter).SortBy(u => u.Name).Limit(500).ToListAsync(ct);
        var counts = await db.Appointments.Aggregate()
            .Group(a => a.UserId, g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byUser = counts.ToDictionary(c => c.UserId, c => c.Count);
        return Results.Ok(users.Select(u => u.ToAdminDto(byUser.GetValueOrDefault(u.Id))));
    }

    /// <summary>
    /// Alta de una cuenta por el personal. La cuenta nace activa y con el correo ya confirmado: quien la crea
    /// responde de que la dirección es correcta, así que no hay enlace de verificación. El gestor solo crea clientes.
    /// </summary>
    private static async Task<IResult> CreateUserAsync(AdminCreateUserRequest req, ClaimsPrincipal principal, Db db,
        TokenService tokens, Notifier notifier, IOptions<ShopOptions> shop, CancellationToken ct)
    {
        var role = string.IsNullOrWhiteSpace(req.Role) ? Roles.Client : req.Role;
        if (!Roles.All.Contains(role)) return ApiResults.Error(400, "El rol no es válido.");
        if (!Roles.CanManage(principal.FindFirstValue(ClaimTypes.Role), role))
            return ApiResults.Error(403, "Tu rol de gestor solo permite crear cuentas de cliente.");

        if (!Validation.TryName(req.Name, out var name)) return ApiResults.Error(400, "Escribe el nombre (entre 2 y 80 caracteres).");
        if (!Validation.TryEmail(req.Email, out var email)) return ApiResults.Error(400, "Introduce un correo electrónico válido.");
        if (!Notifier.Deliverable(email)) return ApiResults.Error(400, "Ese dominio está reservado para los datos de ejemplo. Usa otra dirección.");
        if (!Validation.TryPhone(req.Phone, out var phone)) return ApiResults.Error(400, "El teléfono no es válido.");
        if (!Validation.TryAddress(req.Address, out var address, out var problem)) return ApiResults.Error(400, problem);
        if (!Validation.StrongPassword(req.Password))
            return ApiResults.Error(400, "La contraseña debe tener al menos 10 caracteres, con letras y números.");

        var user = new AppUser
        {
            Name = name, Email = email, Phone = phone, Address = address, Role = role,
            PasswordHash = PasswordHasher.Hash(req.Password!),
            EmailVerified = true,
            MustChangePassword = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        try { await db.Users.InsertOneAsync(user, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ApiResults.Error(409, "Ya existe una cuenta con ese correo.");
        }

        // Bienvenida con enlace para elegir contraseña. Con CancellationToken.None: sale aunque se cierre la pestaña.
        var raw = await tokens.IssueAsync(user.Id, TokenKinds.Reset, CancellationToken.None, welcome: true);
        var sent = await notifier.TrySendWelcomeAsync(user, TokenService.Link(shop.Value, "/restablecer", raw), CancellationToken.None);
        return Results.Json(new AdminCreateUserResponse(user.ToAdminDto(0), sent), statusCode: 201);
    }

    /// <summary>Todas las citas de una cuenta (también las pasadas y canceladas), la más reciente primero.</summary>
    private static async Task<IResult> UserAppointmentsAsync(string id, ClaimsPrincipal principal, Db db, CancellationToken ct)
    {
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;
        var list = await db.Appointments.Find(a => a.UserId == id).SortByDescending(a => a.Start).Limit(200).ToListAsync(ct);
        return Results.Ok(list.Select(AppointmentEndpoints.ToDto));
    }

    /// <summary>Reserva una cita en nombre de un cliente (por ejemplo, cuando llama o viene al taller).</summary>
    private static async Task<IResult> BookForUserAsync(string id, BookRequest req, ClaimsPrincipal principal, Db db,
        BookingService booking, Notifier notifier, CancellationToken ct)
    {
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (user is not { Active: true }) return ApiResults.Error(409, "La cuenta está desactivada: reactívala para darle cita.");
        return await AppointmentEndpoints.BookForAsync(user, req, true, db, booking, notifier, ct);
    }

    /// <summary>Edita cualquier dato de la ficha de un cliente. Un administrador no puede quitarse a sí mismo el rol.</summary>
    private static async Task<IResult> UpdateUserAsync(string id, AdminUserRequest req, ClaimsPrincipal principal, Db db,
        AccountDeletion deletion, IMemoryCache cache, CancellationToken ct)
    {
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;
        var role = req.Role;
        // Solo el administrador reparte roles: el gestor edita la ficha del cliente, nada más.
        if (role is not null && !principal.IsInRole(Roles.Admin))
            return ApiResults.Error(403, "Solo un administrador puede cambiar el rol de una cuenta.");
        if (id == principal.UserId() && role is not null && role != Roles.Admin)
            return ApiResults.Error(400, "No puedes quitarte a ti mismo el rol de administrador.");

        // Degradar al último administrador utilizable dejaría el taller sin nadie que pueda entrar aquí.
        if (role is not null && role != Roles.Admin && await IsLastUsableAdminAsync(id, db, deletion, ct))
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de cambiarle el rol.");

        var result = await UserAdmin.UpdateProfileAsync(db, id, req.Name, req.Email, req.Phone, req.Address, role, ct);
        cache.Remove(AuthSetup.CacheKey(id)); // si ha cambiado el rol, la sesión debe revalidarse enseguida
        return result;
    }

    private static async Task<IResult> AddVehicleAsync(string id, VehicleRequest req, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await GuardTargetAsync(id, principal, db, ct) ?? await UserAdmin.AddVehicleAsync(db, id, req, ct);

    private static async Task<IResult> UpdateVehicleAsync(string id, string vehicleId, VehicleRequest req, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await GuardTargetAsync(id, principal, db, ct) ?? await UserAdmin.UpdateVehicleAsync(db, id, vehicleId, req, ct);

    private static async Task<IResult> DeleteVehicleAsync(string id, string vehicleId, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await GuardTargetAsync(id, principal, db, ct) ?? await UserAdmin.DeleteVehicleAsync(db, id, vehicleId, ct);

    /// <summary>
    /// ¿Esa cuenta es el último administrador con el que se puede entrar? Se usa antes de borrarla,
    /// degradarla o desactivarla: las tres cosas dejarían la administración inaccesible.
    /// </summary>
    private static async Task<bool> IsLastUsableAdminAsync(string id, Db db, AccountDeletion deletion, CancellationToken ct)
    {
        var target = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return target?.Role == Roles.Admin && await deletion.IsLastAdminAsync(id, ct);
    }

    /// <summary>Lo que se perderá con esa cuenta, contado en el momento de abrir el aviso de confirmación.</summary>
    private static async Task<IResult> DeletionPreviewAsync(string id, ClaimsPrincipal principal, Db db, AccountDeletion deletion, CancellationToken ct)
    {
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return user is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(await deletion.PreviewAsync(user, ct));
    }

    /// <summary>
    /// Borra una cuenta con todo lo suyo: vehículos, citas y enlaces de correo pendientes.
    /// Para evitar un clic en la fila equivocada, hay que teclear el correo exacto de esa cuenta.
    /// </summary>
    private static async Task<IResult> DeleteUserAsync(string id, AdminDeleteUserRequest req, ClaimsPrincipal principal,
        Db db, AccountDeletion deletion, Notifier notifier, CancellationToken ct)
    {
        if (id == principal.UserId())
            return ApiResults.Error(400, "No puedes borrar tu propia cuenta desde aquí. Hazlo desde tu área de cliente.");
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;

        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (user is null) return ApiResults.Error(404, "Usuario no encontrado.");

        if (!Validation.TryEmail(req.ConfirmEmail, out var confirm) || confirm != user.Email)
            return ApiResults.Error(400, "Escribe el correo exacto de la cuenta para confirmar el borrado.");

        var result = await deletion.DeleteAsync(user, principal.UserId(), ct);
        if (result.Outcome == DeletionOutcome.LastAdmin)
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de borrarla.");

        // El cliente se entera de que su cuenta ya no está, aunque la baja no la haya pedido él.
        if (result.Outcome == DeletionOutcome.Deleted)
            await notifier.TrySendDeletedAsync(user, result.Summary, true, CancellationToken.None);
        return Results.Ok(result.Summary);
    }

    private static async Task<IResult> SetActiveAsync(string id, ActiveRequest req, ClaimsPrincipal principal, Db db,
        AccountDeletion deletion, IMemoryCache cache, CancellationToken ct)
    {
        if (await GuardTargetAsync(id, principal, db, ct) is { } denied) return denied;
        if (req.Active is not { } active) return ApiResults.Error(400, "Indica si la cuenta debe estar activa.");
        if (id == principal.UserId()) return ApiResults.Error(400, "No puedes desactivar tu propia cuenta.");

        // Una cuenta desactivada no puede iniciar sesión: desactivar al último administrador equivale a perderlo.
        if (!active && await IsLastUsableAdminAsync(id, db, deletion, ct))
            return ApiResults.Error(409, "Es la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de desactivarla.");

        var result = await db.Users.UpdateOneAsync(u => u.Id == id, Builders<AppUser>.Update.Set(u => u.Active, active), cancellationToken: ct);
        if (result.MatchedCount == 0) return ApiResults.Error(404, "Usuario no encontrado.");
        cache.Remove(AuthSetup.CacheKey(id)); // la sesión de la persona desactivada deja de valer enseguida
        return Results.Ok(new { id, active });
    }
}
