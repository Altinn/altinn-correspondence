﻿using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CheckNotification;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
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
        /// Scopes: <br />
        /// - altinn:correspondence.write <br />
        /// Requires uploads of specified attachments if any before it can be Published
        /// </remarks>
        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(InitializeCorrespondencesResponseExt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<InitializeCorrespondencesResponseExt>> InitializeCorrespondences(
            InitializeCorrespondencesExt request,
            [FromServices] InitializeCorrespondencesHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(request.Correspondence);
            _logger.LogInformation("Initialize correspondences");

            var commandRequest = InitializeCorrespondencesMapper.MapToRequest(request.Correspondence, request.Recipients, null, request.ExistingAttachments);
            var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(InitializeCorrespondencesMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Initialize Correspondences and uploads attachments in the same request
        /// </summary>
        /// <remarks>
        /// Scopes: <br />
        /// - altinn:correspondence.write
        /// </remarks>
        /// <returns>
        /// CorrespondenceIds
        /// </returns>
        [HttpPost]
        [Route("upload")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(InitializeCorrespondencesResponseExt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<InitializeCorrespondencesResponseExt>> UploadCorrespondences(
            [FromForm] InitializeCorrespondencesExt request,
            [FromForm] List<IFormFile> attachments,
            [FromServices] InitializeCorrespondencesHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(request.Correspondence);
            _logger.LogInformation("Insert correspondences with attachment data");

            Request.EnableBuffering();

            var commandRequest = InitializeCorrespondencesMapper.MapToRequest(request.Correspondence, request.Recipients, attachments, request.ExistingAttachments);
            var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(InitializeCorrespondencesMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Get information about the Correspondence and its current status
        /// </summary>
        /// <remarks>
        ///  Scopes: <br />
        ///  - altinn:correspondence.read <br />
        ///  - altinn:correspondence.write <br />
        /// Mostly for use by recipients and occasional status checks
        /// </remarks>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CorrespondenceOverviewExt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        public async Task<ActionResult<CorrespondenceOverviewExt>> GetCorrespondenceOverview(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new GetCorrespondenceOverviewRequest()
            {
                CorrespondenceId = correspondenceId
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(CorrespondenceOverviewMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Get more detailed information about the Correspondence and its current status as well as noticiation statuses, if available
        /// </summary>
        /// <remarks>
        ///  Scopes: <br />
        ///  - altinn:correspondence.read <br />
        ///  - altinn:correspondence.write <br />
        /// Meant for Senders that want a complete overview of the status and history of the Correspondence, but also available for Receivers
        /// </remarks>
        /// <returns>Detailed information about the correspondence with current status and status history</returns>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CorrespondenceDetailsExt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{correspondenceId}/details")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        public async Task<ActionResult<CorrespondenceDetailsExt>> GetCorrespondenceDetails(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceDetailsHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence details for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new GetCorrespondenceDetailsRequest()
            {
                CorrespondenceId = correspondenceId,
            }, HttpContext.User, cancellationToken);

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
        [Produces("text/plain")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient, AuthenticationSchemes = AuthorizationConstants.AltinnTokenOrDialogportenScheme)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult> GetCorrespondenceContent(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence content for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new GetCorrespondenceOverviewRequest()
            {
                CorrespondenceId = correspondenceId
            }, HttpContext.User, cancellationToken);
            return commandResult.Match(
                data =>
                {
                    var messageBody = data.Content.MessageBody;
                    return Ok(messageBody);
                },
                Problem
            );
        }

        /// <summary>
        /// Gets a list of Correspondences for the authenticated user
        /// </summary>
        /// <remarks>
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// - altinn:correspondence.write <br />
        /// Meant for Receivers, but also available for Senders to track Correspondences
        /// </remarks>
        /// <returns>A list of Correspondence ids</returns>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CorrespondencesExt), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<CorrespondencesExt>> GetCorrespondences(
            [FromQuery] string resourceId,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromServices] GetCorrespondencesHandler handler,
            [FromQuery] CorrespondenceStatusExt? status,
            [FromQuery, RequiredEnum] CorrespondencesRoleType role,
            [FromQuery] string? onBehalfOf,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Get correspondences for receiver");

            var commandResult = await handler.Process(new GetCorrespondencesRequest
            {
                ResourceId = resourceId,
                From = from,
                To = to,
                Status = status is null ? null : (CorrespondenceStatus)status,
                Role = role,
                OnBehalfOf = onBehalfOf
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Mark Correspondence found by ID as read
        /// </summary>
        /// <remarks>
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// </remarks>
        /// <returns>StatusId</returns>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
                Status = CorrespondenceStatus.Archived,
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
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// - altinn:correspondence.write <br /> (Can only purge before the correspondence is published)
        /// </remarks>
        /// <returns>Ok</returns>
        [HttpDelete]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{correspondenceId}/purge")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        [EnableCors(AuthorizationConstants.ArbeidsflateCors)]
        public async Task<ActionResult> Purge(
            Guid correspondenceId,
            [FromServices] PurgeCorrespondenceHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Purging Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new PurgeCorrespondenceRequest()
            {
                CorrespondenceId = correspondenceId
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }


        /// <summary>
        /// Downloads the attachment data
        /// </summary>
        /// <remarks>
        /// Scopes: <br />
        /// - altinn:correspondence.read <br />
        /// <returns></returns>
        [HttpGet]
        [Produces("application/octet-stream")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            }, HttpContext.User, cancellationToken);
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
            var commandResult = await handler.Process(correspondenceId, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
