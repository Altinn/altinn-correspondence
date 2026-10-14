using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Core.Models.Entities;


namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceStatusMapper
{
    internal static CorrespondenceStatusEventExt MapToExternal(CorrespondenceStatusEntity correspondenceStatus, PerformedBy? performedBy = null)
    {
        var Correspondence = new CorrespondenceStatusEventExt
        {

            Status = (CorrespondenceStatusExt)correspondenceStatus.Status,
            StatusText = correspondenceStatus.StatusText,
            StatusChanged = correspondenceStatus.StatusChanged,
            PerformedBy = MapPerformedBy(performedBy)
        };
        return Correspondence;
    }

    internal static List<CorrespondenceStatusEventExt> MapListToExternal(
        List<CorrespondenceStatusEntity> correspondenceStatuses,
        IReadOnlyDictionary<Guid, PerformedBy>? performedByByPartyUuid = null)
    {
        var mappedStatuses = new List<CorrespondenceStatusEventExt>();
        foreach (var status in correspondenceStatuses)
        {
            PerformedBy? performedBy = null;
            performedByByPartyUuid?.TryGetValue(status.PartyUuid, out performedBy);
            mappedStatuses.Add(MapToExternal(status, performedBy));
        }
        return mappedStatuses;
    }

    private static PerformedByExt? MapPerformedBy(PerformedBy? performedBy)
    {
        if (performedBy is null)
        {
            return null;
        }

        return new PerformedByExt
        {
            ActorType = performedBy.ActorType == PerformedByActorType.ServiceOwner
                ? ActorTypeExt.ServiceOwner
                : ActorTypeExt.PartyRepresentative,
            ActorName = performedBy.ActorName,
            ActorId = performedBy.ActorId
        };
    }
}
