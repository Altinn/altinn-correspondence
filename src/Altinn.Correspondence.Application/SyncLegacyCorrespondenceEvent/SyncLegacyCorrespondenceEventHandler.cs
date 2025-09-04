using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.SyncLegacyCorrespondenceEvent;

public class SyncLegacyCorrespondenceEventHandler(
    ILogger<SyncLegacyCorrespondenceEventHandler> logger,
    IAltinnStorageService altinnStorageService)
{
    public async Task Process(int partyId, int altinn2CorrespondenceId, DateTimeOffset dateTimeOffset, SyncEventType eventType, CancellationToken cancellationToken)
    {
        logger.LogInformation("{partyId} is syncing {eventType} for correspondence {altinn2CorrespondenceId} to SBLBridge", partyId, eventType.ToString(), altinn2CorrespondenceId);
        await altinnStorageService.SyncCorrespondenceEventToSblBridge(altinn2CorrespondenceId, (int)partyId, dateTimeOffset, eventType, cancellationToken);
    }
}
