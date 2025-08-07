using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;
public class SyncCorrespondenceStatusHelper(    
    ICorrespondenceStatusRepository correspondenceStatusRepository)
{
    
    public async Task AddSyncedCorrespondenceStatuses(CorrespondenceEntity correspondence, List<CorrespondenceStatusEntity> statuses, CancellationToken cancellationToken)
    {
        foreach (var entity in statuses)
        {
            entity.CorrespondenceId = correspondence.Id;
            entity.SyncedFromAltinn2 = DateTime.UtcNow;
        }

        await correspondenceStatusRepository.AddCorrespondenceStatuses(statuses, cancellationToken);
    }

    /// <summary>
    /// Validates if the status of the synced correspondence allows for status updates.
    /// </summary>
    /// <param name="correspondence">The correspondence entity to validate</param>
    /// <returns></returns>
    internal Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        var currentStatus = correspondence.GetHighestStatus();
        if (currentStatus is null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return null;
    }
}