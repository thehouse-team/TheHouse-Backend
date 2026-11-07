using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-11: perfil propio y universidad elegida. El usuario sale del `sub`.
public sealed class ProfileService(
    IProfileRepository profiles,
    IPreferencesRepository preferences,
    IUniversityRepository universities,
    TimeProvider timeProvider)
{
    public const int MaximumNameLength = 80;

    public async Task<ProfileResponse> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken);
        var saved = await preferences.GetAsync(userId, cancellationToken);

        UniversitySummary? university = null;
        if (profile?.UniversityId is { } universityId)
        {
            var found = await universities.GetByIdAsync(universityId, cancellationToken);
            university = found is null ? null : new UniversitySummary(found.Id, found.Code, found.Name, found.City);
        }

        return new ProfileResponse(
            profile?.Name,
            university,
            saved?.QuickSetupCompleted ?? false,
            profile?.UpdatedAt);
    }

    public async Task<ProfileResponse> UpdateNameAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) ||
            name.Length > MaximumNameLength ||
            name.Any(char.IsControl))
        {
            throw new RequestValidationException("name", "name must have 1 to 80 characters.");
        }

        await profiles.SaveNameAsync(userId, name, timeProvider.GetUtcNow(), cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task SetUniversityAsync(string userId, SetUniversityRequest request, CancellationToken cancellationToken)
    {
        if (!UniversityQueryValidation.IsObjectId(request.UniversityId))
        {
            throw new RequestValidationException("universityId", "universityId must be a 24-character hexadecimal ObjectId.");
        }

        var universityId = request.UniversityId!.ToLowerInvariant();
        _ = await universities.GetByIdAsync(universityId, cancellationToken)
            ?? throw new NotFoundException("university");

        await profiles.SaveUniversityAsync(userId, universityId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
