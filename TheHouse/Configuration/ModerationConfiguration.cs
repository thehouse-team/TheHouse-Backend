using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-21: roles, revisión, reportes, suspensiones y bitácora. Requiere
// AddRegistration, AddSessions y AddLandlord registrados antes.
public static class ModerationConfiguration
{
    public static void AddModeration(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<StaffService>();
        builder.Services.AddSingleton<ReviewService>();
        builder.Services.AddSingleton<ModerationService>();
        builder.Services.AddSingleton<ReportService>();
        builder.Services.AddSingleton<IAccountSuspensionQuery, AccountSuspensionQuery>();

        var unavailable = new UnavailableModerationRepositories();

        builder.Services.AddSingleton<IStaffRoleRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database ? new MongoStaffRoleRepository(database) : unavailable);
        builder.Services.AddSingleton<IVerificationReviewRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database ? new MongoVerificationReviewRepository(database) : unavailable);
        builder.Services.AddSingleton<IReportRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database ? new MongoReportRepository(database) : unavailable);
        builder.Services.AddSingleton<IUserSuspensionRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database ? new MongoUserSuspensionRepository(database) : unavailable);
        builder.Services.AddSingleton<IAuditRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database ? new MongoAuditRepository(database) : unavailable);
    }
}
