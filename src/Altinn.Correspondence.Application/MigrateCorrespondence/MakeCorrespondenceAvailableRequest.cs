namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MakeCorrespondenceAvailableRequest
{
    public Guid? CorrespondenceId { get; set; }
    public bool CreateEvents { get; set; }
    public List<Guid>? CorrespondenceIds { get; set; }
    public bool AsyncProcessing { get; set; }
    public int? BatchSize { get; set; }
    public int? BatchOffset { get; set; }
    public DateTimeOffset? CursorCreated { get; set; }
    public Guid? CursorId { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
}
