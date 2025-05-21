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

    public async Task<IdempotencyKeyEntity?> GetByCorrespondenceAndAttachmentAndActionAsync(
        Guid correspondenceId, 
        Guid? attachmentId, 
        StatusAction? action,
        IdempotencyType idempotencyType,
        CancellationToken cancellationToken)
    {
        return await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => 
                k.CorrespondenceId == correspondenceId && 
                k.AttachmentId == attachmentId && 
                k.StatusAction == action &&
                k.IdempotencyType == idempotencyType,
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

    public async Task<IdempotencyKeyEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var idempotencyKey = await _dbContext.IdempotencyKeys.FindAsync(new object[] { id }, cancellationToken);
        if (idempotencyKey != null)
        {
            _dbContext.IdempotencyKeys.Remove(idempotencyKey);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
} 