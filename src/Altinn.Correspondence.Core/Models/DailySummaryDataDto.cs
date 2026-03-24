using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models;

/// <summary>
/// Aggregated daily summary data DTO returned from repository queries.
/// This is a data transfer object used between the persistence and application layers.
/// </summary>
public class DailySummaryDataDto
{
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string ServiceOwnerId { get; set; } = string.Empty;
    public string? ServiceOwnerName { get; set; }
    public string MessageSender { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public RecipientType RecipientType { get; set; }
    public AltinnVersion AltinnVersion { get; set; }
    public int MessageCount { get; set; }
    public long DatabaseStorageBytes { get; set; }
    public long AttachmentStorageBytes { get; set; }
}

