namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MigrateCorrespondenceResponse
{
    public Guid CorrespondenceId { get; set; }

    public int Altinn2CorrespondenceId { get; set; }

    public List<AttachmentMigrationStatus>? AttachmentMigrationStatuses { get; set; }
    public bool IsAlreadyMigrated { get; set; } = false;

    public string? DialogId { get; set; }
}
