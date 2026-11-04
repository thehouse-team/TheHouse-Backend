using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;

namespace TheHouse.Application.UseCases;

public sealed class UniversitySeedService(
    IUniversityRepository universityRepository)
{
    public Task SeedAsync(
        CancellationToken cancellationToken) =>
        universityRepository.UpsertSeedAsync(
            UniversitySeedData.Create(),
            cancellationToken);
}
