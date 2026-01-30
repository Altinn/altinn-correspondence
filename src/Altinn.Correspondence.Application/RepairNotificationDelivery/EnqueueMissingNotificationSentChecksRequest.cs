using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.RepairNotificationDelivery;

public sealed class EnqueueMissingNotificationSentChecksRequest
{
    [Range(1, 100000)]
    public int BatchSize { get; set; } = 10000;

    [Range(0, 365)]
    public int OlderThanDays { get; set; } = 2;
}

