using Altinn.Correspondence.Core.Models.Entities;
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
        return await _context.CorrespondenceDeleteEvents
                .AsNoTracking() // Is used in cleanup operations, no need to track
                .Where(c => c.CorrespondenceId == correspondenceId)
                .ToListAsync(cancellationToken);
    }
}