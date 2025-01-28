public interface IExceptionNotificationService
{
    Task NotifyAsync(Exception exception, string? context = null, CancellationToken cancellationToken = default);
}