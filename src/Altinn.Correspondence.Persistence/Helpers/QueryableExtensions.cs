using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.EntityFrameworkCore.Internal;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class QueryExtensions
    {
        public static IQueryable<CorrespondenceEntity> FilterBySenderOrRecipient(this IQueryable<CorrespondenceEntity> query, string orgNo, CorrespondencesRoleType role)
        {
            return role switch
            {
                CorrespondencesRoleType.RecipientAndSender => query.Where(c => c.Sender == orgNo || c.Recipient == orgNo),
                CorrespondencesRoleType.Recipient => query.Where(c => c.Recipient == orgNo),
                CorrespondencesRoleType.Sender => query.Where(c => c.Sender == orgNo),
                _ => query.Where(c => false),
            };
        }

        public static IQueryable<CorrespondenceEntity> FilterByStatus(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status, string orgNo, CorrespondencesRoleType role)
        {
            var blacklistSender = new List<CorrespondenceStatus?> // TODO: any missing? or should not be here?
            {
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient,
            };

            var blacklistRecipient = new List<CorrespondenceStatus?> // TODO: any missing? or should not be here?
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
                return query.Where(c => c.Sender == orgNo &&
                    status.HasValue ?
                    (!blacklistSender.Contains(status)) && status == c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status :
                    (!blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)));
            }

            IQueryable<CorrespondenceEntity> FilterForRecipient()
            {
                return query.Where(c => c.Recipient == orgNo &&
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

        public static IQueryable<CorrespondenceEntity> IncludeByStatuses(this IQueryable<CorrespondenceEntity> query, bool includeActive, bool includeArchived, bool includePurged, CorrespondenceStatus? specificStatus)
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
                }

                if (includeArchived) // Include correspondences with active status
                {
                    statusesToFilter.Add(CorrespondenceStatus.Archived);
                }

                if (includePurged) // Include correspondences with active status
                {
                    statusesToFilter.Add(CorrespondenceStatus.PurgedByAltinn);
                    statusesToFilter.Add(CorrespondenceStatus.PurgedByRecipient);
                }
            }

            return query
                .Where(cs => statusesToFilter.Contains(cs.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status));
        }
    }
}