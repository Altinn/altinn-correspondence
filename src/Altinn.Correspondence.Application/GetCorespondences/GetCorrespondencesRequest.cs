using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesRequest
{
    public int offset;

    public int limit;

    public DateTimeOffset? from;

    public DateTimeOffset? to;

    public CorrespondenceStatus status = CorrespondenceStatus.Published;
}
