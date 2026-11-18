using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-22: doble en memoria de `roommate_profiles` (uno por usuario).
public sealed class InMemoryRoommateProfileRepository : IRoommateProfileRepository
{
    private readonly object gate = new();

    public Dictionary<string, RoommateProfile> Profiles { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<(RoommateProfile Profile, bool Created)> UpsertAsync(RoommateProfile profile, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var created = !Profiles.Values.Any(item => item.UserId == profile.UserId);
            foreach (var previous in Profiles.Values.Where(item => item.UserId == profile.UserId).ToArray())
            {
                Profiles.Remove(previous.Id);
            }

            Profiles[profile.Id] = profile;
            return Task.FromResult((profile, created));
        }
    }

    public Task<RoommateProfile?> GetByUserAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Profiles.Values.FirstOrDefault(item => item.UserId == userId));
        }
    }

    public Task<RoommateProfile?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Profiles.GetValueOrDefault(id));
        }
    }

    public Task DeleteByUserAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var profile in Profiles.Values.Where(item => item.UserId == userId).ToArray())
            {
                Profiles.Remove(profile.Id);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoommateProfile>> ListListedAsync(string universityId, string excludeUserId, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<RoommateProfile>>(Profiles.Values
                .Where(item => item.UniversityId == universityId && item.UserId != excludeUserId && item.IsListed(now))
                .OrderByDescending(item => item.UpdatedAt)
                .Take(limit)
                .ToArray());
        }
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}
