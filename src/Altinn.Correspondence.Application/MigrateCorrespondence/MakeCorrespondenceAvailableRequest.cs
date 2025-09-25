namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MakeCorrespondenceAvailableRequest
{
    public Guid? CorrespondenceId { get; set; }
    public bool CreateEvents { get; set; }
    public List<Guid>? CorrespondenceIds { get; set; }
    public bool AsyncProcessing { get; set; }
    public int? BatchSize { get; set; }
    public int? BatchOffset { get; set; }
}
