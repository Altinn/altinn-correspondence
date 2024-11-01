using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CheckNotification;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Application.UpdateMarkAsUnread;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [Route("correspondence/api/v1/correspondence")]
    [Authorize]
    public class CorrespondenceController : Controller
    {
        private readonly ILogger<CorrespondenceController> _logger;

        public CorrespondenceController(ILogger<CorrespondenceController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize Correspondences
        /// </summary>
        /// <remarks>
        /// Requires uploads of specified attachments if any before it can be Published
        /// </remarks>
        /// <returns>CorrespondenceIds</returns>
        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<InitializeCorrespondencesResponseExt>> InitializeCorrespondences(
            InitializeCorrespondencesExt request,
            [FromServices] InitializeCorrespondencesHandler handler,
            CancellationToken cancellationToken)
        {
                LogContextHelpers.EnrichLogsWithInsertCorrespondence(request.Correspondence);
                _logger.LogInformation("Initialize correspondences");

                var commandRequest = InitializeCorrespondencesMapper.MapToRequest(request.Correspondence, request.Recipients, null, request.ExistingAttachments, false);
                var commandResult = await handler.Process(commandRequest, cancellationToken);

                return commandResult.Match(
                    data => Ok(new InitializeCorrespondencesResponseExt()
                    {
                        Correspondences = data.Correspondences,
                        AttachmentIds = data.AttachmentIds
                    }),
                    Problem
                );
        }

        /// <summary>
        /// Initialize Correspondences with attachment data
        /// </summary>
        /// <returns>CorrespondenceIds/returns>
        [HttpPost]
        [Route("upload")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<InitializeCorrespondencesResponseExt>> UploadCorrespondences(
            [FromForm] InitializeCorrespondencesExt request,
            [FromForm] List<IFormFile> attachments,
            [FromServices] InitializeCorrespondencesHandler handler,
            CancellationToken cancellationToken)
        {

            throw new NotImplementedException();
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(request.Correspondence);
            _logger.LogInformation("Insert correspondences with attachment data");

            Request.EnableBuffering();

            var commandRequest = InitializeCorrespondencesMapper.MapToRequest(request.Correspondence, request.Recipients, attachments, request.ExistingAttachments, true);
            var commandResult = await handler.Process(commandRequest, cancellationToken);
            return Problem("An unexpected error occurred.", statusCode: 500);

            // return commandResult.Match(
            //     data => Ok(new InitializeCorrespondencesResponseExt()
            //     {
            //         Correspondences = data.Correspondences,
            //         AttachmentIds = data.AttachmentIds
            //     }),
            //     Problem
            // );
        }

        /// <summary>
        /// Get information about the Correspondence and its current status
        /// </summary>
        /// <remarks>
        /// Mostly for use by recipients and occasional status checks
        /// </remarks>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}")]
        [Produces("application/json")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        public async Task<ActionResult<CorrespondenceOverviewExt>> GetCorrespondenceOverview(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(CorrespondenceOverviewMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Get more detailed information about the Correspondence and its current status as well as noticiation statuses, if available
        /// </summary>
        /// <remarks>
        /// Meant for Senders that want a complete overview of the status and history of the Correspondence, but also available for Receivers
        /// </remarks>
        /// <returns>Detailed information about the correspondence with current status and status history</returns>
        [HttpGet]
        [Produces("application/json")]
        [Route("{correspondenceId}/details")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        public async Task<ActionResult<CorrespondenceDetailsExt>> GetCorrespondenceDetails(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceDetailsHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence details for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(CorrespondenceDetailsMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Gets the message body of a correspondence
        /// </summary>
        /// <remarks>
        /// Meant for use with Felles Arbeidsflate
        /// </remarks>
        /// <returns>Message body in markdown format</returns>
        [HttpGet]
        [Route("{correspondenceId}/content")]
        [Produces("application/vnd.dialogporten.frontchannelembed+json;type=markdown")]
        [Authorize(AuthenticationSchemes = AuthorizationConstants.DialogportenScheme)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        public async Task<ActionResult> GetCorrespondenceContent(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence content for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);
            return commandResult.Match(
                data => Ok(data.Content.MessageBody),
                Problem
            );
        }

        /// <summary>
        /// Gets a list of Correspondences for the authenticated user
        /// </summary>
        /// <remarks>
        /// Meant for Receivers, but also available for Senders to track Correspondences
        /// </remarks>
        /// <returns>A list of Correspondence ids and pagination metadata</returns>
        [HttpGet]
        [Produces("application/json")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<CorrespondencesExt>> GetCorrespondences(
            [FromQuery] string resourceId,
            [FromQuery] int offset,
            [FromQuery] int limit,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromServices] GetCorrespondencesHandler handler,
            [FromQuery] CorrespondenceStatusExt? status,
            [FromQuery, RequiredEnum] CorrespondencesRoleType role,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Get correspondences for receiver");

            var commandResult = await handler.Process(new GetCorrespondencesRequest
            {
                ResourceId = resourceId,
                From = from,
                To = to,
                Limit = limit,
                Offset = offset,
                Status = status is null ? null : (CorrespondenceStatus)status,
                Role = role,
            }, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
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
        [Authorize(Policy = AuthorizationConstants.Recipient, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Route("{correspondenceId}/markasread")]
        public async Task<ActionResult> MarkAsRead(
            Guid correspondenceId,
            [FromServices] UpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Marking Correspondence as read for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new UpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Read
            }, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Mark Correspondence found by ID as unread
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>OK</returns>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.Recipient, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Route("{correspondenceId}/markasunread")]
        public async Task<ActionResult> MarkAsUnread(
            Guid correspondenceId,
            [FromServices] UpdateMarkAsUnreadHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Marking Correspondence as unread for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(null),
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
        [Authorize(Policy = AuthorizationConstants.Recipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        [Route("{correspondenceId}/confirm")]
        public async Task<ActionResult> Confirm(
            Guid correspondenceId,
            [FromServices] UpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Marking Correspondence as confirmed for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new UpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Confirmed
            }, cancellationToken);

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
        [Authorize(Policy = AuthorizationConstants.Recipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        [Route("{correspondenceId}/archive")]
        public async Task<ActionResult> Archive(
            Guid correspondenceId,
            [FromServices] UpdateCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Archiving Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new UpdateCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Archived
            }, cancellationToken);

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
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult> Purge(
            Guid correspondenceId,
            [FromServices] PurgeCorrespondenceHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Purging Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }


        /// <summary>
        /// Downloads the attachment data
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}/attachment/{attachmentId}/download")]
        [Authorize(Policy = AuthorizationConstants.DownloadAttachmentPolicy, AuthenticationSchemes = AuthorizationConstants.AllSchemes)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        public async Task<ActionResult> DownloadCorrespondenceAttachmentData(
            Guid correspondenceId,
            Guid attachmentId,
            [FromServices] DownloadCorrespondenceAttachmentHandler handler,
            CancellationToken cancellationToken)
        {
            var commandResult = await handler.Process(new DownloadCorrespondenceAttachmentRequest()
            {
                CorrespondenceId = correspondenceId,
                AttachmentId = attachmentId
            }, cancellationToken);
            return commandResult.Match(
                result => File(result.Stream, "application/octet-stream", result.FileName),
                Problem
            );
        }

        /// <summary>
        /// Check if a reminder notification should be sent
        /// </summary>
        [HttpGet]
        [Route("{correspondenceId}/notification/check")]
        [Authorize(Policy = AuthorizationConstants.NotificationCheck)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult> CheckNotification(
            Guid correspondenceId,
            [FromServices] CheckNotificationHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Checking notification for Correspondence with id: {correspondenceId}", correspondenceId.ToString());
            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
