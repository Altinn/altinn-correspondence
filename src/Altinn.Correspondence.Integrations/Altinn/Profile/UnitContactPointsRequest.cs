namespace Altinn.Correspondence.Integrations.Altinn.Profile;

public class UnitContactPointsRequest
{
    public List<string> OrganizationNumbers { get; set; } = new List<string>();
    public string ResourceId { get; set; } = string.Empty;
}
