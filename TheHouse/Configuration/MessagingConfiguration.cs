using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-16: solicitudes y chat. Requiere AddFavoritesAndVisits (IUserLock y
// límites), AddProfile, AddLandlord y AddModeration registrados antes.
public static class MessagingConfiguration
{
    public static void AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<RentalRequestService>();
        builder.Services.AddSingleton<ConversationService>();

        builder.Services.AddSingleton<IRentalRequestRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableRentalRequestRepository()
                : new MongoRentalRequestRepository(database);
        });

        builder.Services.AddSingleton<IConversationRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableConversationRepository()
                : new MongoConversationRepository(database);
        });
    }
}
