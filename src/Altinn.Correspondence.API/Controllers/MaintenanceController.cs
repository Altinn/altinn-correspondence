using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CleanupOrphanedDialogs;
using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup orphaned dialogs received");
        var result = await handler.Process(HttpContext.User, cancellationToken);
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


