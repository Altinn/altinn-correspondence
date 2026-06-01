namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsNotificationLengthStatisticsResponse
{
    public required string JobId { get; set; }
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }
    public required int BatchSize { get; set; }
}
