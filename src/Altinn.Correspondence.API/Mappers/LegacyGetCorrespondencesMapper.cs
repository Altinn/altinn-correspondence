using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class LegacyGetCorrespondencesMapper
{
    internal static LegacyGetCorrespondencesRequest MapToRequest(LegacyGetCorrespondencesRequestExt requestExt)
    {
        return new LegacyGetCorrespondencesRequest()
        {
            From = requestExt.From,
            To = requestExt.To,
            IncludeActive = requestExt.IncludeActive,
            IncludeArchived = requestExt.IncludeArchived,
            IncludeDeleted = requestExt.IncludeDeleted,
            InstanceOwnerPartyIdList = requestExt.InstanceOwnerPartyIdList,
            Offset = requestExt.Offset,
            Limit = requestExt.Limit,
            Language = requestExt.Language,
            SearchString = requestExt.SearchString,
            Status = requestExt.Status is null ? null : (CorrespondenceStatus)requestExt.Status
        };
    }
}
