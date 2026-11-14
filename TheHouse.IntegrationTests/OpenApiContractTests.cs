using System.Net;
using System.Text.Json;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Openapi_is_reproducible_versioned_and_contains_only_authorized_operations()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();

        var first =
            await client.GetStringAsync("/openapi/v1.json");

        var second =
            await client.GetStringAsync("/openapi/v1.json");

        Assert.Equal(first, second);

        using var document =
            JsonDocument.Parse(first);

        Assert.StartsWith(
            "3.",
            document.RootElement
                .GetProperty("openapi")
                .GetString());

        Assert.Equal(
            ApiConventions.Version,
            document.RootElement
                .GetProperty("info")
                .GetProperty("version")
                .GetString());

        Assert.Equal(
            "/api/v1",
            ApiConventions.Prefix);

        var paths =
            document.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            Assert.True(
                path.Name.StartsWith(
                    "/health/",
                    StringComparison.Ordinal) ||
                path.Name == RegistrationEndpoint.Path ||
                path.Name == LoginEndpoint.Path ||
                path.Name == LogoutEndpoint.Path ||
                path.Name == RefreshEndpoint.Path ||
                path.Name == UsersMeEndpoint.Path ||
                path.Name == PropertyEndpoint.ListPath ||
                path.Name == PropertyEndpoint.DetailPath ||
                path.Name == UniversityEndpoint.ListPath ||
                path.Name == UniversityEndpoint.DetailPath ||
                path.Name == FavoritesEndpoint.BasePath ||
                path.Name == FavoritesEndpoint.IdsPath ||
                path.Name == FavoritesEndpoint.ItemPath ||
                path.Name == VisitsEndpoint.MinePath ||
                path.Name == VisitsEndpoint.BasePath ||
                path.Name == VisitsEndpoint.DetailPath ||
                path.Name == VisitsEndpoint.CancelPath ||
                path.Name == ProfileEndpoint.ProfilePath ||
                path.Name == ProfileEndpoint.UniversityPath ||
                path.Name == ProfileEndpoint.PreferencesPath ||
                path.Name == UniversitySearchEndpoint.Path ||
                path.Name.StartsWith(LandlordEndpoint.BasePath + "/", StringComparison.Ordinal) ||
                path.Name.StartsWith(AdminEndpoint.BasePath + "/", StringComparison.Ordinal) ||
                path.Name == AdminEndpoint.ReportsPath ||
                path.Name.StartsWith(SearchEndpoint.RoomsPath + "/", StringComparison.Ordinal) ||
                path.Name == SearchEndpoint.PlacesPath + "/search" ||
                path.Name.StartsWith(MessagingEndpoint.MinePath, StringComparison.Ordinal) ||
                path.Name.StartsWith(MessagingEndpoint.ConversationsPath, StringComparison.Ordinal) ||
                path.Name.StartsWith(RentalsEndpoint.StudentPath + "/", StringComparison.Ordinal) ||
                path.Name == RentalsEndpoint.RentalPath ||
                path.Name.StartsWith(NotificationsEndpoint.BasePath, StringComparison.Ordinal) ||
                path.Name.StartsWith(ReviewsEndpoint.ReviewsPath + "/", StringComparison.Ordinal) ||
                path.Name == ReviewsEndpoint.MinePath,
                $"Unexpected OpenAPI path: {path.Name}");
        }

        // NV-16: las 12 operaciones de solicitudes y chat están publicadas.
        foreach (var messagingPath in new[]
                 {
                     MessagingEndpoint.RoomRequestsPath, MessagingEndpoint.MinePath, MessagingEndpoint.MineDetailPath,
                     MessagingEndpoint.MineCancelPath, MessagingEndpoint.LandlordPath, MessagingEndpoint.LandlordDetailPath,
                     MessagingEndpoint.LandlordAcceptPath, MessagingEndpoint.LandlordRejectPath, MessagingEndpoint.ConversationsPath,
                     MessagingEndpoint.MessagesPath, MessagingEndpoint.ReadPath,
                 })
        {
            Assert.True(paths.TryGetProperty(messagingPath, out _), messagingPath);
        }

        // NV-18: las 9 operaciones de convenios y Mi renta están publicadas.
        foreach (var rentalPath in new[]
                 {
                     RentalsEndpoint.LandlordPath, RentalsEndpoint.LandlordDetailPath, RentalsEndpoint.LandlordCodePath,
                     RentalsEndpoint.LandlordCancelPath, RentalsEndpoint.LandlordEndPath, RentalsEndpoint.PreviewPath,
                     RentalsEndpoint.AcceptPath, RentalsEndpoint.RentalPath,
                 })
        {
            Assert.True(paths.TryGetProperty(rentalPath, out _), rentalPath);
        }

        // NV-20: las 4 operaciones de la bandeja están publicadas.
        foreach (var notificationPath in new[]
                 {
                     NotificationsEndpoint.BasePath, NotificationsEndpoint.CountPath,
                     NotificationsEndpoint.ReadPath, NotificationsEndpoint.ReadAllPath,
                 })
        {
            Assert.True(paths.TryGetProperty(notificationPath, out _), notificationPath);
        }

        // NV-19: las 7 operaciones de reseñas están publicadas.
        foreach (var reviewPath in new[]
                 {
                     ReviewsEndpoint.RoomReviewsPath, ReviewsEndpoint.EligibilityPath, ReviewsEndpoint.ReviewPath,
                     ReviewsEndpoint.MinePath, ReviewsEndpoint.HidePath, ReviewsEndpoint.UnhidePath,
                 })
        {
            Assert.True(paths.TryGetProperty(reviewPath, out _), reviewPath);
        }

        Assert.True(
            paths.TryGetProperty(
                PropertyEndpoint.ListPath,
                out _));

        Assert.True(
            paths.TryGetProperty(
                PropertyEndpoint.DetailPath,
                out _));

        Assert.True(
            paths.TryGetProperty(
                UniversityEndpoint.ListPath,
                out _));

        Assert.True(
            paths.TryGetProperty(
                UniversityEndpoint.DetailPath,
                out _));

        Assert.DoesNotContain(
            "WeatherForecast",
            first,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "SigningKey",
            first);

        Assert.DoesNotContain(
            "ConnectionString",
            first);
    }

    [Theory]
    [InlineData("/api/v2/foundation-test")]
    [InlineData("/weatherforecast")]
    [InlineData("/api/v1/auth/register/not-a-route")]
    [InlineData("/api/v1/auth/login/not-a-route")]
    [InlineData("/api/v1/auth/logout/not-a-route")]
    [InlineData("/api/v1/properties/not-a-route/extra")]
    [InlineData("/api/v1/universities/not-a-route/extra")]
    public async Task Unsupported_or_unimplemented_routes_are_not_exposed(
        string path)
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();

        using var response =
            await client.GetAsync(path);

        await ErrorContractTests.AssertProblem(
            response,
            404);
    }

    [Fact]
    public async Task Openapi_is_not_publicly_enabled_in_production()
    {
        using var factory =
            new FoundationFactory(
                environment: "Production");

        using var client =
            factory.CreateHttpsClient();

        using var response =
            await client.GetAsync(
                "/openapi/v1.json");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
