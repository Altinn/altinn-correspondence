using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models;


namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceStatusMapper
{
    internal static CorrespondenceStatusEventExt MapToExternal(CorrespondenceStatusEntity correspondenceStatus)
    {
        var Correspondence = new CorrespondenceStatusEventExt
        {

            Status = (CorrespondenceStatusExt)correspondenceStatus.Status,
            StatusText = correspondenceStatus.StatusText,
            StatusChanged = correspondenceStatus.StatusChanged
        };
        return Correspondence;
    }

    internal static List<CorrespondenceStatusEventExt> MapListToExternal(List<CorrespondenceStatusEntity> correspondenceStatuses)
    {
        var mappedStatuses = new List<CorrespondenceStatusEventExt>();
        foreach (var status in correspondenceStatuses)
        {
            mappedStatuses.Add(MapToExternal(status));
        }
        return mappedStatuses;
    }
}
