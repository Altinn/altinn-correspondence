using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
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
        await _context.SaveChangesAsync(cancellationToken);
        return status.Id;
    }

    public async Task<Guid> AddCorrespondenceStatusForSync(CorrespondenceStatusEntity status, CancellationToken cancellationToken)
    {
        logger.LogDebug("Adding {Status} status for correspondence {CorrespondenceId} (sync operation)", status.StatusText, status.CorrespondenceId);
        _context.CorrespondenceStatuses.Add(status);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return status.Id;
        }
        catch (DbUpdateException ex) when (ex.IsPostgresUniqueViolation())
        {
            // Just let duplicates fail silently in race conditions, the Empty GUID will be used by the caller to determine that the status was not added, and the log will contain the details of the existing status.
            logger.LogInformation(
                "Status event already exists for correspondence {CorrespondenceId}. Status: {Status}, StatusChanged: {StatusChanged}, PartyUuid: {PartyUuid}. Skipping duplicate.",
                status.CorrespondenceId, status.Status, status.StatusChanged, status.PartyUuid);
            
            _context.Entry(status).State = EntityState.Detached;
            return Guid.Empty;
        }
    }

    public async Task<Guid> AddCorrespondenceStatusFetched(CorrespondenceStatusFetchedEntity status, CancellationToken cancellationToken)
    {
        logger.LogDebug("Adding fetched {Status} status for correspondence {CorrespondenceId}", status.StatusText, status.CorrespondenceId);
        await _context.CorrespondenceFetches.AddAsync(status, cancellationToken);
        await _context.SaveChangesAsync();
        return status.Id;
    }

    public async Task<List<CorrespondenceStatusEntity>> GetStatusesByCorrespondenceId(Guid correspondenceId, CancellationToken cancellationToken)
    {
        return await _context.CorrespondenceStatuses
            .AsNoTracking()
            .Where(s => s.CorrespondenceId == correspondenceId)
            .ToListAsync(cancellationToken);
    }
}