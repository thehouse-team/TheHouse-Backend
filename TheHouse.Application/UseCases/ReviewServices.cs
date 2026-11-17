using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Application.Contracts.Reviews;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-19: reglas de reseñas.
public static class ReviewRules
{
    public const int MinimumScore = 1;

    public const int MaximumScore = 5;

    public const int MaximumComment = 1000;

    private const int VisitPageSize = 50;

    private const int MaximumVisitPages = 20;

    public static int Score(int? value, string field) =>
        value is { } score and >= MinimumScore and <= MaximumScore
            ? score
            : throw new RequestValidationException(field, $"{field} must be an integer from 1 to 5.");

    public static LandlordScores Landlord(LandlordScoresInput input) =>
        new(
            Score(input.Communication, "landlord.communication"),
            Score(input.Attention, "landlord.attention"),
            Score(input.Compliance, "landlord.compliance"),
            Score(input.Respect, "landlord.respect"));

    public static PropertyScores Property(PropertyScoresInput input) =>
        new(
            Score(input.Cleanliness, "property.cleanliness"),
            Score(input.Location, "property.location"),
            Score(input.Value, "property.value"),
            Score(input.Condition, "property.condition"),
            Score(input.Services, "property.services"));

    public static string? Comment(string? value)
    {
        var comment = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return comment is { Length: > MaximumComment }
            ? throw new RequestValidationException("comment", "comment must have at most 1000 characters.")
            : comment;
    }

    public static double? Round(double? value) =>
        value is { } number ? Math.Round(number, 1, MidpointRounding.AwayFromZero) : null;

    public static double? Mean(params double?[] values) =>
        values.All(value => value is not null) ? Round(values.Average(value => value!.Value)) : null;

    // «Visitó»: visita `confirmed` a ese cuarto cuyo horario ya pasó (NV-17
    // no emite `completed`).
    public static async Task<bool> VisitedAsync(IVisitRepository visits, string userId, string roomId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var page = 1; page <= MaximumVisitPages; page++)
        {
            var (items, total) = await visits.ListAsync(userId, VisitStatus.Confirmed, page, VisitPageSize, cancellationToken);
            if (items.Any(visit => visit.RoomId == roomId && visit.ScheduledAt <= now))
            {
                return true;
            }

            if ((long)page * VisitPageSize >= total)
            {
                return false;
            }
        }

        return false;
    }
}

// NV-19 (RF-85): reseñas verificadas de cuarto y arrendador.
public sealed class RoomReviewService(
    IReviewRepository reviews,
    IVisitRepository visits,
    IRentalHistoryQuery rentals,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    IProfileRepository profiles,
    TimeProvider timeProvider)
{
    public async Task<ReviewEligibilityResponse> EligibilityAsync(string userId, string? roomId, CancellationToken cancellationToken)
    {
        var room = await LoadRoomAsync(roomId, cancellationToken);
        var (basis, reason, existing) = await CheckAsync(userId, room, cancellationToken);
        return new ReviewEligibilityResponse(basis is not null, basis, reason, existing?.Id, existing?.EditableUntil);
    }

    public async Task<ReviewResponse> CreateAsync(string userId, string? roomId, ReviewInput body, CancellationToken cancellationToken)
    {
        var landlord = ReviewRules.Landlord(body.Landlord ?? throw new RequestValidationException("landlord", "landlord is required."));
        var property = ReviewRules.Property(body.Property ?? throw new RequestValidationException("property", "property is required."));
        var comment = ReviewRules.Comment(body.Comment);
        var room = await LoadRoomAsync(roomId, cancellationToken);
        var (basis, reason, _) = await CheckAsync(userId, room, cancellationToken);
        if (reason == "already_reviewed")
        {
            throw new ConflictException("review_exists");
        }

        if (basis is null)
        {
            throw new ForbiddenException(reason!);
        }

        var now = timeProvider.GetUtcNow();
        var created = await reviews.CreateAsync(
            new Review
            {
                Id = MessagingIds.New(now),
                RoomId = room.Id,
                BuildingId = room.BuildingId,
                OwnerId = room.OwnerId,
                AuthorId = userId,
                Basis = basis,
                Landlord = landlord,
                Property = property,
                Comment = comment,
                Status = ReviewStatus.Published,
                CreatedAt = now,
                UpdatedAt = now,
            },
            cancellationToken) ?? throw new ConflictException("review_exists");
        return await ToResponseAsync(created, room, cancellationToken);
    }

    public async Task<ReviewResponse> UpdateAsync(string userId, string? id, ReviewInput body, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        var landlord = body.Landlord is null ? null : ReviewRules.Landlord(body.Landlord);
        var property = body.Property is null ? null : ReviewRules.Property(body.Property);
        var comment = ReviewRules.Comment(body.Comment);
        if (landlord is null && property is null && body.Comment is null)
        {
            throw new RequestValidationException("body", "Send landlord, property or comment.");
        }

        var review = await reviews.GetAsync(id!.ToLowerInvariant(), cancellationToken);
        if (review is null || review.AuthorId != userId)
        {
            throw new NotFoundException("review");
        }

        var edited = review.Edit(landlord, property, comment, changeComment: body.Comment is not null, timeProvider.GetUtcNow());
        if (!await reviews.ReplaceIfUnchangedAsync(edited, review.UpdatedAt, cancellationToken))
        {
            throw new ConflictException("review_state");
        }

        return await ToResponseAsync(edited, null, cancellationToken);
    }

    public async Task<RoomReviewListResponse> ListAsync(string? roomId, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var room = await LoadRoomAsync(roomId, cancellationToken);
        var (items, total) = await reviews.ListPublishedByRoomAsync(room.Id, page, pageSize, cancellationToken);
        var responses = new List<ReviewResponse>(items.Count);
        foreach (var item in items)
        {
            responses.Add(await ToResponseAsync(item, room, cancellationToken));
        }

        var byRoom = await reviews.AveragesByRoomAsync(room.Id, cancellationToken);
        var byOwner = await reviews.AveragesByOwnerAsync(room.OwnerId, cancellationToken);
        var summary = new ReviewSummary(
            new ReviewAverage(ReviewRules.Mean(byRoom.Cleanliness, byRoom.Location, byRoom.Value, byRoom.Condition, byRoom.Services), byRoom.Count),
            new ReviewAverage(ReviewRules.Mean(byOwner.Communication, byOwner.Attention, byOwner.Compliance, byOwner.Respect), byOwner.Count),
            new CriteriaAverages(
                ReviewRules.Round(byRoom.Communication),
                ReviewRules.Round(byRoom.Attention),
                ReviewRules.Round(byRoom.Compliance),
                ReviewRules.Round(byRoom.Respect),
                ReviewRules.Round(byRoom.Cleanliness),
                ReviewRules.Round(byRoom.Location),
                ReviewRules.Round(byRoom.Value),
                ReviewRules.Round(byRoom.Condition),
                ReviewRules.Round(byRoom.Services)));
        return new RoomReviewListResponse(summary, responses, page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<MyReviewListResponse> ListMineAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (items, total) = await reviews.ListByAuthorAsync(userId, page, pageSize, cancellationToken);
        var responses = new List<MyReviewResponse>(items.Count);
        foreach (var item in items)
        {
            var review = await ToResponseAsync(item, null, cancellationToken);
            responses.Add(new MyReviewResponse(
                review.Id, review.Room, review.Author, review.Basis, review.Landlord, review.Property,
                review.LandlordRating, review.PropertyRating, review.Comment, review.Edited,
                item.Status.ToString().ToLowerInvariant(), review.CreatedAt, review.UpdatedAt));
        }

        return new MyReviewListResponse(responses, page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    // Elegible: no es el dueño, no la ha reseñado y visitó o rentó el cuarto.
    private async Task<(string? Basis, string? Reason, Review? Existing)> CheckAsync(string userId, Room room, CancellationToken cancellationToken)
    {
        if (room.OwnerId == userId)
        {
            return (null, "own_room", null);
        }

        if (await reviews.FindAsync(userId, room.Id, cancellationToken) is { } existing)
        {
            return (null, "already_reviewed", existing);
        }

        if (await rentals.HasRentedAsync(userId, room.Id, cancellationToken))
        {
            return (ReviewBases.Rental, null, null);
        }

        return await ReviewRules.VisitedAsync(visits, userId, room.Id, timeProvider.GetUtcNow(), cancellationToken)
            ? (ReviewBases.Visit, null, null)
            : (null, "no_stay", null);
    }

    private async Task<Room> LoadRoomAsync(string? roomId, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(roomId, "roomId");
        return await rooms.GetAsync(roomId!.ToLowerInvariant(), cancellationToken) ?? throw new NotFoundException("room");
    }

    private async Task<ReviewResponse> ToResponseAsync(Review review, Room? room, CancellationToken cancellationToken)
    {
        room ??= await rooms.GetAsync(review.RoomId, cancellationToken);
        var building = await buildings.GetAsync(review.BuildingId, cancellationToken);
        var profile = await profiles.GetAsync(review.AuthorId, cancellationToken);
        var name = string.IsNullOrWhiteSpace(profile?.Name) ? "Estudiante" : MessagingRules.FirstName(profile!.Name);
        return new ReviewResponse(
            review.Id,
            new ReviewRoomSummary(
                review.RoomId,
                review.BuildingId,
                building is null || room is null ? "Cuarto" : $"{building.Name} · Cuarto {room.Number}",
                building?.Address.City ?? string.Empty),
            new ReviewAuthor(name),
            review.Basis,
            new LandlordScoresResponse(review.Landlord.Communication, review.Landlord.Attention, review.Landlord.Compliance, review.Landlord.Respect),
            new PropertyScoresResponse(review.Property.Cleanliness, review.Property.Location, review.Property.Value, review.Property.Condition, review.Property.Services),
            review.Landlord.Average,
            review.Property.Average,
            review.Comment,
            review.Edited,
            review.CreatedAt,
            review.UpdatedAt);
    }
}

// NV-19 en NV-21: ocultar o volver a mostrar una reseña (rol `admin`), con
// motivo y bitácora. El módulo Moderation usa el puerto de Reviews.
public sealed class ReviewModerationService(StaffService staff, IReviewRepository reviews, TimeProvider timeProvider)
{
    public Task<ReviewStatusResponse> HideAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(actorId, id, request, (review, now) => review.Hide(now), "review.hidden", cancellationToken);

    public Task<ReviewStatusResponse> UnhideAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(actorId, id, request, (review, now) => review.Unhide(now), "review.unhidden", cancellationToken);

    private async Task<ReviewStatusResponse> ChangeAsync(
        string actorId,
        string? id,
        ReasonRequest request,
        Func<Review, DateTimeOffset, Review> change,
        string action,
        CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Admin, cancellationToken);
        var reason = ModerationRules.Reason(request.Reason, required: true);
        var review = await reviews.GetAsync(ModerationRules.ObjectId("id", id), cancellationToken) ?? throw new NotFoundException("review");
        var changed = change(review, timeProvider.GetUtcNow());
        if (!await reviews.ReplaceIfUnchangedAsync(changed, review.UpdatedAt, cancellationToken))
        {
            throw new ConflictException("review_state");
        }

        await staff.AuditAsync(actorId, action, "review", review.Id, reason, cancellationToken);
        return new ReviewStatusResponse(changed.Id, changed.Status.ToString().ToLowerInvariant());
    }
}
