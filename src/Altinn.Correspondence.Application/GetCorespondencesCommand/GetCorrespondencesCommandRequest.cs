using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondencesCommand;

public class GetCorrespondencesCommandRequest
{
    public int offset;

    public int limit;

    public DateTimeOffset? from;

    public DateTimeOffset? to;

    public CorrespondenceStatus status = CorrespondenceStatus.Published;
}
