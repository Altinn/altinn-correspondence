namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class StatisticsSummaryResponse
{
    /// <summary>
    /// Summary of correspondences per service owner
    /// </summary>
    public List<ServiceOwnerSummary> ServiceOwnerSummaries { get; set; } = new();
    
    /// <summary>
    /// Total number of correspondences in the report
    /// </summary>
    public int TotalCorrespondences { get; set; }
    
    /// <summary>
    /// Total number of unique service owners
    /// </summary>
    public int TotalServiceOwners { get; set; }
    
    /// <summary>
    /// When this summary was generated
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }
    
    /// <summary>
    /// Environment (e.g., "Development", "Test", "Production")
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    
    /// <summary>
    /// Date range of the correspondences
    /// </summary>
    public DateRange? DateRange { get; set; }
}

public class ServiceOwnerSummary
{
    /// <summary>
    /// Service Owner ID
    /// </summary>
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name (if available)
    /// </summary>
    public string? ServiceOwnerName { get; set; }
    
    /// <summary>
    /// Number of correspondences for this service owner
    /// </summary>
    public int CorrespondenceCount { get; set; }
    
    /// <summary>
    /// Percentage of total correspondences
    /// </summary>
    public decimal PercentageOfTotal { get; set; }
    
    /// <summary>
    /// Unique resources used by this service owner
    /// </summary>
    public int UniqueResourceCount { get; set; }
    
    /// <summary>
    /// Most recent correspondence date for this service owner
    /// </summary>
    public DateTimeOffset? MostRecentCorrespondence { get; set; }
}

public class DateRange
{
    /// <summary>
    /// Earliest correspondence date in the dataset
    /// </summary>
    public DateTimeOffset From { get; set; }
    
    /// <summary>
    /// Latest correspondence date in the dataset
    /// </summary>
    public DateTimeOffset To { get; set; }
}
