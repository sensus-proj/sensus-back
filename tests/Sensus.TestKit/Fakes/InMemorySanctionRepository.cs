using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de sanciones para pruebas sin MongoDB.</summary>
public sealed class InMemorySanctionRepository : ISanctionRepository
{
    private readonly List<Sanction> _sanctions = [];

    public Task<Sanction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sanctions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Sanction>> GetAllByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sanction>>(_sanctions.Where(s => s.UserId == userId).ToList());

    public Task<Sanction?> GetByMediaIdAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sanctions.FirstOrDefault(s => s.Evidence.MediaId == mediaId));

    public Task<IReadOnlyList<Sanction>> GetActiveByUserAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sanction>>(
            _sanctions.Where(s => s.UserId == userId && s.IsActive(nowUtc)).ToList());

    public Task<int> CountNotRevertedSinceAsync(
        Guid userId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_sanctions.Count(s =>
            s.UserId == userId && s.RevertedAtUtc is null && s.StartsAtUtc >= sinceUtc));

    public Task<(IReadOnlyList<Sanction> Items, long TotalCount)> ListAsync(
        Guid? userId,
        SanctionState? state,
        DateTime nowUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        List<Sanction> filtered = _sanctions
            .Where(s => userId is null || s.UserId == userId)
            .Where(s => state is null || s.GetState(nowUtc) == state)
            .ToList();

        IReadOnlyList<Sanction> items = filtered
            .OrderByDescending(s => s.StartsAtUtc)
            .Skip((Math.Max(1, page) - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();
        return Task.FromResult<(IReadOnlyList<Sanction>, long)>((items, filtered.Count));
    }

    public Task AddAsync(Sanction sanction, CancellationToken cancellationToken = default)
    {
        _sanctions.Add(sanction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Sanction sanction, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
