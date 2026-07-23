using Altinn.Correspondence.Core.Models.Profile;

namespace Altinn.Correspondence.Integrations.Altinn.Profile;

public class UnitContactPointsResponse
{
    public List<UnitContactPoints> ContactPointsList { get; set; } = new List<UnitContactPoints>();
}
