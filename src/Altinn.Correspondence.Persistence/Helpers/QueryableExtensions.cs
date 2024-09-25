using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class QueryExtensions
    {
        public static IQueryable<CorrespondenceEntity> FilterBySenderOrRecipient(this IQueryable<CorrespondenceEntity> query, string orgNo, bool sender, bool recipient)
        {
            if (sender && recipient)
            {
                return query.Where(c => c.Sender == orgNo || c.Recipient == orgNo);
            }
            else if (sender)
            {
                return query.Where(c => c.Sender == orgNo);
            }
            else if (recipient)
            {
                return query.Where(c => c.Recipient == orgNo);
            }
            return Enumerable.Empty<CorrespondenceEntity>().AsQueryable();
        }

        public static IQueryable<CorrespondenceEntity> FilterByStatus(this IQueryable<CorrespondenceEntity> query, CorrespondenceStatus? status, string orgNo, bool isSender, bool isRecipient)
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

            if (isSender && isRecipient)
            {
                return query.Where(c =>
                    (c.Sender == orgNo && !blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status)) ||
                    (c.Recipient == orgNo && !blacklistRecipient.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status))
                );
            }
            else if (isSender)
            {
                return query.Where(c => c.Sender == orgNo && !blacklistSender.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status));
            }
            else if (isRecipient)
            {
                return query.Where(c => c.Recipient == orgNo && !blacklistRecipient.Contains(c.Statuses.OrderBy(cs => cs.StatusChanged).Last().Status));
            }
            return Enumerable.Empty<CorrespondenceEntity>().AsQueryable();
        }
    }
}