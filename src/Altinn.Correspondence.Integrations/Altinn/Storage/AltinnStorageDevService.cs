using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Storage
{
    public class AltinnStorageDevService : IAltinnStorageService
    {
        public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken = default)
        {
            if (partyId <= 0)
            {
                return false;
            }
            return true;
        }

        public async Task<bool> SyncCorrespondenceEventToSblBridge(int altinn2CorrespondenceId, int partyId, DateTimeOffset utcEventTimeStamp, SyncEventType eventType, CancellationToken cancellationToken)
        {
            if (partyId <= 0 || altinn2CorrespondenceId <= 0 || utcEventTimeStamp == DateTimeOffset.MinValue)
            {
                return false;
            }
            return true;
        }
    }
}
