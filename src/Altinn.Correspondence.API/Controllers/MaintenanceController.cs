using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CleanupOrphanedDialogs;
using Altinn.Correspondence.Application.CleanupPerishingDialogs;
using Altinn.Correspondence.Application.CleanupMarkdownAndHTMLInSummary;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;
using Altinn.Correspondence.Application.InitializeServiceOwner;
using Altinn.Correspondence.Application.CleanupBruksmonster;
using Altinn.Correspondence.Application.CleanupConfirmedMigratedCorrespondences;
using Altinn.Correspondence.Application.RepairNotificationDelivery;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Altinn.Correspondence.Application.MigrateForwardingEventsBatch;
using Altinn.Correspondence.Application.CleanupBulkFetchStatuses;
using Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;
using Altinn.Correspondence.Application.MaskinportenJwkRotation;
using Altinn.Correspondence.Application.SmsNotificationLengthStatistics;
using Altinn.Correspondence.API.Swagger;
using Altinn.Correspondence.Application.PurgeDialogAndDeleteReminderForReadCorrespondences;
using Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;
using Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[HideFromPublicApi]
[Route("correspondence/api/v1/maintenance")]
[Authorize]
public class MaintenanceController(ILogger<MaintenanceController> logger) : Controller
{
    private readonly ILogger<MaintenanceController> _logger = logger;

    /// <summary>
    /// Enqueue cleanup of orphaned dialogs in Dialogporten for correspondences already purged in our system
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-orphaned-dialogs")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupOrphanedDialogsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupOrphanedDialogs(
        [FromServices] CleanupOrphanedDialogsHandler handler,
        [FromBody] CleanupOrphanedDialogsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup orphaned dialogs received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Enqueue cleanup to remove expiresAt from dialogs in Dialogporten
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-perishing-dialogs")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupPerishingDialogsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupPerishingDialogs(
        [FromServices] CleanupPerishingDialogsHandler handler,
        [FromBody] CleanupPerishingDialogsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup perishing dialogs received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Enqueue restore of soft-deleted dialogs in Dialogporten for correspondences that are not purged
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("restore-soft-deleted-dialogs")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RestoreSoftDeletedDialogsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> RestoreSoftDeletedDialogs(
        [FromServices] RestoreSoftDeletedDialogsHandler handler,
        [FromBody] RestoreSoftDeletedDialogsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to restore soft-deleted dialogs received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Enqueue cleanup to remove markdown and html from summary in Dialogporten for correspondences
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-markdown-and-html-in-summary")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupMarkdownAndHTMLInSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupMarkdownAndHtmlInSummary(
        [FromServices] CleanupMarkdownAndHTMLInSummaryHandler handler,
        [FromBody] CleanupMarkdownAndHTMLInSummaryRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup markdown and html in summary received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Enqueue cleanup to remove confirmed migrated correspondences in Dialogporten
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-confirmed-migrated-correspondences")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupConfirmedMigratedCorrespondencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupConfirmedMigratedCorrespondences(
        [FromServices] CleanupConfirmedMigratedCorrespondencesHandler handler,
        [FromBody] CleanupConfirmedMigratedCorrespondencesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup confirmed migrated correspondences received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Initialize a new service owner in the system and deploy storage accounts
    /// </summary>
    /// <response code="200">boolean</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("initialize-service-owner")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> InitializeServiceOwner(
        [FromServices] InitializeServiceOwnerHandler handler,
        [FromBody] InitializeServiceOwnerRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to initialize service owner for {serviceOwner} received", request.ServiceOwnerName);
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            (result) => Ok(result),
            Problem
        );
    }

    /// <summary>
    /// Cleanup test data (dialogs and correspondences) for bruksmonstertests.
    /// Optionally scopes cleanup to data older than a given age.
    /// </summary>
    /// <response code="200">Returns a summary of deleted correspondences</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-bruksmonster")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupBruksmonsterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupBruksmonsterTestData(
        [FromServices] CleanupBruksmonsterHandler handler,
        [FromQuery] int? minAgeDays,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup bruksmonster test data received");
        var request = new CleanupBruksmonsterRequest
        {
            MinAgeDays = minAgeDays
        };

        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Enqueue a repair job that schedules delivery checks for Altinn3 notifications
    /// missing the "notification sent" information activity in Dialogporten.
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("enqueue-missing-notification-sent-checks")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EnqueueMissingNotificationSentChecksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> EnqueueMissingNotificationSentChecks(
        [FromServices] EnqueueMissingNotificationSentChecksHandler handler,
        [FromBody] EnqueueMissingNotificationSentChecksRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to enqueue missing notification sent checks received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Sync a single Correspondence forwarding event to Dialogporten
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("sync-forwarding-event/{correspondenceForwardingId}")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EnqueueMissingNotificationSentChecksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> SyncForwardingEvent(
        [FromServices] IDialogportenService service,
        [FromRoute] Guid correspondenceForwardingId,
        CancellationToken cancellationToken)
    {
        await service.AddForwardingEvent(correspondenceForwardingId, cancellationToken);
        return Ok();
    }


    /// <summary>
    /// Sync all forwarding events that has no dialog activity id yet to Dialogporten
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("sync-forwarding-events-batch/{count}")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EnqueueMissingNotificationSentChecksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> SyncForwardingEvents(
        [FromServices] IBackgroundJobClient backgroundJobClient,
        [FromRoute] int count,
        CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<MigrateForwardingEventsBatchHandler>(handler => handler.Process(count, DateTimeOffset.UtcNow));
        return Ok();
    }


    [HttpPost]
    [Route("cleanup-bulk-fetch-statuses")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupBulkFetchStatuses(
        [FromServices] CleanupBulkFetchStatusesHandler handler,
        [FromBody] CleanupBulkFetchStatusesRequest request,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Request to cleanup bulk fetch statuses received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }


    [HttpPost]
    [Route("manual-retry-not-published-correspondences")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ManualRetryNotPublishedCorrespondences(
        [FromServices] ManualRetryNotPublishedCorrespondencesHandler handler,
        [FromBody] ManualRetryNotPublishedCorrespondencesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to manually retry not published correspondences received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    [HttpPost]
    [Route("maskinporten-jwk-rotation")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult TriggerMaskinportenJwkRotation(
        [FromServices] IBackgroundJobClient backgroundJobClient,
        [FromBody] TriggerMaskinportenJwkRotationRequest request)
    {
        const string expectedConfirmation = "rotate-maskinporten-jwk";
        if (!string.Equals(request.Confirmation, expectedConfirmation, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = $"Confirmation must be '{expectedConfirmation}' to trigger Maskinporten JWK rotation."
            });
        }

        var jobId = backgroundJobClient.Enqueue<MaskinportenJwkRotationHandler>(
            handler => handler.Process(CancellationToken.None));

        _logger.LogWarning(
            "Manual Maskinporten JWK rotation job {JobId} was enqueued by {User}.",
            jobId,
            HttpContext.User.Identity?.Name ?? "unknown");

        return Ok(new
        {
            jobId,
            message = "Maskinporten JWK rotation was enqueued."
        });
    }

    /// <summary>
    /// Enqueue a job that computes hypothetical SMS notification length statistics for all
    /// correspondences created within the given date range. Aggregated statistics are emitted to logs.
    /// </summary>
    /// <response code="200">Returns the enqueued job id and the resolved range</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("sms-notification-length-statistics")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SmsNotificationLengthStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetSmsNotificationLengthStatistics(
        [FromServices] SmsNotificationLengthStatisticsHandler handler,
        [FromBody] SmsNotificationLengthStatisticsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to compute SMS notification length statistics received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    [HttpPost]
    [Route("purge-dialog-and-delete-reminder-for-read-correspondences")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> PurgeDialogAndDeleteReminderForReadCorrespondences(
        [FromServices] PurgeDialogAndDeleteReminderForReadCorrespondencesHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to purge dialog and delete reminder for read correspondences received");
        var result = await handler.Process(HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    [HttpPost]
    [Route("update-old-correspondences-with-download-all")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateOldCorrespondencesWithDownloadAll(
        [FromServices] UpdateOldCorrespondencesWithDownloadAllHandler handler,
        [FromBody] UpdateOldCorrespondencesWithDownloadAllRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to update old correspondences with download all received");
        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Starts batch cleanup of missing synced notification events to Dialogporten for migrated correspondences
    /// </summary>
    /// <param name="handler">The handler for processing notification events batch</param>
    /// <param name="batchCount">Number of notifications to process per batch</param>
    /// <param name="startDate">Start processing from this date (defaults to DateTime.MaxValue to process newest first)</param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("cleanup-missing-synced-notification-events")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    public async Task<ActionResult> CleanupMissingSyncedNotificationEvents(
        [FromServices] CleanupMissingSyncedNotificationsBatchHandler handler,
        [FromQuery] int batchCount = 100,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] Guid? startId = null)
    {
        if (batchCount <= 0)
        {
            return BadRequest(new { Message = "batchCount must be greater than zero", ProvidedValue = batchCount });
        }

        var processFromDate = startDate ?? DateTimeOffset.MaxValue;
        var sanitizedStartIdForLog = startId?.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);

        _logger.LogInformation(
            "Starting notification events cleanup batch processing. Batch size: {BatchCount}, Starting from: {StartDate}, Start Id: {StartId}", 
            batchCount, 
            processFromDate,
            sanitizedStartIdForLog);

        await handler.Process(batchCount, processFromDate, startId);

        return Ok(new 
        { 
            Message = "Notification events cleanup started", 
            BatchCount = batchCount, 
            StartingFrom = processFromDate,
            StartingId = startId
        });
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}

public class TriggerMaskinportenJwkRotationRequest
{
    public string Confirmation { get; set; } = string.Empty;
}
