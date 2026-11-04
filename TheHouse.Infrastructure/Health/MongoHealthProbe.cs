using MongoDB.Bson;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Health;

namespace TheHouse.Infrastructure.Health;

public sealed class MongoHealthProbe(
    IMongoDatabase database) :
    IMongoHealthProbe
{
    public async ValueTask<bool> IsReadyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var command =
                new BsonDocumentCommand<BsonDocument>(
                    new BsonDocument("ping", 1));

            _ = await database.RunCommandAsync(
                command,
                cancellationToken:
                    cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            return false;
        }
    }
}
