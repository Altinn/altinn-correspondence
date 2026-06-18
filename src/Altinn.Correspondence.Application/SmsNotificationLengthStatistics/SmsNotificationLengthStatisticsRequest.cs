using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsNotificationLengthStatisticsRequest
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    [Range(100, 5000)]
    public int? BatchSize { get; set; }

    [Range(1, 1000000)]
    public int? MaxBatches { get; set; }
}
