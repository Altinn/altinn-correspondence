using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CleanupOrphanedDialogs;
using Altinn.Correspondence.Application.CleanupPerishingDialogs;
using Altinn.Correspondence.Application.CleanupMarkdownAndHTMLInSummary;
using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
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
    /// Enqueue cleanup to remove expiresAt from dialogs in Dialogporten for correspondences where AllowSystemDeleteAfter has been set
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


    private ActionResult Problem(Error error) => Problem(
        detail: error.Message,
        statusCode: (int)error.StatusCode,
        extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
}
