using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.DownloadAttachment;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [Route("correspondence/api/v1/correspondence")]
    public class CorrespondenceController : Controller
    {
        private readonly ILogger<CorrespondenceController> _logger;

        public CorrespondenceController(ILogger<CorrespondenceController> logger)
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
        public async Task<ActionResult<CorrespondenceOverviewExt>> InitializeCorrespondence(InitializeCorrespondenceExt initializeCorrespondence, [FromServices] InitializeCorrespondenceHandler handler, CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(initializeCorrespondence);
            _logger.LogInformation("Initialize correspondence");

            var commandRequest = InitializeCorrespondenceMapper.MapToRequest(initializeCorrespondence);
            var commandResult = await handler.Process(commandRequest, cancellationToken);

            return commandResult.Match(
                data => Ok(new InitializeCorrespondenceResponseExt()
                {
                    CorrespondenceId = data.CorrespondenceId,
                    AttachmentIds = data.AttachmentIds
                }),
                Problem
            );
        }

        /// <summary>
        /// Initialize a new Correspondence with attachment data as single operation
        /// </summary>
        /// <remarks>        
        /// TODO: How to solve this for multiple attachment data blobs?
        /// </remarks>
        /// <returns></returns>
        [HttpPost]
        [Route("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult<CorrespondenceOverviewExt>> InitializeCorrespondenceAndUploadData(InitializeCorrespondenceExt initializeCorrespondence)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(initializeCorrespondence);
            _logger.LogInformation("Insert correspondence");

            // Hack return for now
            return Ok(
                new CorrespondenceOverviewExt
                {
                    CorrespondenceId = Guid.NewGuid(),
                    Recipient = initializeCorrespondence.Recipient,
                    Content = initializeCorrespondence.Content != null ? (CorrespondenceContentExt)initializeCorrespondence.Content : null,
                    ResourceId = initializeCorrespondence.ResourceId,
                    Sender = initializeCorrespondence.Sender,
                    SendersReference = initializeCorrespondence.SendersReference,
                    Created = DateTimeOffset.UtcNow,
                    VisibleFrom = initializeCorrespondence.VisibleFrom,
                    Status = CorrespondenceStatusExt.Published,
                    StatusText = "Initialized and Published successfully",
                    StatusChanged = DateTimeOffset.UtcNow
                }
            );
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
        /// Meant for Senders that want a complete overview of the status and history of the Correspondence
        /// </remarks>
        /// <returns>Detailed information about the correspondence with current status and status history</returns>
        [HttpGet]
        [Route("{correspondenceId}/details")]
        public async Task<ActionResult<CorrespondenceDetailsExt>> GetCorrespondenceDetails(
            Guid correspondenceId,
            [FromServices] GetCorrespondenceDetailsHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(correspondenceId, cancellationToken);

            return commandResult.Match(
                data => Ok(CorrespondenceDetailsMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Gets a list of Correspondences for the authenticated user
        /// </summary>
        /// <remarks>
        /// Meant for Receivers
        /// </remarks>
        /// <returns>A list of Correspondence ids and pagination metadata</returns>
        [HttpGet]
        public async Task<ActionResult<CorrespondencesExt>> GetCorrespondences(
            [FromQuery] int offset,
            [FromQuery] int limit,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromServices] GetCorrespondencesHandler handler,
            [FromQuery] CorrespondenceStatusExt status = CorrespondenceStatusExt.Published,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Get correspondences for receiver");

            var commandResult = await handler.Process(new GetCorrespondencesRequest
            {
                from = from,
                limit = limit,
                offset = offset,
                status = (CorrespondenceStatus)status,
                to = to

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
        [Route("{correspondenceId}/delete")]
        public async Task<ActionResult> Delete(
            Guid correspondenceId)
        {
            _logger.LogInformation("Deleting Correspondence with id: {correspondenceId}", correspondenceId.ToString());

            return Ok();
        }


        /// <summary>
        /// Downloads the attachment data
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("attachment/{attachmentId}/download")]
        public async Task<ActionResult> DownloadAttachmentData(
            Guid attachmentId,
            [FromServices] DownloadAttachmentHandler handler,
            CancellationToken cancellationToken)
        {
            var commandResult = await handler.Process(new DownloadAttachmentRequest()
            {
                AttachmentId = attachmentId
            }, cancellationToken);
            return commandResult.Match(
                result => File(result, "application/octet-stream"),
                Problem
            );
        }

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
