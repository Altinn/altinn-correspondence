using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories;

public class IdempotencyKeyRepository : IIdempotencyKeyRepository
{
    private readonly ApplicationDbContext _dbContext;

    public IdempotencyKeyRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IdempotencyKeyEntity?> GetByCorrespondenceAndAttachmentAndActionAsync(Guid correspondenceId, Guid? attachmentId, StatusAction action, CancellationToken cancellationToken)
    {
        return await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => 
                k.CorrespondenceId == correspondenceId && 
                k.AttachmentId == attachmentId && 
                k.StatusAction == action,
                cancellationToken);
    }

    public async Task<IdempotencyKeyEntity> CreateAsync(IdempotencyKeyEntity idempotencyKey, CancellationToken cancellationToken)
    {
        await _dbContext.IdempotencyKeys.AddAsync(idempotencyKey, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return idempotencyKey;
    }

    public async Task CreateRangeAsync(IEnumerable<IdempotencyKeyEntity> idempotencyKeys, CancellationToken cancellationToken)
    {
        await _dbContext.IdempotencyKeys.AddRangeAsync(idempotencyKeys, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
} 