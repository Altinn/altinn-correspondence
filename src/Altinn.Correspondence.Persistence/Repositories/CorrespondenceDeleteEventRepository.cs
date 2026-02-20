using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceDeleteEventRepository(ApplicationDbContext context, ILogger<CorrespondenceDeleteEventRepository> logger) : ICorrespondenceDeleteEventRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Guid> AddDeleteEventForSync(CorrespondenceDeleteEventEntity correspondenceDeleteEventEntity, CancellationToken cancellationToken)
    {
        try
        {
            await _context.AddAsync(correspondenceDeleteEventEntity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondenceDeleteEventEntity.Id;
        }
        catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
        {
            logger.LogInformation(
                "Delete event already exists for correspondence {CorrespondenceId}. EventType: {EventType}, EventOccurred: {EventOccurred}, PartyUuid: {PartyUuid}. Skipping duplicate.",
                correspondenceDeleteEventEntity.CorrespondenceId,
                correspondenceDeleteEventEntity.EventType,
                correspondenceDeleteEventEntity.EventOccurred,
                correspondenceDeleteEventEntity.PartyUuid);

            // Return empty ID to indicate duplicate
            return Guid.Empty;
        }
    }

    public async Task<List<CorrespondenceDeleteEventEntity>> GetDeleteEventsForCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
    {
        return await _context.CorrespondenceDeleteEvents
                .AsNoTracking() // Is used in cleanup operations, no need to track
                .Where(c => c.CorrespondenceId == correspondenceId)
                .ToListAsync(cancellationToken);
    }
}