using Microsoft.Extensions.Options;
using TheHouse.Application.Abstractions.Identity;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-10 1.2.0: eliminación de cuenta y aviso legal. Va después de todos los
// módulos: cada uno aporta su participante de eliminación.
public static class AccountConfiguration
{
    public static void AddAccount(this WebApplicationBuilder builder)
    {
        var legal = builder.Configuration.GetSection("Legal").Get<LegalOptions>() ?? new LegalOptions();
        if (!legal.IsValid())
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(AccountConfiguration),
                ["Configuración inválida en Legal; no se muestran valores."]);
        }

        builder.Services.AddSingleton(legal);

        // El repositorio de usuarios real (o el de pruebas) también guarda la
        // aceptación legal y la eliminación; si no, falla cerrado (503).
        builder.Services.AddSingleton<IUserAccountRepository>(provider =>
            provider.GetRequiredService<IUserRepository>() as IUserAccountRepository ?? new UnavailableUserRepository());

        builder.Services.AddSingleton<AccountDeletionService>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, RentalsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, ModerationDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, VisitsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, MessagingDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, FavoritesDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, ReviewsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, RoommatesDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, AnalyticsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, SmartMatchDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, ListingsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, NotificationsDeletionParticipant>();
        builder.Services.AddSingleton<IAccountDeletionParticipant, ProfileDeletionParticipant>();
    }
}
