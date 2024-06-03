
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Altinn.Events.Helpers;
internal class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return name.ToLower();
    }
}
