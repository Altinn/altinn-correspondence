namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MigrateCorrespondenceResponse
{
    public Guid CorrespondenceId { get; set; }

    public int Altinn2CorrespondenceId { get; set; }

    public List<AttachmentMigrationStatus>? AttachmentMigrationStatuses { get; set; }
}
