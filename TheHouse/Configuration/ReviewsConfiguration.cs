using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-19: reseñas. Requiere AddFavoritesAndVisits, AddProfile, AddLandlord,
// AddModeration y AddRentals (IRentalHistoryQuery) registrados antes.
public static class ReviewsConfiguration
{
    public static void AddReviews(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<RoomReviewService>();
        builder.Services.AddSingleton<ReviewModerationService>();

        builder.Services.AddSingleton<IReviewRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableReviewRepository()
                : new MongoReviewRepository(database);
        });
    }
}
