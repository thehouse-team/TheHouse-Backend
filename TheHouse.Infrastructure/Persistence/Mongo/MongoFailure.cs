using MongoDB.Driver;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Persistence.Mongo;

internal static class MongoFailure
{
    public static DependencyUnavailableException Unavailable() => new();
}
