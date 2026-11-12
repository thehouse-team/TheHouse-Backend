using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-18: doble en memoria de `agreements` con la semántica de MongoDB (un
// convenio abierto por cuarto, reemplazo condicional por `updatedAt`, suma
// atómica de intentos fallidos). Se comparte entre unitarias e integración.
public sealed class InMemoryAgreementRepository : IAgreementRepository
{
    private readonly object gate = new();

    public Dictionary<string, Agreement> Agreements { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<Agreement?> CreateAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (agreement.IsOpen && Agreements.Values.Any(item => item.IsOpen && item.RoomId == agreement.RoomId))
            {
                return Task.FromResult<Agreement?>(null);
            }

            Agreements[agreement.Id] = agreement;
            return Task.FromResult<Agreement?>(agreement);
        }
    }

    public Task<Agreement?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Agreements.GetValueOrDefault(id));
        }
    }

    public Task<Agreement?> FindByCodeHashAsync(string codeHash, CancellationToken cancellationToken) =>
        FindBy(item => item.Code?.CodeHash == codeHash, cancellationToken);

    public Task<Agreement?> FindByQrTokenHashAsync(string qrTokenHash, CancellationToken cancellationToken) =>
        FindBy(item => item.Code?.QrTokenHash == qrTokenHash, cancellationToken);

    public Task<bool> ReplaceIfUnchangedAsync(Agreement agreement, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Agreements.TryGetValue(agreement.Id, out var current) || current.UpdatedAt != expectedUpdatedAt)
            {
                return Task.FromResult(false);
            }

            if (agreement.IsOpen && Agreements.Values.Any(item => item.Id != agreement.Id && item.IsOpen && item.RoomId == agreement.RoomId))
            {
                return Task.FromResult(false);
            }

            Agreements[agreement.Id] = agreement;
            return Task.FromResult(true);
        }
    }

    public Task RecordFailedAttemptAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Agreements.TryGetValue(id, out var current) && current.Code is { } code)
            {
                Agreements[id] = current with { Code = code with { FailedAttempts = code.FailedAttempts + 1 } };
            }
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Agreement> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        AgreementStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var all = Agreements.Values
                .Where(item => item.OwnerId == ownerId && (status is null || item.Status == status) && (roomId is null || item.RoomId == roomId))
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<(IReadOnlyList<Agreement>, long)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), all.Length));
        }
    }

    public Task<Agreement?> GetActiveForStudentAsync(string studentId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Agreements.Values
                .Where(item => item.StudentId == studentId && item.Status == AgreementStatus.Active)
                .OrderByDescending(item => item.ActivatedAt)
                .FirstOrDefault());
        }
    }

    public Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Agreements.Values.Any(item =>
                item.StudentId == studentId && item.RoomId == roomId && item.Status is AgreementStatus.Active or AgreementStatus.Ended));
        }
    }

    private Task<Agreement?> FindBy(Func<Agreement, bool> predicate, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var matches = Agreements.Values.Where(predicate).OrderByDescending(item => item.UpdatedAt).ToArray();
            return Task.FromResult(matches.FirstOrDefault(item => item.Status == AgreementStatus.Pending) ?? matches.FirstOrDefault());
        }
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}
