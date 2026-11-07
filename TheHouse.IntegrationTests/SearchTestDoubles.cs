using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-14: colonias en memoria con la semántica de Mongo (upsert por id OSM y
// coincidencia parcial sobre el nombre normalizado).
public sealed class InMemoryPlaceRepository : IPlaceRepository
{
    private readonly object gate = new();

    public Dictionary<long, Place> Places { get; } = [];

    public bool Unavailable { get; set; }

    public Task<IReadOnlyList<Place>> SearchAsync(string normalizedQuery, int limit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<Place>>(Places.Values
                .Where(place => SearchText.Normalize(place.Name).Contains(normalizedQuery, StringComparison.Ordinal))
                .Take(limit)
                .ToList());
        }
    }

    public Task UpsertSeedAsync(IReadOnlyCollection<PlaceSeedRecord> places, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var place in places)
            {
                Places[place.OsmNodeId] = new Place($"f0f0f0f0{place.OsmNodeId:x16}", place.Name, place.Type, place.City, place.Latitude, place.Longitude, place.OsmNodeId);
            }
        }

        return Task.CompletedTask;
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
