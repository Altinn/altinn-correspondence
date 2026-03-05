using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class PublishHelper(ICorrespondenceStatusRepository correspondenceStatusRepository,
        IAltinnRegisterService altinnRegisterService,
        ICorrespondenceRepository correspondenceRepository,
        ILogger<PublishHelper> logger)
    {
        public async Task SetPublishAtPublishTime(Guid correspondenceId, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
            if (correspondence is null)
            {
                throw new Exception($"Correspondence with id {correspondenceId} not found when scheduling publish");
            }
            var publishTime = GetActualPublishTime(correspondence.RequestedPublishTime);
            var senderParty = await altinnRegisterService.LookUpPartyById(correspondence!.Sender, cancellationToken);
            var senderPartyUuid = senderParty?.PartyUuid;
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = Core.Models.Enums.CorrespondenceStatus.Published,
                StatusText = Core.Models.Enums.CorrespondenceStatus.Published.ToString(),
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = senderPartyUuid ?? Guid.Empty
            }, cancellationToken);
            await correspondenceRepository.UpdatePublished(correspondenceId, publishTime, cancellationToken);
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now

    }
}
