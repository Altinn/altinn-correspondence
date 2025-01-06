namespace Altinn.Correspondence.Core.Options;

public class GeneralSettings
{
    public string SlackUrl { get; set; } = string.Empty;

    public string CorrespondenceBaseUrl { get; set; } = string.Empty;
    public string RedisConnectionString { get; set; } = string.Empty;
    public string ContactReservationRegistryBaseUrl { get; set; } = string.Empty;
}