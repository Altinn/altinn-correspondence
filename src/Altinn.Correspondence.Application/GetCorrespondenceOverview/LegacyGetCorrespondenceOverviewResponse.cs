using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewResponse : GetCorrespondenceOverviewResponse
{
    public bool AllowDelete { get; set; }
    public bool AuthroizedForWrite { get; set; }
    public bool AuthorizedForSign { get; set; }
    public DateTimeOffset? Archived { get; set; }
    public int MinimumAuthenticationlevel { get; set; }
}

