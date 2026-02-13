using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceForwardingEventRepository(ApplicationDbContext context, ILogger<ICorrespondenceStatusRepository> logger) : ICorrespondenceForwardingEventRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<CorrespondenceForwardingEventEntity>> AddForwardingEvents(List<CorrespondenceForwardingEventEntity> correspondenceForwardingEventEntities, CancellationToken cancellationToken)
    {
        await _context.CorrespondenceForwardingEvents.AddRangeAsync(correspondenceForwardingEventEntities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return correspondenceForwardingEventEntities;
    }

    public async Task<CorrespondenceForwardingEventEntity> GetForwardingEvent(Guid forwardingEventId, CancellationToken cancellationToken)
    {
        var correspondenceForwardingEventQuery = _context.CorrespondenceForwardingEvents.AsSplitQuery().Include(c => c.Correspondence).ThenInclude(c => c.Content).AsQueryable();

        var forwardingEvent = await correspondenceForwardingEventQuery.SingleOrDefaultAsync(c => c.Id == forwardingEventId, cancellationToken);
        return forwardingEvent ?? throw new KeyNotFoundException($"Could not find correspondence forwarding event with id {forwardingEventId}");
    }
}