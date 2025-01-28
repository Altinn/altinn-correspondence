// src/Altinn.Correspondence.API/Middleware/ExceptionNotificationMiddleware.cs
public class ExceptionNotificationMiddleware : IExceptionHandler
{
    private readonly IExceptionNotificationService _notificationService;
    private readonly ILogger<ExceptionNotificationMiddleware> _logger;

    public ExceptionNotificationMiddleware(
        IExceptionNotificationService notificationService,
        ILogger<ExceptionNotificationMiddleware> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await _notificationService.NotifyAsync(
            exception, 
            $"HTTP {httpContext.Request.Method} {httpContext.Request.Path}",
            cancellationToken);

        return false; // Let other handlers continue processing
    }
}