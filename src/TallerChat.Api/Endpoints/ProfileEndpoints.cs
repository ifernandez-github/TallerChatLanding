using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;

namespace TallerChat.Api;

/// <summary>Datos y vehículos del cliente autenticado. La administración usa UserAdmin para lo mismo sobre otra cuenta.</summary>
public static class ProfileEndpoints
{
    public const int MaxVehicles = 10;

    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/profile").RequireAuthorization();
        g.MapPut("", UpdateAsync);
        g.MapPost("/vehicles", AddVehicleAsync);
        g.MapPut("/vehicles/{vehicleId}", UpdateVehicleAsync);
        g.MapDelete("/vehicles/{vehicleId}", DeleteVehicleAsync);
        g.MapGet("/deletion", DeletionPreviewAsync).RequireRateLimiting("account");
        // POST y no DELETE: lleva la contraseña en el cuerpo, y hay proxies que descartan el cuerpo de un DELETE.
        g.MapPost("/delete", DeleteAccountAsync).RequireRateLimiting("account");
    }

    private static async Task<IResult> UpdateAsync(ProfileRequest req, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await UserAdmin.UpdateProfileAsync(db, principal.UserId(), req.Name, null, req.Phone, req.Address, null, ct);

    private static async Task<IResult> AddVehicleAsync(VehicleRequest req, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await UserAdmin.AddVehicleAsync(db, principal.UserId(), req, ct);

    private static async Task<IResult> UpdateVehicleAsync(string vehicleId, VehicleRequest req, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await UserAdmin.UpdateVehicleAsync(db, principal.UserId(), vehicleId, req, ct);

    private static async Task<IResult> DeleteVehicleAsync(string vehicleId, ClaimsPrincipal principal, Db db, CancellationToken ct)
        => await UserAdmin.DeleteVehicleAsync(db, principal.UserId(), vehicleId, ct);

    /// <summary>Lo que se perdería al borrar la cuenta. La web lo enseña en el aviso antes de pedir confirmación.</summary>
    private static async Task<IResult> DeletionPreviewAsync(ClaimsPrincipal principal, Db db, AccountDeletion deletion,
        CancellationToken ct)
    {
        var id = principal.UserId();
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        return user is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(await deletion.PreviewAsync(user, ct));
    }

    /// <summary>
    /// Baja definitiva de la propia cuenta. Se pide la contraseña a propósito: con solo la cookie de sesión
    /// (un ordenador prestado, una pestaña abierta) no se puede borrar nada.
    /// </summary>
    private static async Task<IResult> DeleteAccountAsync(DeleteAccountRequest req, ClaimsPrincipal principal, Db db,
        AccountDeletion deletion, Notifier notifier, HttpContext http, CancellationToken ct)
    {
        var id = principal.UserId();
        var user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (user is null) return ApiResults.Error(404, "Usuario no encontrado.");

        if (string.IsNullOrEmpty(req.Password) || req.Password.Length > 100 ||
            !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return ApiResults.Error(403, "La contraseña no es correcta.");

        var result = await deletion.DeleteAsync(user, id, ct);
        if (result.Outcome == DeletionOutcome.LastAdmin)
            return ApiResults.Error(409, "Eres la única cuenta de administración con la que se puede entrar. Nombra a otro administrador antes de darte de baja.");

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Solo se despide a quien se acaba de borrar: si la cuenta ya no estaba, el aviso saldría dos veces.
        // CancellationToken.None: el correo sale aunque se cierre la pestaña.
        if (result.Outcome == DeletionOutcome.Deleted)
            await notifier.TrySendDeletedAsync(user, result.Summary, false, CancellationToken.None);
        return Results.Ok(result.Summary);
    }
}

/// <summary>
/// Operaciones sobre la ficha de un usuario (datos personales y vehículos), compartidas por el área de cliente
/// y la de administración. Devuelven siempre el usuario actualizado para que la web no tenga que recargar.
/// </summary>
public static class UserAdmin
{
    public static async Task<IResult> UpdateProfileAsync(Db db, string userId, string? name, string? email, string? phone,
        AddressDto? addressDto, string? role, CancellationToken ct)
    {
        if (!Validation.TryPhone(phone, out var cleanPhone)) return ApiResults.Error(400, "El teléfono no es válido.");
        if (!Validation.TryAddress(addressDto, out var address, out var problem)) return ApiResults.Error(400, problem);

        var update = Builders<AppUser>.Update
            .Set(u => u.Phone, cleanPhone)
            .Set(u => u.Address, address);

        // El nombre solo se toca si llega en la petición (permite editar solo la dirección o el rol).
        if (name is not null)
        {
            if (!Validation.TryName(name, out var cleanName)) return ApiResults.Error(400, "El nombre debe tener entre 2 y 80 caracteres.");
            update = update.Set(u => u.Name, cleanName);
        }

        if (email is not null)
        {
            if (!Validation.TryEmail(email, out var cleanEmail)) return ApiResults.Error(400, "El correo electrónico no es válido.");
            update = update.Set(u => u.Email, cleanEmail);
        }
        if (role is not null)
        {
            if (role != Roles.Client && role != Roles.Admin) return ApiResults.Error(400, "El rol no es válido.");
            update = update.Set(u => u.Role, role);
        }

        try
        {
            var user = await db.Users.FindOneAndUpdateAsync<AppUser>(u => u.Id == userId, update,
                new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After }, ct);
            return user is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(user.ToDto());
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            return ApiResults.Error(409, "Ya existe otra cuenta con ese correo.");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ApiResults.Error(409, "Ya existe otra cuenta con ese correo.");
        }
    }

    public static async Task<IResult> AddVehicleAsync(Db db, string userId, VehicleRequest req, CancellationToken ct)
    {
        if (!Validation.TryVehicle(req, null, out var vehicle, out var problem)) return ApiResults.Error(400, problem);

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
        if (user is null) return ApiResults.Error(404, "Usuario no encontrado.");
        if (user.Vehicles.Count >= ProfileEndpoints.MaxVehicles)
            return ApiResults.Error(409, $"No se pueden guardar más de {ProfileEndpoints.MaxVehicles} vehículos.");
        if (user.Vehicles.Any(v => v.Plate == vehicle.Plate))
            return ApiResults.Error(409, "Ya hay un vehículo guardado con esa matrícula.");

        var updated = await db.Users.FindOneAndUpdateAsync<AppUser>(u => u.Id == userId,
            Builders<AppUser>.Update.Push(u => u.Vehicles, vehicle),
            new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After }, ct);
        return updated is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(updated.ToDto());
    }

    public static async Task<IResult> UpdateVehicleAsync(Db db, string userId, string vehicleId, VehicleRequest req, CancellationToken ct)
    {
        if (!Validation.TryVehicle(req, vehicleId, out var vehicle, out var problem)) return ApiResults.Error(400, problem);

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
        if (user is null) return ApiResults.Error(404, "Usuario no encontrado.");
        var current = user.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        if (current is null) return ApiResults.Error(404, "Vehículo no encontrado.");
        if (user.Vehicles.Any(v => v.Id != vehicleId && v.Plate == vehicle.Plate))
            return ApiResults.Error(409, "Ya hay otro vehículo guardado con esa matrícula.");

        vehicle.CreatedAt = current.CreatedAt; // la edición no cambia cuándo se dio de alta
        var filter = Builders<AppUser>.Filter.Eq(u => u.Id, userId) &
                     Builders<AppUser>.Filter.ElemMatch(u => u.Vehicles, v => v.Id == vehicleId);
        var updated = await db.Users.FindOneAndUpdateAsync<AppUser>(filter,
            Builders<AppUser>.Update.Set("vehicles.$", vehicle),
            new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After }, ct);
        return updated is null ? ApiResults.Error(404, "Vehículo no encontrado.") : Results.Ok(updated.ToDto());
    }

    public static async Task<IResult> DeleteVehicleAsync(Db db, string userId, string vehicleId, CancellationToken ct)
    {
        // Las citas guardan una copia de los datos del vehículo, así que borrarlo no afecta al historial.
        var updated = await db.Users.FindOneAndUpdateAsync<AppUser>(u => u.Id == userId,
            Builders<AppUser>.Update.PullFilter(u => u.Vehicles, v => v.Id == vehicleId),
            new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After }, ct);
        return updated is null ? ApiResults.Error(404, "Usuario no encontrado.") : Results.Ok(updated.ToDto());
    }
}
