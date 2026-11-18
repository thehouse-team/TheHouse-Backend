using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Analytics;

// NV-25: el detalle del cuarto avisa que alguien lo vio. Nunca lanza: si el
// registro falla, el detalle responde igual.
public interface IRoomViewSink
{
    Task RecordAsync(Room room, string viewerId, CancellationToken cancellationToken);
}

// NV-25: sal aleatoria del día, compartida entre réplicas (Redis `SET NX`
// con expiración de 48 h). Se descarta sola; no es un secreto configurado.
public interface IViewSaltStore
{
    Task<byte[]> GetDailySaltAsync(string day, CancellationToken cancellationToken);
}
