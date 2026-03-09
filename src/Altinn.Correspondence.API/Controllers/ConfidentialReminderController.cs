using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;
using Altinn.Correspondence.Application.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;


namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("correspondence/api/v1/confidential-reminders")]
[Authorize]
public class ConfidentialReminderController(ILogger<ConfidentialReminderController> logger) : Controller
{
    private readonly ILogger<ConfidentialReminderController> _logger = logger;

    /// <summary>
    /// Get a list of unread correspondences with the IsConfidential flag set to true.
    /// </summary>
    /// <response code="200">Returns the list of unread confidential correspondences</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpGet]
    [Produces("text/plain")]
    [Authorize(Policy = AuthorizationConstants.Recipient)]
    [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
    public async Task<ActionResult> GetUnreadConfidentialCorrespondences(
        [FromServices] GetUnreadConfidentialCorrespondencesHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting unread confidential correspondences");
        var commandResult = await handler.Process(HttpContext.User, cancellationToken);
        return commandResult.Match(
            data => Content(MessageBodyHelpers.ConvertMixedToMarkdown(data.Text)),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);

}
