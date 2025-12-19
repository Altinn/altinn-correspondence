using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.LegacyUpdateCorrespondenceStatus;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    /// <summary>
    /// The LegacyCorrespondenceController allows integration from the Altinn 2 Portal to allow legacy users access to Altinn 3 Correspondence
    /// As such it overrides some standad authentication mechanisms
    /// </summary>
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("correspondence/api/v1/legacy/correspondence")]
    [Authorize(Policy = AuthorizationConstants.Legacy)]
    public class LegacyCorrespondenceController : Controller
    {
        private readonly ILogger<LegacyCorrespondenceController> _logger;

        public LegacyCorrespondenceController(ILogger<LegacyCorrespondenceController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get an overview of the Correspondence and its current status
        /// </summary>
        /// <remarks>
        /// Provides a summary for Receivers
        /// </remarks>
        /// <returns>Overview information about the correspondence</returns>
        [HttpGet]
        [Route("{correspondenceId}/overview")]
        public async Task<ActionResult<CorrespondenceOverviewExt>> GetCorrespondenceOverview(
            Guid correspondenceId,
            [FromServices] LegacyGetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, HttpContext.User, cancellationToken);
            return commandResult.Match(
                data => Ok(LegacyCorrespondenceOverviewMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Get status history for the Correspondence, as well as notification statuses, if available
        /// </summary>
        [HttpGet]
        [Route("{correspondenceId}/history")]
        public async Task<ActionResult<LegacyCorrespondenceHistoryExt>> GetCorrespondenceHistory(
            Guid correspondenceId,
            [FromServices] LegacyGetCorrespondenceHistoryHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence history for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(LegacyCorrespondenceHistoryMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Gets a list of Correspondences for the authenticated user based on complex search criteria
        /// </summary>
        /// <returns>A list of overall Correspondence data</returns>
        [HttpPost]
        public async Task<ActionResult<CorrespondencesExt>> GetCorrespondences(
            LegacyGetCorrespondencesRequestExt request,
            [FromServices] LegacyGetCorrespondencesHandler handler,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Get correspondences for receiver");

            LegacyGetCorrespondencesRequest legacyRequest = LegacyGetCorrespondencesMapper.MapToRequest(request);

            var commandResult = await handler.Process(legacyRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Download an attachment from a Correspondence
        /// </summary>
        [HttpGet]
        [Route("{correspondenceId}/attachment/{attachmentId}/download")]
        public async Task<ActionResult> DownloadCorrespondenceAttachment(
            Guid correspondenceId,
            Guid attachmentId,
            [FromServices] LegacyDownloadCorrespondenceAttachmentHandler handler,
            CancellationToken cancellationToken)
        {
            var commandResult = await handler.Process(new DownloadCorrespondenceAttachmentRequest()
            {
                CorrespondenceId = correspondenceId,
                AttachmentId = attachmentId
            }, HttpContext.User, cancellationToken);

            return commandResult.Match<ActionResult>(
                result => File(result.Stream, "application/octet-stream", result.FileName),
                Problem
            );
        }

        /// <summary>
        /// Mark Correspondence found by ID as read
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Route("{correspondenceId}/markasread")]
        public async Task<ActionResult> MarkAsRead(
            Guid correspondenceId,
            [FromServices] LegacyUpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Marking Correspondence as read for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new LegacyUpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Read
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Mark Correspondence found by ID as confirmed
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Route("{correspondenceId}/confirm")]
        public async Task<ActionResult> Confirm(
            Guid correspondenceId,
            [FromServices] LegacyUpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Marking Correspondence as confirmed for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new LegacyUpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Confirmed
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Mark Correspondence found by ID as archived
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Route("{correspondenceId}/archive")]
        public async Task<ActionResult> Archive(
            Guid correspondenceId,
            [FromServices] LegacyUpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Archiving Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new LegacyUpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Archived
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Delete Correspondence found by ID
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>Ok</returns>
        [HttpDelete]
        [Route("{correspondenceId}/purge")]
        public async Task<ActionResult> Purge(
            Guid correspondenceId,
            [FromServices] LegacyPurgeCorrespondenceHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Purging Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }
        private ActionResult Problem(Error error) => Problem(
            detail: error.Message, 
            statusCode: (int)error.StatusCode, 
            extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
    }
}
