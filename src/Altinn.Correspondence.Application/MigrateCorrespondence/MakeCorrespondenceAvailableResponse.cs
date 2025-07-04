using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MakeCorrespondenceAvailableResponse
{
    public List<MakeCorrespondenceAvailableStatus>? Statuses { get; set; }
}

public class MakeCorrespondenceAvailableStatus : IComparable
{
    public MakeCorrespondenceAvailableStatus(Guid correspondenceId, string? error = null, string? dialogId = null, bool ok = false)
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
        if (obj is MakeCorrespondenceAvailableStatus other)
        {
            return this.CorrespondenceId.CompareTo(other.CorrespondenceId);
        }
        throw new ArgumentException($"Object is not a {nameof(MakeCorrespondenceAvailableStatus)}", nameof(obj));
    }
}
