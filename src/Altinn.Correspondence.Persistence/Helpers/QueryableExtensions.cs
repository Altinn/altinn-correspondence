using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class QueryExtensions
    {
        public static IQueryable<CorrespondenceEntity> WithValidStatuses(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status, string orgNo)
        {
            var blacklistedStatuesSender = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient
            };
            var blacklistedStatuesRecipient = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Initialized,
                CorrespondenceStatus.ReadyForPublish,
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient
            };
            if (status == null) // No status specified, return all except blacklisted
            {
                return query.Where(correspondence => correspondence.Recipient.Contains(orgNo) ? // If org is a recipient
                !blacklistedStatuesRecipient.Contains(correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status) : 
                !blacklistedStatuesSender.Contains(correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status));
            }
            else
            {
                return query
                .Where(correspondence => correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status == status);
            }
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