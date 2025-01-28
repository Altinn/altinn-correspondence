public class SlackExceptionNotificationService : IExceptionNotificationService
{
    private readonly ILogger<SlackExceptionNotificationService> _logger;
    private readonly ISlackClient _slackClient;
    private readonly IHostEnvironment _hostEnvironment;
    private const string TestChannel = "#test-varslinger";

    public SlackExceptionNotificationService(
        ILogger<SlackExceptionNotificationService> logger,
        ISlackClient slackClient,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;
    }

    public async Task NotifyAsync(Exception exception, string? context = null, CancellationToken cancellationToken = default)
    {
        var message = FormatExceptionMessage(exception, context);
        
        try
        {
            await SendSlackNotificationWithMessage(message, cancellationToken);
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
        }
    }

    private string FormatExceptionMessage(Exception exception, string? context)
    {
        return $":warning: *Unhandled Exception*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Correspondence\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Context:* {context ?? "Not provided"}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private async Task SendSlackNotificationWithMessage(string message, CancellationToken cancellationToken)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = TestChannel,
        };
        await _slackClient.PostAsync(slackMessage, cancellationToken);
    }
}