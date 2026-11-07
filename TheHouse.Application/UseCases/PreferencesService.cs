using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

// NV-11: preferencias de búsqueda. Catálogos cerrados; `version` y
// `quickSetupCompleted` se calculan aquí, nunca vienen del cliente.
public sealed class PreferencesService(
    IPreferencesRepository preferences,
    TimeProvider timeProvider)
{
    public async Task<PreferencesResponse> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var saved = await preferences.GetAsync(userId, cancellationToken);
        return ToResponse(saved ?? new UserPreferences { UserId = userId });
    }

    public async Task<PreferencesResponse> ReplaceAsync(string userId, PreferencesRequest request, CancellationToken cancellationToken)
    {
        var validated = Validate(userId, request, timeProvider.GetUtcNow());
        var stored = await preferences.ReplaceAsync(validated, cancellationToken);
        return ToResponse(stored);
    }

    public static UserPreferences Validate(string userId, PreferencesRequest request, DateTimeOffset now)
    {
        var budget = request.Budget;
        var currency = budget?.Currency ?? PreferenceCatalogs.Currency;
        if (!string.Equals(currency, PreferenceCatalogs.Currency, StringComparison.Ordinal))
        {
            throw Invalid("budget.currency", "Only MXN is supported.");
        }

        if (budget?.Min is < 0 or > PreferenceCatalogs.MaximumBudget ||
            budget?.Max is < 0 or > PreferenceCatalogs.MaximumBudget)
        {
            throw Invalid("budget", "Budget must be between 0 and 50000.");
        }

        if (budget?.Min is { } min && budget.Max is { } max && min > max)
        {
            throw Invalid("budget", "budget.min must not exceed budget.max.");
        }

        if (request.MaxDistanceKm is { } distance &&
            (distance < PreferenceCatalogs.MinimumDistanceKm ||
             distance > PreferenceCatalogs.MaximumDistanceKm ||
             decimal.Round(distance, 1) != distance))
        {
            throw Invalid("maxDistanceKm", "maxDistanceKm must be 0.5 to 30 with one decimal.");
        }

        var services = Distinct("essentialServices", request.EssentialServices, PreferenceCatalogs.EssentialServices);
        var propertyTypes = Distinct("propertyTypes", request.PropertyTypes, PreferenceCatalogs.PropertyTypes);
        var livingWith = OneOf("livingWith", request.LivingWith, PreferenceCatalogs.LivingWith);
        var placeType = OneOf("placeType", request.PlaceType, PreferenceCatalogs.PlaceTypes);
        var transport = OneOf("transport", request.Transport, PreferenceCatalogs.Transport);

        return new UserPreferences
        {
            UserId = userId,
            BudgetMin = budget?.Min,
            BudgetMax = budget?.Max,
            Currency = PreferenceCatalogs.Currency,
            MaxDistanceKm = request.MaxDistanceKm,
            EssentialServices = services,
            LivingWith = livingWith,
            PlaceType = placeType,
            Pets = request.Pets,
            Transport = transport,
            PropertyTypes = propertyTypes,
            QuickSetupCompleted = UserPreferences.IsQuickSetupComplete(budget?.Max, livingWith, placeType),
            UpdatedAt = now,
        };
    }

    public static PreferencesResponse ToResponse(UserPreferences value) =>
        new(
            new BudgetResponse(value.BudgetMin, value.BudgetMax, value.Currency),
            value.MaxDistanceKm,
            value.EssentialServices,
            value.LivingWith,
            value.PlaceType,
            value.Pets,
            value.Transport,
            value.PropertyTypes,
            value.QuickSetupCompleted,
            value.Version,
            value.UpdatedAt);

    private static string? OneOf(string field, string? value, IReadOnlySet<string> catalog)
    {
        if (value is null)
        {
            return null;
        }

        return catalog.Contains(value) ? value : throw Invalid(field, $"{field} has an unknown value.");
    }

    private static IReadOnlyList<string> Distinct(string field, IReadOnlyList<string>? values, IReadOnlySet<string> catalog)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        if (values.Any(value => value is null || !catalog.Contains(value)))
        {
            throw Invalid(field, $"{field} has an unknown value.");
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw Invalid(field, $"{field} has repeated values.");
        }

        return values.ToArray();
    }

    private static RequestValidationException Invalid(string field, string message) => new(field, message);
}
