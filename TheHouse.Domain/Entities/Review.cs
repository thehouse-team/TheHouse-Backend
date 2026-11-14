using TheHouse.Domain.Exceptions;

namespace TheHouse.Domain.Entities;

// NV-19: base de la reseña (RF-85: quien visitó o rentó).
public static class ReviewBases
{
    public const string Visit = "visit";

    public const string Rental = "rental";
}

public enum ReviewStatus
{
    Published,
    Hidden,
}

// 4 criterios del arrendador, de 1 a 5 (prototipo «Calificar estancia»).
public sealed record LandlordScores(int Communication, int Attention, int Compliance, int Respect)
{
    public double Average => Math.Round((Communication + Attention + Compliance + Respect) / 4.0, 1, MidpointRounding.AwayFromZero);
}

// 5 criterios de la propiedad, de 1 a 5.
public sealed record PropertyScores(int Cleanliness, int Location, int Value, int Condition, int Services)
{
    public double Average => Math.Round((Cleanliness + Location + Value + Condition + Services) / 5.0, 1, MidpointRounding.AwayFromZero);
}

// NV-19: reseña de un cuarto y de su arrendador. Una por autor y cuarto;
// el autor la edita 30 días; un admin la oculta o la vuelve a mostrar.
public sealed record Review
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromDays(30);

    public string Id { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string AuthorId { get; init; } = string.Empty;

    public string Basis { get; init; } = ReviewBases.Visit;

    public LandlordScores Landlord { get; init; } = new(1, 1, 1, 1);

    public PropertyScores Property { get; init; } = new(1, 1, 1, 1, 1);

    public string? Comment { get; init; }

    public ReviewStatus Status { get; init; } = ReviewStatus.Published;

    public bool Edited { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? HiddenAt { get; init; }

    public DateTimeOffset EditableUntil => CreatedAt + EditWindow;

    public Review Edit(LandlordScores? landlord, PropertyScores? property, string? comment, bool changeComment, DateTimeOffset now) =>
        now <= EditableUntil
            ? this with
            {
                Landlord = landlord ?? Landlord,
                Property = property ?? Property,
                Comment = changeComment ? comment : Comment,
                Edited = true,
                UpdatedAt = now,
            }
            : throw new ConflictException("review_locked");

    public Review Hide(DateTimeOffset now) =>
        Status == ReviewStatus.Published
            ? this with { Status = ReviewStatus.Hidden, HiddenAt = now, UpdatedAt = now }
            : throw new ConflictException("review_state");

    public Review Unhide(DateTimeOffset now) =>
        Status == ReviewStatus.Hidden
            ? this with { Status = ReviewStatus.Published, HiddenAt = null, UpdatedAt = now }
            : throw new ConflictException("review_state");
}

// Promedios calculados al leer desde las reseñas publicadas.
public sealed record ReviewAverages(
    int Count,
    double? Communication,
    double? Attention,
    double? Compliance,
    double? Respect,
    double? Cleanliness,
    double? Location,
    double? Value,
    double? Condition,
    double? Services);
