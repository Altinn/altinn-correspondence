using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceForwardingEventRepository(ApplicationDbContext context, ILogger<ICorrespondenceForwardingEventRepository> logger) : ICorrespondenceForwardingEventRepository
{
    private readonly ApplicationDbContext _context = context;

    public Task<Guid> AddForwardingEventForSync(CorrespondenceForwardingEventEntity forwardingEvent, CancellationToken cancellationToken)
    {
        _context.CorrespondenceForwardingEvents.Add(forwardingEvent);
        return Task.FromResult(forwardingEvent.Id);
    }

    public async Task<CorrespondenceForwardingEventEntity> GetForwardingEvent(Guid forwardingEventId, CancellationToken cancellationToken)
    {
        var correspondenceForwardingEventQuery = _context.CorrespondenceForwardingEvents.AsSplitQuery()
            .Include(c => c.Correspondence)
            .ThenInclude(c => c.Content)
            .Include(c => c.Correspondence)
            .ThenInclude(c => c.ExternalReferences)
            .AsQueryable();

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
