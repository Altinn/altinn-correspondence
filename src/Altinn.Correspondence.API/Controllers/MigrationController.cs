using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("correspondence/api/v1/migration")]
    [Authorize]
    public class MigrationController : Controller
    {
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(ILogger<MigrationController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize a new Correspondence
        /// </summary>
        /// <remarks>
        /// Requires uploads of specified attachments if any before it can be Published
        /// If no attachments are specified, should go directly to Published
        /// </remarks>
        /// <returns>CorrespondenceId and attachmentIds</returns>
        [HttpPost]
        [Route("correspondence")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<CorrespondenceMigrationStatusExt>> MigrateCorrespondence(
            MigrateCorrespondenceExt migrateCorrespondence,
            [FromServices] MigrateCorrespondenceHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithMigrateCorrespondence(migrateCorrespondence);

            var commandRequest = MigrateCorrespondenceMapper.MapToRequest(migrateCorrespondence);
            var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(MigrateCorrespondenceMapper.MapCorrespondenceMigrationStatusToExternal(data)),
                Problem
            );
        }
        /// <summary>
        /// Upload attachment data to Altinn Correspondence blob storage
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("attachment")]
        [Consumes("application/octet-stream")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<AttachmentOverviewExt>> MigrateAttachmentData(
            [FromQuery]MigrateInitializeAttachmentExt initializeAttachmentExt,
            [FromServices] MigrateAttachmentHandler migrateAttachmentHandler,
            CancellationToken cancellationToken = default
        )
        {
            Guid attachmentId = Guid.NewGuid();

            Request.EnableBuffering();
            var attachment = new MigrateAttachmentRequest()
            {
                SenderPartyUuid = initializeAttachmentExt.SenderPartyUuid,
                UploadStream = Request.Body,
                ContentLength = Request.ContentLength ?? Request.Body.Length,
                Attachment = MigrateAttachmentMapper.MapToRequest(initializeAttachmentExt, Request).Attachment
            };
            attachment.Attachment.Id = Guid.NewGuid();
            var uploadAttachmentResult = await migrateAttachmentHandler.Process(attachment, HttpContext.User, cancellationToken);

            return uploadAttachmentResult.Match(
                attachment => Ok(attachment.AttachmentId),
                Problem
            );
        }

        /// <summary>
        /// Creates a dialog with connected activities in Dialogporten.
        /// Also sets the IsMigrating value to false, which makes the correspondence available in the Altinn 3 Correspondence API.
        /// Setting 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("makemigratedcorrespondenceavailable")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<MakeCorrespondenceAvailableResponseExt>> MakeMigratedCorrespondenceAvailable(
            MakeCorrespondenceAvailableRequestExt request, 
            [FromServices] MigrateCorrespondenceHandler migrateCorrespondenceHandler,
            CancellationToken cancellationToken = default
        )
        {
            var internalRequest = MigrateCorrespondenceMapper.MapMakeAvailableToInternal(request);
            var result = await migrateCorrespondenceHandler.MakeCorrespondenceAvailable(internalRequest, cancellationToken);

            return result.Match(
                result => Ok(MigrateCorrespondenceMapper.MapMakeAvailableResponseToExternal(result)),
                Problem
            );
        }

        /// <summary>
        /// Syncronizes an event that occurred in Altinn 2 on an migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("syncStatusEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncStatusEvent(
            SyncCorrespondenceStatusEventRequestExt request,
            [FromServices] SyncCorrespondenceStatusEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - {request.SyncedEvent.Status} for correspondence {request.CorrespondenceId}");

            var commandRequest = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);
            var commandResult = await handler.Process(commandRequest, null, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Syncronizes an forwarding event that occurred in Altinn 2 on an migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("syncForwardingEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncForwardingEvent(
            SyncCorrespondenceForwardingEventRequestExt request,
            [FromServices] SyncCorrespondenceForwardingEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - Forwarding Event for correspondence {request.CorrespondenceId}");

            throw new NotImplementedException();
            //var commandRequest = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);
            //var commandResult = await handler.Process(commandRequest, null, cancellationToken);


            //return commandResult.Match(
            //    data => Ok(data),
            //    Problem
            //);
        }

        /// <summary>
        /// Syncronizes an forwarding event that occurred in Altinn 2 on an migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("syncNotificationEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncNotificationEvent(
            SyncCorrespondenceNotificationEventRequestExt request,
            [FromServices] SyncCorrespondenceNotificationEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - Notification Event for correspondence {request.CorrespondenceId}");
            
            var commandRequest = MigrateCorrespondenceMapper.MapSyncCorrespondenceNotifcationEventToInternal(request);
            var commandResult = await handler.Process(commandRequest, null, cancellationToken);


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