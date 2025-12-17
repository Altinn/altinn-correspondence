using Altinn.Correspondence.API.Filters;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.Helpers;
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
    [SetJobOrigin("migrate")]
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
            [FromServices] ServiceOwnerHelper serviceOwnerHelper,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithMigrateCorrespondence(migrateCorrespondence);

            var commandRequest = await MigrateCorrespondenceMapper.MapToRequestAsync(migrateCorrespondence, serviceOwnerHelper, cancellationToken);
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
            [FromServices] ServiceOwnerHelper serviceOwnerHelper,
            CancellationToken cancellationToken = default
        )
        {
            Guid attachmentId = Guid.NewGuid();

            Request.EnableBuffering();
            var attachmentRequest = await MigrateAttachmentMapper.MapToRequestAsync(initializeAttachmentExt, Request, serviceOwnerHelper, cancellationToken);
            var attachment = new MigrateAttachmentRequest()
            {
                SenderPartyUuid = initializeAttachmentExt.SenderPartyUuid,
                UploadStream = Request.Body,
                ContentLength = Request.ContentLength ?? Request.Body.Length,
                Attachment = attachmentRequest.Attachment
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
        ///  Synchronizes an event that occurred in Altinn 2 on a migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("correspondence/syncStatusEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncStatusEvent(
            SyncCorrespondenceStatusEventRequestExt request,
            [FromServices] SyncCorrespondenceStatusEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - {request.SyncedEvents.Count} # of StatusEvents for correspondence {request.CorrespondenceId}");

            var commandRequest = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);
            var commandResult = await handler.Process(commandRequest, null, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Synchronizes a forwarding event that occurred in Altinn 2 on a migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("correspondence/syncForwardingEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncForwardingEvent(
            SyncCorrespondenceForwardingEventRequestExt request,
            [FromServices] SyncCorrespondenceForwardingEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - {request.SyncedEvents.Count} # of ForwardingEvents for correspondence {request.CorrespondenceId}");
                        
            var commandRequest = MigrateCorrespondenceMapper.MapSyncForwardingEventToInternal(request);
            var commandResult = await handler.Process(commandRequest, null, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Synchronizes a notification event that occurred in Altinn 2 on a migrated correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("correspondence/syncNotificationEvent")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncNotificationEvent(
            SyncCorrespondenceNotificationEventRequestExt request,
            [FromServices] SyncCorrespondenceNotificationEventHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Sync from Altinn 2 - {request.SyncedEvents.Count} # of NotificationEvents for correspondence {request.CorrespondenceId}");
            
            var commandRequest = MigrateCorrespondenceMapper.MapSyncCorrespondenceNotificationEventToInternal(request);
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