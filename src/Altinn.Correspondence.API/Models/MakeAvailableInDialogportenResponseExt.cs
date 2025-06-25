using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.API.Models;
public class MakeAvailableInDialogportenResponseExt
{
    public List<MakeAvailableInDialogPortenStatusExt>? Statuses { get; set; }
}

public class MakeAvailableInDialogPortenStatusExt
{
    public MakeAvailableInDialogPortenStatusExt(Guid correspondenceId, string? error = null, string? dialogId = null, bool ok = false)
    {
        CorrespondenceId = correspondenceId;
        DialogId = dialogId;
        Ok = ok;
        Error = error;
    }
    public Guid CorrespondenceId { get; set; }
    public string? DialogId { get; set; }
    public string? Error { get; set; }
    public bool Ok { get; set; } = false;
}
