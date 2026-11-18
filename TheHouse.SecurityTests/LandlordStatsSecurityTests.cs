using TheHouse.Application.Contracts.Analytics;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-25 (RNF-11): las estadísticas solo llevan agregados; el hash de quien
// vio no es reversible ni se puede unir entre días; códigos en español.
public sealed class LandlordStatsSecurityTests
{
    [Theory]
    [InlineData(typeof(LandlordStatsResponse))]
    [InlineData(typeof(StatsTotals))]
    [InlineData(typeof(StatsDay))]
    [InlineData(typeof(RoomStatsItem))]
    [InlineData(typeof(StatsRecommendation))]
    public void Stats_responses_never_carry_people(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "UserId", "StudentId", "ViewerId", "ActorHash", "Email", "Phone", "Name", "Students", "Viewers" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Actor_hash_depends_on_the_daily_salt_and_hides_the_user()
    {
        const string user = "6650000000000000000000a1";
        var first = LandlordStatsRules.ActorHash(new byte[32], user);
        var second = LandlordStatsRules.ActorHash([.. Enumerable.Repeat((byte)7, 32)], user);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain(user, first, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.DoesNotContain(nameof(RoomView.ActorHash), typeof(RoomStatsItem).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Stats_are_bounded_and_explained_in_spanish()
    {
        Assert.Equal(90, LandlordStatsRules.MaximumDays);
        Assert.Equal(20, LandlordStatsRules.MaximumRecommendations);
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor("landlord_required")));
        Assert.StartsWith("/api/v1/landlord/stats", LandlordStatsEndpoint.RoomsPath, StringComparison.Ordinal);
    }
}
