using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class QueryExtensions
    {
        public static IQueryable<CorrespondenceEntity> FilterBySenderOrRecipient(this IQueryable<CorrespondenceEntity> query, string orgNo, CorrespondencesRoleType role)
        {
            return role switch
            {
                CorrespondencesRoleType.RecipientAndSender => query.Where(c => 
                    c.ServiceOwnerId == orgNo || c.Sender.Contains(orgNo) || c.Recipient.Contains(orgNo)),
                CorrespondencesRoleType.Recipient => query.Where(c => c.Recipient.Contains(orgNo)),
                CorrespondencesRoleType.Sender => query.Where(c => c.ServiceOwnerId == orgNo || c.Sender.Contains(orgNo)),
                _ => query.Where(c => false),
            };
        }

        public static IQueryable<CorrespondenceEntity> FilterByStatus(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status, string orgNo, CorrespondencesRoleType role)
        {
            var blacklistSender = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient,
            };

            var blacklistRecipient = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Initialized,
                CorrespondenceStatus.ReadyForPublish,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient,
                CorrespondenceStatus.Failed,
            };

            // Helper functions for common query patterns
            IQueryable<CorrespondenceEntity> FilterForSender()
            {
                return query.Where(c => (c.ServiceOwnerId == orgNo || c.Sender.Contains(orgNo)) &&
                    status.HasValue ?
                    (!blacklistSender.Contains(status)) && status == c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status :
                    (!blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)));
            }

            IQueryable<CorrespondenceEntity> FilterForRecipient()
            {
                return query.Where(c => c.Recipient.Contains(orgNo) &&
                    status.HasValue ?
                    (!blacklistRecipient.Contains(status)) && status == c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status :
                    (!blacklistRecipient.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)));
            }
            return role switch
            {
                CorrespondencesRoleType.Sender => FilterForSender(),
                CorrespondencesRoleType.Recipient => FilterForRecipient(),
                CorrespondencesRoleType.RecipientAndSender => FilterForSender().Union(FilterForRecipient()),
                _ => throw new ArgumentException("Invalid CorrespondencesRoleType")
            };
        }

        public static IQueryable<CorrespondenceEntity> IncludeByStatuses(this IQueryable<CorrespondenceEntity> query, bool includeActive, bool includeArchived, CorrespondenceStatus? specificStatus)
        {
            var statusesToFilter = new List<CorrespondenceStatus?>();

            if (specificStatus != null) // Specific status overrides other choices
            {
                statusesToFilter.Add(specificStatus);
            }
            else
            {
                if (includeActive) // Include correspondences with active status
                {
                    statusesToFilter.Add(CorrespondenceStatus.Published);
                    statusesToFilter.Add(CorrespondenceStatus.Fetched);
                    statusesToFilter.Add(CorrespondenceStatus.Read);
                    statusesToFilter.Add(CorrespondenceStatus.Confirmed);
                    statusesToFilter.Add(CorrespondenceStatus.Replied);
                    //statusesToFilter.Add(CorrespondenceStatus.AttachmentsDownloaded);
                }

                if (includeArchived) // Include correspondences with active status
                {
                    statusesToFilter.Add(CorrespondenceStatus.Archived);
                }
            }

            return query.Where(cs =>
                statusesToFilter.Contains(cs.Statuses.Max(s => s.Status)));
        }

        /// <summary>
        /// Excludes correspondences that have been purged (either by Altinn or by the recipient).
        /// </summary>
        /// <returns>An <see cref="IQueryable{CorrespondenceEntity}"/> containing only correspondences that do not have any status entries of <see cref="CorrespondenceStatus.PurgedByAltinn"/> or <see cref="CorrespondenceStatus.PurgedByRecipient"/>.</returns>
        public static IQueryable<CorrespondenceEntity> ExcludePurged(this IQueryable<CorrespondenceEntity> query)
        {
            return query.Where(cs =>
                    !cs.Statuses.Any(s =>
                        s.Status == CorrespondenceStatus.PurgedByAltinn ||
                        s.Status == CorrespondenceStatus.PurgedByRecipient));
        }

        /// <summary>
        /// Filters out correspondences whose Recipient is a self-identified party (i.e., whose Recipient value starts with the party-UUID prefix).
        /// </summary>
        /// <returns>An <see cref="IQueryable{CorrespondenceEntity}"/> containing only correspondences whose Recipient does not have the party-UUID prefix.</returns>
        public static IQueryable<CorrespondenceEntity> ExcludeSelfIdentifiedRecipients(this IQueryable<CorrespondenceEntity> query)
        {
            return query.Where(cs =>
                    !cs.Recipient.IsWithPartyUuidPrefix());
        }

        /// <summary>
        /// Filters correspondences to those whose current (latest) status is one of the provided statuses.
        /// </summary>
        /// <remarks>
        /// The "current" status is determined by ordering the <c>Statuses</c> collection by <c>StatusChanged</c> then <c>Id</c> and taking the last element.
        /// If <paramref name="statuses"/> is null or empty, the method returns an empty result set.
        /// </remarks>
        /// <param name="statuses">One or more status values to match against an entity's latest status.</param>
        /// <returns>An <see cref="IQueryable{CorrespondenceEntity}"/> containing only entities whose latest status is in <paramref name="statuses"/>.</returns>
        public static IQueryable<CorrespondenceEntity> WhereCurrentStatusIn(
            this IQueryable<CorrespondenceEntity> query,
            params CorrespondenceStatus[] statuses)
        {
            if (statuses == null || statuses.Length == 0)
            {
                return query.Where(_ => false);
            }

            return query.Where(c => c.Statuses.Any() &&statuses.Contains(
                c.Statuses
                    .OrderBy(s => s.StatusChanged)
                    .ThenBy(s => s.Id)
                    .Last().Status));
        }

        /// <summary>
        /// Filters out migrated correspondences when filterMigrated is true
        /// </summary>
        /// <param name="query">The source query</param>
        /// <param name="filterMigrated">When true, excludes migrated correspondences</param>
        /// <returns>Filtered or unmodified query based on the filterMigrated parameter</returns>
        public static IQueryable<CorrespondenceEntity> FilterMigrated(this IQueryable<CorrespondenceEntity> query, bool filterMigrated)
        {
            if (!filterMigrated)
            {
                return query;
            }
            return query
                .Where(cs => !cs.Altinn2CorrespondenceId.HasValue);
        }
    }
}