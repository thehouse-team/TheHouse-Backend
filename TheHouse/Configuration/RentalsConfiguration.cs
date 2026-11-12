using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-18: convenios y Mi renta. Requiere AddFavoritesAndVisits (IUserLock y
// límites), AddProfile, AddLandlord y AddMessaging registrados antes.
public static class RentalsConfiguration
{
    public static void AddRentals(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AgreementService>();
        builder.Services.AddSingleton<IRentalHistoryQuery, RentalHistoryQuery>();

        builder.Services.AddSingleton<IAgreementRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableAgreementRepository()
                : new MongoAgreementRepository(database);
        });
    }
}
