using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class QueryExtensions
    {

        public static IQueryable<CorrespondenceEntity> WithValidStatuses(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status)
        {
            var blacklistedStatues = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient
            };
            if (status == null) // No status specified, return all except blacklisted
            {
                return query
                .Where(correspondence => blacklistedStatues.Contains(correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status));
            }
            else
            {
                return query
                .Where(correspondence => correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status == status);
            }
        }
    }
}