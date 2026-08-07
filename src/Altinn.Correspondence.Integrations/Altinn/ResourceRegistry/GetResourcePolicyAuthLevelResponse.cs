using System.Runtime.CompilerServices;
using System.Xml.Linq;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
internal class GetResourcePolicyAuthLevelResponse
{
    public List<int> MinimumAuthenticationLevels { get; init; } = new List<int>();

    public static GetResourcePolicyAuthLevelResponse Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var levels = doc.Descendants()
            .Where(e => e.Name.LocalName == "ObligationExpression")
            .SelectMany(o => o.Descendants().Where(d => d.Name.LocalName == "AttributeValue"))
            .Where(v => v.Attribute("DataType")?.Value.EndsWith("#integer") == true)
            .Select(v => int.Parse(v.Value))
            .ToList();

        return new GetResourcePolicyAuthLevelResponse { MinimumAuthenticationLevels = levels };
    }
}