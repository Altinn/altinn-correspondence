using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.UploadAttachment;
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
            _logger.LogInformation("Initialize correspondence");

            var commandRequest = MigrateCorrespondenceMapper.MapToRequest(migrateCorrespondence);
            var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                data => Ok(MigrateCorrespondenceMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Initialize attachment for migration.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("attachment")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<Guid>> MigrateAttachment(
            MigrateInitializeAttachmentExt initializeAttachmentExt,
            [FromServices] MigrateInitializeAttachmentHandler migrateInitializeAttachmentHandler,
            CancellationToken cancellationToken = default
        )
        {
            _logger.LogInformation("{initializeAttachmentExt.SendersReference};Initializing attachment with sendersference", initializeAttachmentExt.SendersReference);
            var commandRequest = MigrateAttachmentMapper.MapToRequest(initializeAttachmentExt);
            var commandResult = await migrateInitializeAttachmentHandler.Process(commandRequest, HttpContext.User, cancellationToken);

            return commandResult.Match(
                attachmentId => Ok(attachmentId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Upload attachment data to Altinn Correspondence blob storage
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("attachment/{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<AttachmentOverviewExt>> UploadAttachmentData(
            Guid attachmentId,
            [FromServices] MigrateUploadAttachmentHandler migrateAttachmentHandler,
            CancellationToken cancellationToken = default
        )
        {
            _logger.LogInformation("{attachmentId};Uploading attachment", attachmentId.ToString());

            Request.EnableBuffering();
            var uploadAttachmentResult = await migrateAttachmentHandler.Process(new UploadAttachmentRequest()
            {
                AttachmentId = attachmentId,
                UploadStream = Request.Body,
                ContentLength = Request.ContentLength ?? Request.Body.Length
            }, HttpContext.User, cancellationToken);
            return uploadAttachmentResult.Match(
                attachment => Ok(AttachmentOverviewMapper.MapMigrateToExternal(attachment)),
                Problem
            );
        }

        /// <summary>
        /// Upload attachment data to Altinn Correspondence blob storage
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("attachment/{senderPartyUuid}/{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        [Authorize(Policy = AuthorizationConstants.Migrate)]
        public async Task<ActionResult<AttachmentOverviewExt>> UploadAttachmentDataV2(
            Guid senderPartyUuid,
            Guid attachmentId,
            [FromServices] MigrateUploadAttachmentHandler migrateAttachmentHandler,
            CancellationToken cancellationToken = default
        )
        {
            _logger.LogInformation("{attachmentId};Uploading attachment", attachmentId.ToString());

            Request.EnableBuffering();
            var uploadAttachmentResult = await migrateAttachmentHandler.Process(new UploadAttachmentRequest()
            {
                SenderPartyUuid = senderPartyUuid,
                AttachmentId = attachmentId,
                UploadStream = Request.Body,
                ContentLength = Request.ContentLength ?? Request.Body.Length
            }, HttpContext.User, cancellationToken);
            return uploadAttachmentResult.Match(
                attachment => Ok(AttachmentOverviewMapper.MapMigrateToExternal(attachment)),
                Problem
            );
        }

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}