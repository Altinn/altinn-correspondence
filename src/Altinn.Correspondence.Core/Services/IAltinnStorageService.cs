namespace Altinn.Correspondence.Core.Services;
public interface IAltinnStorageService
{
    Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken);

}
