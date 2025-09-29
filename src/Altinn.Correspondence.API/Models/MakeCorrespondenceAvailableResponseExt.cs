namespace Altinn.Correspondence.API.Models;
public class MakeCorrespondenceAvailableResponseExt
{
    public List<MakeCorrespondenceAvailableStatusExt>? Statuses { get; set; }
}

public class MakeCorrespondenceAvailableStatusExt
{
    public MakeCorrespondenceAvailableStatusExt(Guid correspondenceId, string? error = null, string? dialogId = null, bool ok = false)
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
