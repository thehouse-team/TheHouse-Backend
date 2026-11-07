using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-12: versión vigente de los términos del arrendador (configurable).
public sealed class LandlordOptions
{
    public string TermsVersion { get; init; } = "2026-11";
}

// NV-12 / RF-06: modo arrendador. El rol es la existencia del perfil
// (M-BE-064); se consulta en cada solicitud, sin claim en el JWT.
public sealed class LandlordService(
    ILandlordProfileRepository profiles,
    LandlordOptions options,
    TimeProvider timeProvider)
{
    public async Task<LandlordProfileResponse> CreateAsync(string userId, CreateLandlordProfileRequest request, CancellationToken cancellationToken)
    {
        var fullName = ListingRules.Text("fullName", request.FullName, 2, 120);
        if (!ListingRules.IsValidPhone(request.Phone))
        {
            throw new RequestValidationException("phone", "phone must be +52 followed by 10 digits.");
        }

        var rentalMode = ListingRules.OneOf("rentalMode", request.RentalMode, ListingRules.RentalModes);
        var company = rentalMode == "agency";
        string? rfc = null;
        if (company || request.Rfc is not null)
        {
            if (!ListingRules.IsValidRfc(request.Rfc, company))
            {
                throw new RequestValidationException("rfc", "rfc is not valid.");
            }

            rfc = request.Rfc!.ToUpperInvariant();
        }

        if (!string.Equals(request.AcceptedTermsVersion, options.TermsVersion, StringComparison.Ordinal))
        {
            throw new RequestValidationException("acceptedTermsVersion", "The current landlord terms must be accepted.");
        }

        var now = timeProvider.GetUtcNow();
        var profile = new LandlordProfile
        {
            UserId = userId,
            FullName = fullName,
            Phone = request.Phone!,
            RentalMode = rentalMode,
            Rfc = rfc,
            AcceptedTermsVersion = options.TermsVersion,
            AcceptedTermsAt = now,
            CreatedAt = now,
        };

        if (!await profiles.CreateAsync(profile, cancellationToken))
        {
            throw new ConflictException("landlord_exists");
        }

        return ToResponse(profile);
    }

    public async Task<LandlordProfileResponse> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken) ?? throw new NotFoundException("landlord");
        return ToResponse(profile);
    }

    public async Task RequireAsync(string userId, CancellationToken cancellationToken)
    {
        if (await profiles.GetAsync(userId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }
    }

    private static LandlordProfileResponse ToResponse(LandlordProfile profile) =>
        new(profile.FullName, profile.Phone, profile.RentalMode, profile.Rfc, profile.AcceptedTermsVersion, profile.CreatedAt);
}
