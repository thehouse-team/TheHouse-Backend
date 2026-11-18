using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-10: cuenta propia. Solo se consulta el usuario del `sub` del token
// validado; no existe forma de pedir otra cuenta (sin BOLA por diseño).
// 1.2.0: versiones legales vigentes y registro de su aceptación.
public sealed class AccountService(
    IUserRepository users,
    LegalOptions? legal = null,
    IUserAccountRepository? accounts = null,
    TimeProvider? timeProvider = null)
{
    private readonly LegalOptions options = legal ?? new LegalOptions();

    public async Task<MeResponse> GetMeAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.GetByIdAsync(userId, cancellationToken);

        // Token válido de un usuario que ya no existe: 401, sin revelar
        // la diferencia con un token inválido.
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        return new MeResponse(user.Id, user.Email, user.CreatedAt, ToResponse(user.Legal), IsCurrent(user.Legal));
    }

    public LegalResponse Legal() =>
        new(options.TermsVersion, options.PrivacyVersion, options.LandlordTermsVersion,
            options.TermsUrl, options.PrivacyUrl, options.AccountDeletionUrl, options.UpdatedAt);

    public async Task<LegalAcceptanceResponse> AcceptLegalAsync(
        string userId,
        LegalAcceptanceRequest? request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var terms = Version(request?.TermsVersion, "termsVersion");
        var privacy = Version(request?.PrivacyVersion, "privacyVersion");

        // Solo se acepta la versión vigente: la app debe mostrar la actual.
        if (terms != options.TermsVersion || privacy != options.PrivacyVersion)
        {
            throw new ConflictException("legal_version_outdated");
        }

        var store = accounts ?? throw new DependencyUnavailableException();
        var acceptance = new LegalAcceptance(terms, privacy, (timeProvider ?? TimeProvider.System).GetUtcNow());
        if (!await store.SetLegalAsync(userId, acceptance, cancellationToken))
        {
            throw new InvalidCredentialsException();
        }

        return ToResponse(acceptance)!;
    }

    private bool IsCurrent(LegalAcceptance? acceptance) =>
        acceptance is not null &&
        acceptance.TermsVersion == options.TermsVersion &&
        acceptance.PrivacyVersion == options.PrivacyVersion;

    private static LegalAcceptanceResponse? ToResponse(LegalAcceptance? acceptance) =>
        acceptance is null ? null : new(acceptance.TermsVersion, acceptance.PrivacyVersion, acceptance.AcceptedAt);

    private static string Version(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 20
            ? throw new RequestValidationException(field, $"{field} is required.")
            : value.Trim();
}
