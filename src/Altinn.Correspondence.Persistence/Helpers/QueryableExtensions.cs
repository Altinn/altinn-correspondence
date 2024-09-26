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
                CorrespondencesRoleType.RecipientAndSender => query.Where(c => c.Sender == orgNo || c.Recipient == orgNo),
                CorrespondencesRoleType.Recipient => query.Where(c => c.Recipient == orgNo),
                CorrespondencesRoleType.Sender => query.Where(c => c.Sender == orgNo),
                _ => query.Where(c => false),
            };
        }

        public static IQueryable<CorrespondenceEntity> FilterByStatus(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status, string orgNo, CorrespondencesRoleType role)
        {
            var blacklistSender = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient
            };
            var blacklistRecipient = new List<CorrespondenceStatus?>
            {
                CorrespondenceStatus.Initialized,
                CorrespondenceStatus.ReadyForPublish,
                CorrespondenceStatus.Archived,
                CorrespondenceStatus.PurgedByAltinn,
                CorrespondenceStatus.PurgedByRecipient
            };

            if (status is not null)
            {
                return query
                .Where(correspondence => correspondence.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status == status);
            }

            return role switch
            {
                CorrespondencesRoleType.RecipientAndSender => query.Where(c =>
                                    (c.Sender == orgNo && !blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)) ||
                                    (c.Recipient == orgNo && !blacklistRecipient.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status))),
                CorrespondencesRoleType.Recipient => query.Where(c => c.Recipient == orgNo && !blacklistRecipient.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)),
                CorrespondencesRoleType.Sender => query.Where(c => c.Sender == orgNo && !blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)),
                _ => query.Where(c => false),
            };
        }
    }
}