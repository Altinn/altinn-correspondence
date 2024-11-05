using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewResponse : GetCorrespondenceOverviewResponse
{
    public bool AllowDelete { get; set; }
    public bool AuthorizedForWrite { get; set; }
    public bool AuthorizedForSign { get; set; }
    public DateTimeOffset? Archived { get; set; }
    public int MinimumAuthenticationLevel { get; set; }
}

