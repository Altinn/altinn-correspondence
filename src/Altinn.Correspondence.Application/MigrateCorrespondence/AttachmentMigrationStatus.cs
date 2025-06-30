using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class AttachmentMigrationStatus
{
    public Guid AttachmentId {get;set;}
    public AttachmentStatus AttachmentStatus {get;set;}
}
