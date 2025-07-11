using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

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
            bool includeIsMigrating=false)
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
            if(includeForwardingEvents)
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

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesForParties(int limit, DateTimeOffset? from, DateTimeOffset? to, CorrespondenceStatus? status, List<string> recipientIds, List<string> resourceIds, bool includeActive, bool includeArchived, bool includePurged, string searchString, CancellationToken cancellationToken, bool filterMigrated = true)
        {
            var correspondences = recipientIds.Count == 1
                ? _context.Correspondences.Where(c => c.Recipient == recipientIds[0])     // Filter by single recipient
                : _context.Correspondences.Where(c => recipientIds.Contains(c.Recipient)); // Filter multiple recipients

            correspondences = correspondences
                .Where(c => from == null || c.RequestedPublishTime > from)   // From date filter
                .Where(c => to == null || c.RequestedPublishTime < to)       // To date filter                              
                .Where(c => resourceIds.Count == 0 || resourceIds.Contains(c.ResourceId))       // Filter by resources
                .IncludeByStatuses(includeActive, includeArchived, includePurged, status) // Filter by statuses
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
    }
}
