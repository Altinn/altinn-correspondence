using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
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
                data => Ok(MigrateCorrespondenceMapper.MapToExternal(data)),
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
                result => Ok(MigrateCorrespondenceMapper.MakeAvailableResponseToExternal(result)),
                Problem
            );
        }

        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        [Route("{correspondenceId}/sync/markasread")]

        public async Task<ActionResult> SyncMarkAsRead(
            Guid correspondenceId,
            [FromQuery] Guid partyUuid,
            [FromServices] SyncCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sync from Altinn 2: Marking Correspondence as read for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new SyncCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Read,
                PartyUuid = partyUuid
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = AuthorizationConstants.Migrate)]        
        [Route("{correspondenceId}/sync/confirm")]
        public async Task<ActionResult> SyncConfirm(
            Guid correspondenceId,
            [FromQuery] Guid partyUuid,
            [FromServices] SyncCorrespondenceStatusHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sync from Altinn 2: Marking Correspondence as confirmed for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new SyncCorrespondenceStatusRequest
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Confirmed,
                PartyUuid = partyUuid
            }, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }
        
        [HttpDelete]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{correspondenceId}/sync/purge")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncPurge(
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

        [HttpDelete]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{correspondenceId}/sync/archive")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult> SyncArchive(
            Guid correspondenceId,
            [FromServices] PurgeCorrespondenceHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Archive Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new PurgeCorrespondenceRequest()
            {
                CorrespondenceId = correspondenceId
            }, HttpContext.User, cancellationToken);

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