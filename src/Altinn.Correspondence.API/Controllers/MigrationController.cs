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
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;

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
            _logger.LogInformation("{AttachmentId};Uploading attachment", attachmentId.ToString());

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

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}