using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusRequest
{
    public Guid CorrespondenceId { get; set; }
    public CorrespondenceStatus Status { get; set; }
    public string? OnBehalfOf { get; set; }
}
