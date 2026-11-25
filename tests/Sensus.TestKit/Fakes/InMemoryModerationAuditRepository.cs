using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de auditoría de moderación (Spec 021) y copias anonimizadas (Spec 061).</summary>
public sealed class InMemoryModerationAuditRepository : IModerationAuditRepository
{
    private readonly HashSet<Guid> _ids = [];
    private readonly List<ModerationAuditRecord> _items = [];
    private readonly object _lock = new();

    public Task<bool> AddIfNotExistsAsync(
        ModerationAudit entry,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_ids.Add(entry.Id));
        }
    }

    public Task AddAsync(ModerationAuditRecord record, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<ModerationAuditRecord> Items, long TotalCount)> ListAsync(
        Guid? targetId,
        ModerationTargetType? targetType,
        ModerationVerdict? verdict,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IEnumerable<ModerationAuditRecord> query = _items;

            if (targetId.HasValue)
            {
                query = query.Where(x => x.TargetId == targetId.Value);
            }

            if (targetType.HasValue)
            {
                query = query.Where(x => x.TargetType == targetType.Value);
            }

            if (verdict.HasValue)
            {
                query = query.Where(x => x.Verdict == verdict.Value);
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
            }

            var list = query.OrderByDescending(x => x.CreatedAtUtc).ToList();
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult<(IReadOnlyList<ModerationAuditRecord> Items, long TotalCount)>((paged, list.Count));
        }
    }
}
