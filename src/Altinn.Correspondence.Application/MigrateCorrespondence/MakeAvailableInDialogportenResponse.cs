using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MakeAvailableInDialogportenResponse
{
    public List<MakeAvailableInDialogPortenStatus>? Statuses { get; set; }
}

public class MakeAvailableInDialogPortenStatus : IComparable
{
    public MakeAvailableInDialogPortenStatus(Guid correspondenceId, string? error = null, string? dialogId = null, bool ok = false)
    {
        CorrespondenceId = correspondenceId;
        DialogId = dialogId;
        Ok = ok;
        Error = error;
    }
    public Guid CorrespondenceId { get; set; }
    public string? DialogId { get; set; }
    public bool Ok { get; set; } = false;
    public string? Error { get; set; }
    public int CompareTo(object? obj)
    {
        MakeAvailableInDialogPortenStatus inc = obj as MakeAvailableInDialogPortenStatus;
        if (inc != null)
        {
            return this.CorrespondenceId.CompareTo(inc.CorrespondenceId);
        }

        throw new NullReferenceException();
    }
}
