using Altinn.Correspondence.Core.Models;
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
    }
}