namespace Altinn.Correspondence.Core.Options;

public class GeneralSettings
{
    public string SlackUrl { get; set; } = string.Empty;

    public string CorrespondenceBaseUrl { get; set; } = string.Empty;

    public string AltinnSblBridgeBaseUrl { get; set; } = string.Empty;
    public string RedisConnectionString { get; set; } = string.Empty;
    public string ContactReservationRegistryBaseUrl { get; set; } = string.Empty;
    public string BrregBaseUrl { get; set; } = string.Empty;
    public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
    public bool DisableTelemetryForMigration { get; set; } = true;
    public bool DisableTelemetryForSync { get; set; } = false;
    public int MigrationWorkerCountPerReplica { get; set; } = 2;
    public string BruksmonsterTestsResourceId { get; set; } = string.Empty;
}
