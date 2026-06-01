using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsNotificationLengthStatisticsRequest
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    [Range(100, 5000)]
    public int? BatchSize { get; set; }
}
