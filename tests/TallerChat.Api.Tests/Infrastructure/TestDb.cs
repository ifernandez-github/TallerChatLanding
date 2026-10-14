using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace TallerChat.Api.Tests.Infrastructure;

public static class TestDb
{
    /// <summary>
    /// <see cref="Db"/> que nunca llega a conectar: <c>MongoClient</c> es perezoso y GetCollection no abre ninguna
    /// conexión. Sirve para construir servicios (BookingService...) cuyos métodos puros no tocan la base de datos.
    /// Si un test unitario llegara a consultarla, fallaría por timeout en vez de pasar en silencio.
    /// </summary>
    public static Db Offline() =>
        new(new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=200").GetDatabase("offline"));

    public static BookingService Booking(Action<BookingOptions>? configure = null)
    {
        var o = new BookingOptions();
        configure?.Invoke(o);
        return new BookingService(Offline(), Options.Create(o));
    }
}
