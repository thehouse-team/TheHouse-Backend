using TheHouse.Domain.Exceptions;

namespace TheHouse.Domain.Entities;

// NV-12: modo arrendador de una cuenta (un perfil por usuario).
public sealed class LandlordProfile
{
    public string UserId { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string RentalMode { get; init; } = string.Empty;

    public string? Rfc { get; init; }

    public string AcceptedTermsVersion { get; init; } = string.Empty;

    public DateTimeOffset AcceptedTermsAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public enum BuildingStatus
{
    Draft,
    InReview,
    Verified,

    // NV-21: rechazo terminal y suspensión por moderación.
    Rejected,
    Suspended,
}

public enum RoomStatus
{
    Setup,
    Ready,
    InReview,
    Available,
    Paused,
    Rented,
}

public sealed record BuildingAddress(
    string Street,
    string Number,
    string Neighborhood,
    string City,
    string State,
    string PostalCode);

// NV-12: edificio del arrendador. Las transiciones viven aquí; Verified lo
// otorga NV-21.
public sealed record Building
{
    public string Id { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public BuildingAddress Address { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string NearestUniversityId { get; init; } = string.Empty;

    public double DistanceToUniversityKm { get; init; }

    public int WalkingMinutes { get; init; }

    public string AllowedTenants { get; init; } = string.Empty;

    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();

    public string? Rules { get; init; }

    public BuildingStatus Status { get; init; }

    public long Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool AcceptsChanges => Status is BuildingStatus.Draft or BuildingStatus.Verified;

    // Solo `draft` y `verified` admiten cambios del dueño; en revisión → 409
    // `building_in_review`; rechazado o suspendido → 409 `building_state`.
    public Building EnsureEditable() => Status switch
    {
        BuildingStatus.Draft or BuildingStatus.Verified => this,
        BuildingStatus.InReview => throw new ConflictException("building_in_review"),
        _ => throw new ConflictException("building_state"),
    };

    public Building SubmitForReview(DateTimeOffset now) =>
        Status == BuildingStatus.Draft
            ? this with { Status = BuildingStatus.InReview, UpdatedAt = now }
            : throw new ConflictException("building_state");

    // NV-21: decisiones de revisión y moderación.
    public Building Approve(DateTimeOffset now) => Move(BuildingStatus.InReview, BuildingStatus.Verified, now);

    public Building RequestChanges(DateTimeOffset now) => Move(BuildingStatus.InReview, BuildingStatus.Draft, now);

    public Building Reject(DateTimeOffset now) => Move(BuildingStatus.InReview, BuildingStatus.Rejected, now);

    public Building Suspend(DateTimeOffset now) => Move(BuildingStatus.Verified, BuildingStatus.Suspended, now);

    public Building Unsuspend(DateTimeOffset now) => Move(BuildingStatus.Suspended, BuildingStatus.Verified, now);

    private Building Move(BuildingStatus from, BuildingStatus to, DateTimeOffset now) =>
        Status == from
            ? this with { Status = to, UpdatedAt = now }
            : throw new ConflictException("building_state");
}

// NV-12: cuarto de un edificio. `Setup` ↔ `Ready` se deriva de los datos;
// `InReview`, `Available` y `Rented` llegan por publicación, NV-21 y NV-18.
public sealed record Room
{
    public string Id { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public int Number { get; init; }

    public int Floor { get; init; }

    public int? Price { get; init; }

    public decimal? SizeM2 { get; init; }

    public string? Type { get; init; }

    public string? Bathroom { get; init; }

    public IReadOnlyList<string> Furniture { get; init; } = Array.Empty<string>();

    public DateOnly? AvailableFrom { get; init; }

    public string? MinContract { get; init; }

    public RoomStatus Status { get; init; }

    public long Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsComplete =>
        Price is not null &&
        SizeM2 is not null &&
        Type is not null &&
        Bathroom is not null &&
        AvailableFrom is not null &&
        MinContract is not null;

    // Tras editar datos: `Setup`/`Ready` se recalculan; los demás estados no cambian.
    public Room WithDerivedStatus() =>
        Status is RoomStatus.Setup or RoomStatus.Ready
            ? this with { Status = IsComplete ? RoomStatus.Ready : RoomStatus.Setup }
            : this;

    public Room EnsureEditable() =>
        Status == RoomStatus.InReview ? throw new ConflictException("room_state") : this;

    public Room Pause(DateTimeOffset now) =>
        Status == RoomStatus.Available
            ? this with { Status = RoomStatus.Paused, UpdatedAt = now }
            : throw new ConflictException("room_state");

    public Room Resume(DateTimeOffset now) =>
        Status == RoomStatus.Paused
            ? this with { Status = RoomStatus.Available, UpdatedAt = now }
            : throw new ConflictException("room_state");
}
