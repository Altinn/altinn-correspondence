using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories;

public interface IIdempotencyKeyRepository
{
    Task<IdempotencyKeyEntity?> GetByCorrespondenceAndAttachmentAndActionAsync(Guid correspondenceId, Guid? attachmentId, StatusAction action, CancellationToken cancellationToken);
    Task<IdempotencyKeyEntity> CreateAsync(IdempotencyKeyEntity idempotencyKey, CancellationToken cancellationToken);
    Task CreateRangeAsync(IEnumerable<IdempotencyKeyEntity> idempotencyKeys, CancellationToken cancellationToken);
} 