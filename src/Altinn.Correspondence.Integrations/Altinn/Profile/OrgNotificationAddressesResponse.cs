using Altinn.Correspondence.Core.Models.Profile;

namespace Altinn.Correspondence.Integrations.Altinn.Profile;

public class OrgNotificationAddressesResponse
{
    public List<OrgNotificationAddresses> ContactPointsList { get; set; } = new List<OrgNotificationAddresses>();
}
