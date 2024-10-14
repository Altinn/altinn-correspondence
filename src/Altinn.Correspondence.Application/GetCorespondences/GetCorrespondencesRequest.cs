using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesRequest
{
    public required string ResourceId { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public CorrespondenceStatus? Status { get; set; }

    public required CorrespondencesRoleType Role { get; set; }
}
