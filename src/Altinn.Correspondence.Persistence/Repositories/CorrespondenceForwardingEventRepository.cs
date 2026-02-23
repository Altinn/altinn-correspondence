using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceForwardingEventRepository(ApplicationDbContext context, ILogger<ICorrespondenceForwardingEventRepository> logger) : ICorrespondenceForwardingEventRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Guid> AddForwardingEventForSync(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _context.CorrespondenceForwardingEvents.AddAsync(forwardingEvent, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return forwardingEvent.Id;
        }
        catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
        {
            logger.LogInformation(
                "Forwarding event already exists for correspondence {CorrespondenceId}. ForwardedOnDate: {ForwardedOnDate}, ForwardedByPartyUuid: {ForwardedByPartyUuid}. Skipping duplicate.",
                forwardingEvent.CorrespondenceId,
                forwardingEvent.ForwardedOnDate,
                forwardingEvent.ForwardedByPartyUuid);

            _context.Entry(forwardingEvent).State = EntityState.Detached;
            return Guid.Empty;
        }
    }

    public async Task<List<Guid>> AddForwardingEventsForSync(List<CorrespondenceForwardingEventEntity> forwardingEvents, CancellationToken cancellationToken)
    {
        var savedIds = new List<Guid>();
        
        try
        {
            await _context.CorrespondenceForwardingEvents.AddRangeAsync(forwardingEvents, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            // All events were saved successfully
            savedIds.AddRange(forwardingEvents.Select(fe => fe.Id));
        }
        catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
        {
            // If we have a unique constraint violation with batch insert, we need to insert one by one
            // to identify which events are duplicates
            logger.LogInformation("Batch insert failed due to duplicate events. Falling back to individual inserts.");
            
            // Detach all entities that were added
            foreach (var forwardingEvent in forwardingEvents)
            {
                _context.Entry(forwardingEvent).State = EntityState.Detached;
            }
            
            // Try inserting one by one
            foreach (var forwardingEvent in forwardingEvents)
            {
                try
                {
                    await _context.CorrespondenceForwardingEvents.AddAsync(forwardingEvent, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    savedIds.Add(forwardingEvent.Id);
                }
                catch (DbUpdateException individualEx) when (individualEx.IsPostgresUniqueViolation())
                {
                    logger.LogInformation(
                        "Forwarding event already exists for correspondence {CorrespondenceId}. ForwardedOnDate: {ForwardedOnDate}, ForwardedByPartyUuid: {ForwardedByPartyUuid}. Skipping duplicate.",
                        forwardingEvent.CorrespondenceId,
                        forwardingEvent.ForwardedOnDate,
                        forwardingEvent.ForwardedByPartyUuid);
                    
                    _context.Entry(forwardingEvent).State = EntityState.Detached;
                }
            }
        }
        
        return savedIds;
    }

    public async Task<CorrespondenceForwardingEventEntity> GetForwardingEvent(Guid forwardingEventId, CancellationToken cancellationToken)
    {
        var correspondenceForwardingEventQuery = _context.CorrespondenceForwardingEvents.AsSplitQuery().Include(c => c.Correspondence).ThenInclude(c => c.Content).AsQueryable(); 
        var forwardingEvent = await correspondenceForwardingEventQuery.SingleOrDefaultAsync(c => c.Id == forwardingEventId, cancellationToken);
        return forwardingEvent ?? throw new KeyNotFoundException($"Could not find correspondence forwarding event with id {forwardingEventId}");
    }

    public async Task SetDialogActivityId(Guid forwardingEventId, Guid dialogActivityId, CancellationToken cancellationToken)
    {
        var forwardingEvent = await _context.CorrespondenceForwardingEvents.SingleOrDefaultAsync(c => c.Id == forwardingEventId, cancellationToken);
        if (forwardingEvent == null)
        {
            throw new KeyNotFoundException($"Could not find correspondence forwarding event with id {forwardingEventId}");
        }
        forwardingEvent.DialogActivityId = dialogActivityId;
        _context.CorrespondenceForwardingEvents.Update(forwardingEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<CorrespondenceForwardingEventEntity>> GetForwardingEventsWithoutDialogActivityBatch(int count, DateTimeOffset lastProcessed, CancellationToken cancellationToken)
    {
        return await _context.CorrespondenceForwardingEvents
            .Where(e => e.DialogActivityId == null && e.ForwardedOnDate < lastProcessed)
            .OrderByDescending(e => e.ForwardedOnDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}