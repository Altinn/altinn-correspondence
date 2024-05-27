using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondencesCommand;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class GetCorrespondencesMapper
{
    internal static GetCorrespondencesCommandRequest MapToRequest(int offset, int limit, DateTimeOffset? from, DateTimeOffset? to, CorrespondenceStatusExt status)
    {
        var request = new GetCorrespondencesCommandRequest
        {
            from = from,
            limit = limit,
            offset = offset,
            status = (CorrespondenceStatus)status,
            to = to
        };
        return request;
    }
}
