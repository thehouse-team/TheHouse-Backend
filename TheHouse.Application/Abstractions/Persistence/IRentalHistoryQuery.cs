namespace TheHouse.Application.Abstractions.Persistence;

// NV-18 → NV-19: ¿el estudiante rentó ese cuarto (convenio activo o
// terminado)? Sirve para la elegibilidad de reseñas.
public interface IRentalHistoryQuery
{
    Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken);
}
