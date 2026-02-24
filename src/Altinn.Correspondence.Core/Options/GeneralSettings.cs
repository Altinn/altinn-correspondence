namespace Altinn.Correspondence.Core.Options;

public class GeneralSettings
{
    public string SlackUrl { get; set; } = string.Empty;

    public string CorrespondenceBaseUrl { get; set; } = string.Empty;

    public string RedisConnectionString { get; set; } = string.Empty;
    public string ContactReservationRegistryBaseUrl { get; set; } = string.Empty;
    public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
    public bool DisableTelemetryForMigration { get; set; } = true;
    public bool DisableTelemetryForSync { get; set; } = false;
    public string MalwareScanBypassWhiteList { get; set; } = string.Empty;
    public int MigrationWorkerCountPerReplica { get; set; } = 2;
    public int WorkerCountPerReplica { get; set; } = 50;
}
