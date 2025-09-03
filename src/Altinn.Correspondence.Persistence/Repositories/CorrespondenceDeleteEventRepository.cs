using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceDeleteEventRepository(ApplicationDbContext context, ILogger<CorrespondenceDeleteEventRepository> logger) : ICorrespondenceDeleteEventRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<CorrespondenceDeleteEventEntity> AddDeleteEvent(CorrespondenceDeleteEventEntity correspondenceDeleteEventEntity, CancellationToken cancellationToken)
    {
        await _context.AddAsync(correspondenceDeleteEventEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return correspondenceDeleteEventEntity;
    }

    public async Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var deleteEvents = _context.CorrespondenceDeleteEvents.AsNoTracking() // Is used in cleanup operations, no need to track
                .Where(c => c.CorrespondenceId == correspondenceId)
                .AsQueryable();
        
        return await deleteEvents.ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, bool>> GetSoftDeleteStates(IReadOnlyCollection<Guid> correspondenceIds, CancellationToken cancellationToken)
    {
        if (correspondenceIds == null || correspondenceIds.Count == 0) return new Dictionary<Guid, bool>();
        const int chunkSize = 1000;
        var states = new Dictionary<Guid, bool>(correspondenceIds.Count);

        foreach (var chunk in correspondenceIds.Chunk(chunkSize))
        {
            var rows = await _context.CorrespondenceDeleteEvents
                .AsNoTracking()
                .Where(e => chunk.Contains(e.CorrespondenceId))
                .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient
                         || e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient)
                .GroupBy(e => e.CorrespondenceId)
                .Select(g => new
                {
                    Id = g.Key,
                    LatestSoft = g.Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient)
                                  .Max(e => (DateTimeOffset?)e.EventOccurred),
                    LatestRestore = g.Where(e => e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient)
                                     .Max(e => (DateTimeOffset?)e.EventOccurred),
                })
                .ToListAsync(cancellationToken);

            foreach (var r in rows)
            {
                var isSoftDeleted = r.LatestSoft != null && (r.LatestRestore == null || r.LatestSoft > r.LatestRestore);
                states[r.Id] = isSoftDeleted;
            }
        }

        return states;
    }
}