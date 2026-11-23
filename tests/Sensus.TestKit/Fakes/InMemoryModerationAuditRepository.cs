using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de auditoría anonimizada de moderación (Spec 061).</summary>
public sealed class InMemoryModerationAuditRepository : IModerationAuditRepository
{
    private readonly HashSet<Guid> _ids = [];

    public Task<bool> AddIfNotExistsAsync(
        ModerationAudit entry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_ids.Add(entry.Id));
}
