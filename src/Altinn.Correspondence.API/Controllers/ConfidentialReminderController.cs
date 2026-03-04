using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondenceReminder;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Altinn.Correspondence.Application.GetUnreadConfidentialCorrespondences;
using Microsoft.AspNetCore.Cors;
using Altinn.Correspondence.Application.Helpers;


namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("correspondence/api/v1/confidential-reminders")]
[Authorize]
public class ConfidentialReminderController(ILogger<ConfidentialReminderController> logger) : Controller
{
    private readonly ILogger<ConfidentialReminderController> _logger = logger;

    /// <summary>
    /// Enqueue generation of confidential reminders for correspondences
    /// </summary>
    /// <response code="200">Returns the enqueued job id</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpGet]
    [Produces("text/plain")]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
    [ApiExplorerSettings(IgnoreApi = true)]
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
