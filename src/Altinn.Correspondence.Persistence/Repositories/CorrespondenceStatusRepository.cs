using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories;

public class CorrespondenceStatusRepository(ApplicationDbContext context, ILogger<ICorrespondenceStatusRepository> logger) : ICorrespondenceStatusRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Guid> AddCorrespondenceStatus(CorrespondenceStatusEntity status, CancellationToken cancellationToken)
    {
        logger.LogInformation("Adding {Status} status for correspondence {CorrespondenceId}", status.StatusText, status.CorrespondenceId);
        await _context.CorrespondenceStatuses.AddAsync(status, cancellationToken);
        await _context.SaveChangesAsync();
        return status.Id;
    }

    public async Task<List<CorrespondenceStatusEntity>> AddCorrespondenceStatuses(List<CorrespondenceStatusEntity> correspondenceStatusEntities, CancellationToken cancellationToken)
    {
        await _context.CorrespondenceStatuses.AddRangeAsync(correspondenceStatusEntities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return correspondenceStatusEntities;
    }

    public async Task<Guid> AddCorrespondenceStatusFetched(CorrespondenceStatusFetchedEntity status, CancellationToken cancellationToken)
    {
        logger.LogInformation("Adding fetched {Status} status for correspondence {CorrespondenceId}", status.StatusText, status.CorrespondenceId);
        await _context.CorrespondenceFetches.AddAsync(status, cancellationToken);
        await _context.SaveChangesAsync();
        return status.Id;
    }

    public async Task<List<CorrespondenceStatusFetchedEntity>> GetCorrespondenceStatusesFetched(Guid correspondenceId, CancellationToken cancellationToken)
    {
        return await _context.CorrespondenceFetches
            .AsNoTracking()
            .Where(s => s.CorrespondenceId == correspondenceId)
            .OrderBy(s => s.StatusChanged)
            .ToListAsync(cancellationToken);
    }
}