using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-11: `user_profiles`, un documento por usuario.
public interface IProfileRepository
{
    Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken);

    Task SaveNameAsync(string userId, string name, DateTimeOffset now, CancellationToken cancellationToken);

    Task SaveUniversityAsync(string userId, string universityId, DateTimeOffset now, CancellationToken cancellationToken);
}

// NV-11: `user_preferences`, un documento por usuario.
public interface IPreferencesRepository
{
    Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken);

    // Reemplaza el documento e incrementa `Version` de forma atómica.
    Task<UserPreferences> ReplaceAsync(UserPreferences preferences, CancellationToken cancellationToken);
}
