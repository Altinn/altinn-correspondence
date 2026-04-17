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
        logger.LogDebug("Adding {Status} status for correspondence {CorrespondenceId}", status.StatusText, status.CorrespondenceId);
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
        logger.LogDebug("Adding fetched {Status} status for correspondence {CorrespondenceId}", status.StatusText, status.CorrespondenceId);
        await _context.CorrespondenceFetches.AddAsync(status, cancellationToken);
        await _context.SaveChangesAsync();
        return status.Id;
    }

    public async Task<List<CorrespondenceStatusFetchedEntity>> GetBulkFetchStatusesWindowAfter(int windowSize, DateTimeOffset? afterStatusChanged, Guid? afterId, CancellationToken cancellationToken)
    {
        var query = _context.CorrespondenceFetches
            .OrderBy(s => s.StatusChanged)
            .ThenBy(s => s.Id)
            .AsQueryable();

        if (afterStatusChanged != null && afterId != null)
        {
            query = query.Where(s =>
                s.StatusChanged > afterStatusChanged ||
                (s.StatusChanged == afterStatusChanged && s.Id.CompareTo(afterId.Value) > 0));
        }

        return await query.Take(windowSize).ToListAsync(cancellationToken);
    }

    public async Task DeleteBulkFetchStatus(Guid id, CancellationToken cancellationToken)
    {
        var status = await _context.CorrespondenceFetches.FindAsync([id], cancellationToken);
        if (status != null)
        {
            _context.CorrespondenceFetches.Remove(status);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}