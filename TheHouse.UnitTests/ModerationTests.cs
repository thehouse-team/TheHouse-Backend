using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-21: transiciones de moderación, roles y validaciones.
public sealed class ModerationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Review_decisions_only_apply_to_buildings_in_review()
    {
        var inReview = new Building { Status = BuildingStatus.InReview };

        Assert.Equal(BuildingStatus.Verified, inReview.Approve(Now).Status);
        Assert.Equal(BuildingStatus.Draft, inReview.RequestChanges(Now).Status);
        Assert.Equal(BuildingStatus.Rejected, inReview.Reject(Now).Status);
        Assert.Equal("building_state", Assert.Throws<ConflictException>(() => new Building { Status = BuildingStatus.Draft }.Approve(Now)).Reason);
        Assert.Equal("building_state", Assert.Throws<ConflictException>(() => new Building { Status = BuildingStatus.Rejected }.Reject(Now)).Reason);
    }

    [Fact]
    public void Suspension_is_only_between_verified_and_suspended()
    {
        var verified = new Building { Status = BuildingStatus.Verified };

        Assert.Equal(BuildingStatus.Suspended, verified.Suspend(Now).Status);
        Assert.Equal(BuildingStatus.Verified, verified.Suspend(Now).Unsuspend(Now).Status);
        Assert.Throws<ConflictException>(() => new Building { Status = BuildingStatus.InReview }.Suspend(Now));
        Assert.Throws<ConflictException>(() => verified.Unsuspend(Now));
    }

    [Theory]
    [InlineData(BuildingStatus.Draft, null)]
    [InlineData(BuildingStatus.Verified, null)]
    [InlineData(BuildingStatus.InReview, "building_in_review")]
    [InlineData(BuildingStatus.Rejected, "building_state")]
    [InlineData(BuildingStatus.Suspended, "building_state")]
    public void Owner_edits_depend_on_the_building_state(BuildingStatus status, string? code)
    {
        var building = new Building { Status = status };

        if (code is null)
        {
            Assert.Same(building, building.EnsureEditable());
        }
        else
        {
            Assert.Equal(code, Assert.Throws<ConflictException>(() => building.EnsureEditable()).Reason);
        }
    }

    [Fact]
    public void Admin_role_includes_reviewer()
    {
        var admin = new StaffRoles("u1", [StaffRoles.Admin], Now, "cli");
        var reviewer = new StaffRoles("u2", [StaffRoles.Reviewer], Now, "cli");

        Assert.True(admin.Has(StaffRoles.Reviewer));
        Assert.True(reviewer.Has(StaffRoles.Reviewer));
        Assert.False(reviewer.Has(StaffRoles.Admin));
    }

    [Theory]
    [InlineData(null, false, null)]
    [InlineData("Bien", true, "error")]
    [InlineData("Motivo válido", true, "Motivo válido")]
    [InlineData(null, true, "error")]
    public void Reasons_have_5_to_500_characters(string? value, bool required, string? expected)
    {
        if (expected == "error")
        {
            Assert.Throws<RequestValidationException>(() => ModerationRules.Reason(value, required));
        }
        else
        {
            Assert.Equal(expected, ModerationRules.Reason(value, required));
        }
    }

    [Fact]
    public async Task Staff_service_requires_roles_and_audits_grants()
    {
        var moderation = new InMemoryModerationRepositories();
        var users = new SingleUser(new User { Id = "665f1c2a9b1e4a00000a11ce", Email = "alice@example.com" });
        var staff = new StaffService(moderation, moderation, users, new ManualTimeProvider(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() => staff.RequireAsync("665f1c2a9b1e4a00000a11ce", StaffRoles.Reviewer, CancellationToken.None));
        await staff.GrantByEmailAsync("Alice@Example.com", "reviewer", "cli", CancellationToken.None);
        await staff.RequireAsync("665f1c2a9b1e4a00000a11ce", StaffRoles.Reviewer, CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.RequireAsync("665f1c2a9b1e4a00000a11ce", StaffRoles.Admin, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => staff.GrantByEmailAsync("alice@example.com", "owner", "cli", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => staff.GrantByEmailAsync("nobody@example.com", "admin", "cli", CancellationToken.None));
        await staff.RevokeByEmailAsync("alice@example.com", "reviewer", "cli", CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => staff.RevokeByEmailAsync("alice@example.com", "reviewer", "cli", CancellationToken.None));

        Assert.Equal(new[] { "role.granted", "role.revoked" }, moderation.Audit.Select(item => item.Action));
        Assert.All(moderation.Audit, item => Assert.Equal("cli", item.ActorId));
    }

    private sealed class SingleUser(User user) : TheHouse.Application.Abstractions.Persistence.IUserRepository
    {
        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(user.Email == normalizedEmail ? user : null);

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id == id ? user : null);

        public Task<User> CreateAsync(User created, CancellationToken cancellationToken) => Task.FromResult(created);
    }
}
