using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IAltinnStorageService
{
    Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken);
    Task<bool> SyncCorrespondenceEventToSblBridge(int altinn2CorrespondenceId, int partyId, DateTimeOffset utcEventTimestamp, SyncEventType eventType, CancellationToken cancellationToken);
}
