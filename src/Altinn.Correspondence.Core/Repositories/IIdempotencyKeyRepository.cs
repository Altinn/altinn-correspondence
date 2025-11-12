using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories;

public interface IIdempotencyKeyRepository
{
    /// <summary>
    /// Gets an idempotency key by id.
    /// </summary>
    /// <param name="id">The id of the idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IdempotencyKeyEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an idempotency key by correspondence id, attachment id and action.
    /// </summary>
    /// <param name="correspondenceId">The id of the correspondence.</param>
    /// <param name="attachmentId">The id of the attachment.</param>
    /// <param name="action">The action of the idempotency key.</param>
    /// <param name="idempotencyType">The type of idempotency key.</param>
    Task<IdempotencyKeyEntity?> GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
        Guid correspondenceId, 
        Guid? attachmentId, 
        StatusAction? action,
        IdempotencyType idempotencyType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IdempotencyKeyEntity> CreateAsync(IdempotencyKeyEntity idempotencyKey, CancellationToken cancellationToken);
    
    /// <summary>
    /// Creates a range of idempotency keys.
    /// </summary>
    /// <param name="idempotencyKeys">The idempotency keys to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CreateRangeAsync(IEnumerable<IdempotencyKeyEntity> idempotencyKeys, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an idempotency key if it exists. Otherwise does nothing.
    /// </summary>
    /// <param name="id">The id of the idempotency key to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes idempotency keys for the given correspondence ids. Returns number of deleted rows.
    /// </summary>
    Task<int> DeleteByCorrespondenceIds(IEnumerable<Guid> correspondenceIds, CancellationToken cancellationToken);
} 