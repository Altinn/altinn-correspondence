using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;

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
}