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

        public async Task<List<CorrespondenceEntity>> GetCorrespondencesForParties(
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            List<string> recipientIds,
            List<string> resourceIds,
            bool includeActive,
            bool includeArchived,
            bool includePurged,
            string searchString,
            CancellationToken cancellationToken,
            bool filterMigrated = true)
        {
            // Build a lookup for latest statuses
            var latestStatuses = _context.CorrespondenceStatuses
                .GroupBy(s => s.CorrespondenceId)
                .Select(g => g.OrderByDescending(s => s.Status).FirstOrDefault());

            // Base correspondence query
            var correspondences = _context.Correspondences.AsQueryable();

            if (recipientIds.Count == 1)
                correspondences = correspondences.Where(c => c.Recipient == recipientIds[0]);
            else
                correspondences = correspondences.Where(c => recipientIds.Contains(c.Recipient));

            if (from.HasValue)
                correspondences = correspondences.Where(c => c.RequestedPublishTime > from);

            if (to.HasValue)
                correspondences = correspondences.Where(c => c.RequestedPublishTime < to);

            if (resourceIds.Any())
                correspondences = correspondences.Where(c => resourceIds.Contains(c.ResourceId));

            if (!string.IsNullOrEmpty(searchString))
                correspondences = correspondences.Where(c => c.Content != null && c.Content.MessageTitle.Contains(searchString));

            if (filterMigrated)
                correspondences = correspondences.Where(c => !c.IsMigrating);

            // Join latest statuses
            var query = correspondences
                .Join(latestStatuses,
                      c => c.Id,
                      s => s.CorrespondenceId,
                      (c, s) => new { Correspondence = c, LatestStatus = s });

            // Apply status filters
            var statusesToInclude = GetStatusesToInclude(includeActive, includeArchived, includePurged, status);

            if (statusesToInclude.Any())
            {
                query = query.Where(cs =>
                    cs.LatestStatus != null &&
                    statusesToInclude.Contains(cs.LatestStatus.Status) ||
                    (cs.LatestStatus == null && statusesToInclude.Contains(null)));
            }

            // Project and include related entities *after* filtering and limiting
            var result = await query
                .OrderByDescending(cs => cs.Correspondence.RequestedPublishTime)
                .ThenBy(cs => cs.Correspondence.Id)
                .Select(cs => cs.Correspondence)
                .Include(c => c.Content)
                .Include(c => c.Statuses)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return result;
        }

        private static List<CorrespondenceStatus?> GetStatusesToInclude(bool includeActive, bool includeArchived, bool includePurged, CorrespondenceStatus? specificStatus)
        {
            var statusesToInclude = new List<CorrespondenceStatus?>();
            if (specificStatus != null) // Specific status overrides other choices
            {
                statusesToInclude.Add(specificStatus);
            }
            else
            {
                if (includeActive) // Include correspondences with active status
                {
                    statusesToInclude.Add(CorrespondenceStatus.Published);
                    statusesToInclude.Add(CorrespondenceStatus.Fetched);
                    statusesToInclude.Add(CorrespondenceStatus.Read);
                    statusesToInclude.Add(CorrespondenceStatus.Confirmed);
                    statusesToInclude.Add(CorrespondenceStatus.Replied);
                }
                if (includeArchived) // Include correspondences with archived status
                {
                    statusesToInclude.Add(CorrespondenceStatus.Archived);
                }
                if (includePurged) // Include correspondences with purged status
                {
                    statusesToInclude.Add(CorrespondenceStatus.PurgedByAltinn);
                    statusesToInclude.Add(CorrespondenceStatus.PurgedByRecipient);
                }
            }
            return statusesToInclude;
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
