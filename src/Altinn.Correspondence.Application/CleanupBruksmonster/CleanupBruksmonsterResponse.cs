namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterResponse
{
    public required string ResourceId { get; set; }
    public required int CorrespondencesFound { get; set; }
    public required int AttachmentsFound { get; set; }
    public required string DeleteDialogsJobId { get; set; }
    public required string DeleteCorrespondencesJobId { get; set; }
}

