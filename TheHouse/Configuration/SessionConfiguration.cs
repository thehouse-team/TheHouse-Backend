using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-10: sesiones con refresh token y cuenta propia. Requiere que
// AddRegistration (IMongoDatabase) y AddLogout (revocación en Redis)
// se registren antes.
public static class SessionConfiguration
{
    public static void AddSessions(
        this WebApplicationBuilder builder)
    {
        var options =
            builder.Configuration
                .GetSection("RefreshToken")
                .Get<RefreshTokenOptions>() ??
            new RefreshTokenOptions();

        if (!options.IsValid())
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(SessionConfiguration),
                ["Configuración inválida en RefreshToken; no se muestran valores."]);
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<AccountService>();

        var mongo =
            builder.Configuration
                .GetSection("MongoDB")
                .Get<MongoOptions>() ??
            new MongoOptions();

        if (mongo.Enabled)
        {
            builder.Services.AddSingleton<IRefreshTokenStore>(
                provider =>
                    new MongoRefreshTokenStore(
                        provider.GetRequiredService<IMongoDatabase>()));
        }
        else
        {
            // Sin MongoDB, login y refresh fallan cerrados (503).
            builder.Services.AddSingleton<IRefreshTokenStore,
                UnavailableRefreshTokenStore>();
        }
    }
}
