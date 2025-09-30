using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceRepository(ApplicationDbContext context, ILogger<ICorrespondenceRepository> logger) : ICorrespondenceRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<CorrespondenceEntity> CreateCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddAsync(correspondence, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondence;
        }
        public async Task<List<CorrespondenceEntity>> CreateCorrespondences(List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken)
        {
            await _context.Correspondences.AddRangeAsync(correspondences, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return correspondences;
        }

        public async Task<List<Guid>> GetCorrespondences(
            string resourceId,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            string orgNo,
            CorrespondencesRoleType role,
            string? sendersReference,
            Guid? idempotentKey,
            CancellationToken cancellationToken)
        {
            var correspondences = _context.Correspondences
                .Where(c => c.ResourceId == resourceId)             // Correct id
                .Where(c => from == null || c.RequestedPublishTime > from)   // From date filter
                .Where(c => to == null || c.RequestedPublishTime < to)       // To date filter
                .FilterBySenderOrRecipient(orgNo, role)             // Filter by role
                .FilterByStatus(status, orgNo, role)                // Filter by status
                .Where(c => string.IsNullOrEmpty(sendersReference) || c.SendersReference == sendersReference) // Filter by sendersReference
                .Where(c => c.IsMigrating == false) // Filter out migrated correspondences that have not become available yet
                .OrderByDescending(c => c.RequestedPublishTime)              // Sort by RequestedPublishTime
                .Select(c => c.Id);

            var result = await correspondences.Take(limit).ToListAsync(cancellationToken);
            return result;
        }

        public async Task<CorrespondenceEntity?> GetCorrespondenceById(
            Guid guid,
            bool includeStatus,
            bool includeContent,
            bool includeForwardingEvents,
            CancellationToken cancellationToken,
            bool includeIsMigrating = false)
        {
            logger.LogDebug("Retrieving correspondence {CorrespondenceId} including: status={IncludeStatus} content={IncludeContent}", guid, includeStatus, includeContent);
            var correspondences = _context.Correspondences.Include(c => c.ReplyOptions).Include(c => c.ExternalReferences).Include(c => c.Notifications).AsQueryable();

            // Exclude migrating correspondences unless explicitly requested, added as an option since this method is frequently used in unit tests where it it useful to override
            if (!includeIsMigrating)
            {
                correspondences = correspondences.Where(c => !c.IsMigrating);
            }
            if (includeStatus)
            {
                correspondences = correspondences.Include(c => c.Statuses);
            }
            if (includeContent)
            {
                correspondences = correspondences.Include(c => c.Content).ThenInclude(content => content.Attachments).ThenInclude(a => a.Attachment).ThenInclude(a => a.Statuses);
            }
            if (includeForwardingEvents)
            {
                correspondences = correspondences.Include(c => c.ForwardingEvents);
            }

            return await correspondences.SingleOrDefaultAsync(c => c.Id == guid, cancellationToken);
        }

        public async Task<CorrespondenceEntity> GetCorrespondenceByAltinn2Id(int altinn2Id, CancellationToken cancellationToken)
        {
            return await _context.Correspondences.SingleAsync(c => c.Altinn2CorrespondenceId == altinn2Id, cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByAttachmentId(Guid attachmentId, bool includeStatus, CancellationToken cancellationToken = default)
        {
            var correspondence = _context.Correspondences
                .Where(c => c.Content != null && c.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId))
                .Where(c => c.IsMigrating == false) // Filter out migrated correspondences that have not become available yet
                .AsQueryable();

            correspondence = includeStatus ? correspondence.Include(c => c.Statuses) : correspondence;
            return await correspondence.ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetNonPublishedCorrespondencesByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondences = await _context.Correspondences
                .Where(c => c.IsMigrating == false) // Filter out migrated correspondences that have not become available yet
                .Where(correspondence =>
                        correspondence.Content!.Attachments.Any(attachment => attachment.AttachmentId == attachmentId) // Correspondence has the given attachment
                     && !correspondence.Statuses.Any(status => status.Status == CorrespondenceStatus.Published || status.Status == CorrespondenceStatus.ReadyForPublish  // Correspondence is not published
                                                           || status.Status == CorrespondenceStatus.Failed)
                     && correspondence.Content.Attachments.All(correspondenceAttachment => // All attachments of correspondence are published
                            correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Published) // All attachments must be published
                         && !correspondenceAttachment.Attachment.Statuses.Any(statusEntity => statusEntity.Status == AttachmentStatus.Purged))) // No attachments can be purged
                .ToListAsync(cancellationToken);

            return correspondences;
        }

        public async Task AddExternalReference(Guid correspondenceId, ReferenceType referenceType, string referenceValue, CancellationToken cancellationToken = default)
        {
            var correspondence = await _context.Correspondences.SingleOrDefaultAsync(c => c.Id == correspondenceId, cancellationToken);
            if (correspondence is null)
            {
                throw new ArgumentException("Correspondence not found", nameof(correspondenceId));
            }
            correspondence.ExternalReferences.Add(new ExternalReferenceEntity
            {
                ReferenceType = referenceType,
                ReferenceValue = referenceValue
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetCorrespondenceIdsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondenceIds = await _context.Correspondences
            .Where(c => c.Content != null && c.Content.Attachments.Any(ca => ca.AttachmentId == attachmentId))
            .Select(c => c.Id).ToListAsync(cancellationToken);
            return correspondenceIds;
        }
        public async Task UpdatePublished(Guid correspondenceId, DateTimeOffset published, CancellationToken cancellationToken)
        {
            var correspondence = await _context.Correspondences.SingleOrDefaultAsync(c => c.Id == correspondenceId, cancellationToken);
            if (correspondence != null)
            {
                correspondence.Published = published;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        public async Task UpdateIsMigrating(Guid correspondenceId, bool isMigrating, CancellationToken cancellationToken)
        {
            var correspondence = await _context.Correspondences.SingleOrDefaultAsync(c => c.Id == correspondenceId, cancellationToken);
            if (correspondence != null)
            {
                correspondence.IsMigrating = isMigrating;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesForParties(int limit, DateTimeOffset? from, DateTimeOffset? to, CorrespondenceStatus? status, List<string> recipientIds, bool includeActive, bool includeArchived, string searchString, CancellationToken cancellationToken, bool filterMigrated = true)
        {
            var correspondences = recipientIds.Count == 1
                ? _context.Correspondences.Where(c => c.Recipient == recipientIds[0])     // Filter by single recipient
                : _context.Correspondences.Where(c => recipientIds.Contains(c.Recipient)); // Filter multiple recipients

            correspondences = correspondences
                .AsNoTracking()
                .Where(c => from == null || c.RequestedPublishTime > from)   // From date filter
                .Where(c => to == null || c.RequestedPublishTime < to)       // To date filter                              
                .IncludeByStatuses(includeActive, includeArchived, status) // Filter by statuses
                .ExcludePurged() // Exclude purged correspondences
                .Where(c => string.IsNullOrEmpty(searchString) || (c.Content != null && c.Content.MessageTitle.Contains(searchString))) // Filter by messageTitle containing searchstring
                .FilterMigrated(filterMigrated) // Filter all migrated correspondences no matter their IsMigrating status
                .Include(c => c.Statuses)
                .Include(c => c.Content)
                .OrderByDescending(c => c.RequestedPublishTime);             // Sort by RequestedPublishTime

            var result = await correspondences.Take(limit).ToListAsync(cancellationToken);
            return result;
        }
        public async Task<bool> AreAllAttachmentsPublished(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            return await _context.CorrespondenceContents
                .Where(content => content.CorrespondenceId == correspondenceId)
                .Select(content => content.Attachments
                    .All(correspondenceAttachment => correspondenceAttachment.Attachment!.Statuses.Any(status => status.Status == AttachmentStatus.Published)))
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task<List<CorrespondenceEntity>> GetCandidatesForMigrationToDialogporten(int batchSize, int offset, CancellationToken cancellationToken = default)
        {
            return _context.Correspondences
                .Where(c => c.Altinn2CorrespondenceId != null && c.IsMigrating) // Only include correspondences that are not already migrated 
                .ExcludePurged() // Exclude purged correspondences
                .ExcludeSelfIdentifiedRecipients() // Exclude correspondences belonging to self identified users
                .OrderByDescending(c => c.Created)
                .ThenBy(c => c.Id)
                .Skip(offset)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesWindowAfter(
            int limit,
            DateTimeOffset? lastCreated,
            Guid? lastId,
            bool filterMigrated,
            CancellationToken cancellationToken)
        {
            var query = _context.Correspondences
                .AsNoTracking()
                .FilterMigrated(filterMigrated)
                .AsQueryable();

            if (lastCreated.HasValue)
            {
                if (lastId.HasValue)
                {
                    query = query.Where(c => c.Created > lastCreated.Value || (c.Created == lastCreated.Value && c.Id > lastId.Value));
                }
                else
                {
                    query = query.Where(c => c.Created > lastCreated.Value);
                }
            }

            query = query.OrderBy(c => c.Created).ThenBy(c => c.Id).Take(limit);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByIdsWithExternalReferenceAndCurrentStatus(
            List<Guid> correspondenceIds,
            ReferenceType referenceType,
            List<CorrespondenceStatus> currentStatuses,
            CancellationToken cancellationToken)
        {
            if (correspondenceIds == null || correspondenceIds.Count == 0)
            {
                return new List<CorrespondenceEntity>();
            }

            if (currentStatuses == null || currentStatuses.Count == 0)
            {
                return new List<CorrespondenceEntity>();
            }

            return await _context.Correspondences
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => correspondenceIds.Contains(c.Id))
                .Where(c => c.ExternalReferences.Any(er => er.ReferenceType == referenceType))
                .Where(c => currentStatuses.Contains(
                    c.Statuses
                        .OrderByDescending(s => s.StatusChanged)
                        .ThenByDescending(s => s.Id)
                        .Select(s => s.Status)
                        .FirstOrDefault()))
                .Include(c => c.ExternalReferences)
                .Include(c => c.Statuses)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByIdsWithExternalReferenceAndAllowSystemDeleteAfter(
            List<Guid> correspondenceIds,
            ReferenceType referenceType,
            CancellationToken cancellationToken)
        {
            if (correspondenceIds == null || correspondenceIds.Count == 0)
            {
                return new List<CorrespondenceEntity>();
            }

            return await _context.Correspondences
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => correspondenceIds.Contains(c.Id))
                .Where(c => c.AllowSystemDeleteAfter != null)
                .Where(c => c.ExternalReferences.Any(er => er.ReferenceType == referenceType))
                .Include(c => c.ExternalReferences)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByIdsWithExternalReferenceAndNotCurrentStatuses(
            List<Guid> correspondenceIds,
            ReferenceType referenceType,
            List<CorrespondenceStatus> excludedCurrentStatuses,
            CancellationToken cancellationToken)
        {
            if (correspondenceIds == null || correspondenceIds.Count == 0)
            {
                return new List<CorrespondenceEntity>();
            }

            if (excludedCurrentStatuses == null)
            {
                excludedCurrentStatuses = new List<CorrespondenceStatus>();
            }

            return await _context.Correspondences
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => correspondenceIds.Contains(c.Id))
                .Where(c => c.ExternalReferences.Any(er => er.ReferenceType == referenceType))
                .Where(c => !excludedCurrentStatuses.Contains(
                    c.Statuses
                        .OrderByDescending(s => s.StatusChanged)
                        .ThenByDescending(s => s.Id)
                        .Select(s => s.Status)
                        .FirstOrDefault()))
                .Include(c => c.ExternalReferences)
                .Include(c => c.Statuses)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesForReport(bool includeAltinn2, CancellationToken cancellationToken)
        {
            var query = _context.Correspondences.AsQueryable();

            // Filter by Altinn version if needed
            if (!includeAltinn2)
            {
                query = query.Where(c => c.Altinn2CorrespondenceId == null);
            }

            // Get all correspondence data needed for detailed statistics including ServiceOwnerId and AltinnVersion info
            return await query
                .Select(c => new CorrespondenceEntity
                {
                    Id = c.Id,
                    Sender = c.Sender,
                    ResourceId = c.ResourceId,
                    Created = c.Created,
                    Recipient = c.Recipient,
                    SendersReference = c.SendersReference,
                    RequestedPublishTime = c.RequestedPublishTime,
                    ServiceOwnerId = c.ServiceOwnerId,
                    ServiceOwnerMigrationStatus = c.ServiceOwnerMigrationStatus,
                    Altinn2CorrespondenceId = c.Altinn2CorrespondenceId,
                    MessageSender = c.MessageSender,
                    Statuses = new List<CorrespondenceStatusEntity>() // Initialize required property
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CorrespondenceEntity?> GetCorrespondenceByIdempotentKey(Guid idempotentKey, CancellationToken cancellationToken)
        {
            var correspondence = await _context.Correspondences
            .Where(c => c.IdempotencyKeys.Any(k => k.Id == idempotentKey))
            .Where(c => c.IsMigrating == false) // Filter out migrated correspondences that have not become available yet
            .SingleOrDefaultAsync(cancellationToken);

            return correspondence;
        }

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesByNoAltinn2IdAndExistingDialog(
            List<Guid> correspondenceIds,
            ReferenceType referenceType,
            CancellationToken cancellationToken)
        {
            if (correspondenceIds == null || correspondenceIds.Count == 0)
            {
                return new List<CorrespondenceEntity>();
            }
            return await _context.Correspondences
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => correspondenceIds.Contains(c.Id))
                .Where(c => c.Altinn2CorrespondenceId == null)
                .Where(c => c.ExternalReferences.Any(er => er.ReferenceType == referenceType))
                .Include(c => c.Content)
                .Include(c => c.ExternalReferences)
                .ToListAsync(cancellationToken);
        }
    }
}
