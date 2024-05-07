using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [Route("correspondence/api/v1/attachment")]
    public class AttachmentController : Controller
    {
        private readonly ILogger<CorrespondenceController> _logger;

        public AttachmentController(ILogger<CorrespondenceController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize a new Attachment
        /// </summary>
        /// <remarks>Only required if the attachment is to be shared, otherwise this is done as part of the Initialize Correspondence operation</remarks>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<AttachmentOverviewExt>> InitializeAttachment(InitializeAttachmentExt InitializeAttachmentExt, [FromServices] InitializeAttachmentCommandHandler handler, CancellationToken cancellationToken)
        {

            var commandRequest = InitializeAttachmentMapper.MapToRequest(InitializeAttachmentExt);
            var commandResult = await handler.Process(commandRequest, cancellationToken);
            _logger.LogInformation("Initialize attachment");

            return commandResult.Match(
                fileTransferId => Ok(fileTransferId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Upload attachment data to Altinn Correspondence blob storage
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        public async Task<ActionResult<AttachmentOverviewExt>> UploadAttachmentData(
            Guid attachmentId
        )
        {
            _logger.LogInformation("Uploading attachment {attachmentId}", attachmentId.ToString());

            // Hack return for now
            return Ok(
                new AttachmentOverviewExt
                {
                    AttachmentId = attachmentId,
                    Name = "TestName",
                    SendersReference = "1234",
                    DataType = "application/pdf",
                    IntendedPresentation = IntendedPresentationTypeExt.MachineReadable,
                    Status = AttachmentStatusExt.UploadProcessing,
                    StatusText = "Uploaded - Awaitng procesing",
                    StatusChanged = DateTimeOffset.Now
                }
            );
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}")]
        public async Task<ActionResult<AttachmentOverviewExt>> GetAttachmentOverview(
            Guid attachmentId)
        {
            // Hack return for now
            return Ok(
                new AttachmentOverviewExt
                {
                    AttachmentId = attachmentId,
                    Name = "TestName",
                    SendersReference = "1234",
                    DataType = "application/pdf",
                    IntendedPresentation = IntendedPresentationTypeExt.HumanReadable,
                    Status = AttachmentStatusExt.Published,
                    StatusText = "Published - Ready for use",
                    StatusChanged = DateTimeOffset.Now
                }
            );
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}/details")]
        public async Task<ActionResult<AttachmentDetailsExt>> GetAttachmentDetails(
            Guid attachmentId)
        {
            // Hack return for now
            return Ok(
                new AttachmentDetailsExt
                {
                    AttachmentId = attachmentId,
                    Name = "TestName",
                    SendersReference = "1234",
                    DataType = "application/pdf",
                    IntendedPresentation = IntendedPresentationTypeExt.HumanReadable,
                    Status = AttachmentStatusExt.Published,
                    StatusText = "Ready for use",
                    StatusChanged = DateTimeOffset.Now,
                    StatusHistory = new List<AtachmentStatusEvent>() {
                        new AtachmentStatusEvent { Status = AttachmentStatusExt.Initialized, StatusChanged = DateTimeOffset.Now.AddDays(-1), StatusText = "Initialized - awaiting upload" },
                        new AtachmentStatusEvent { Status = AttachmentStatusExt.UploadProcessing, StatusChanged = DateTimeOffset.Now.AddDays(-1).AddMinutes(1), StatusText = "Uploaded - Awaitng procesing" },
                        new AtachmentStatusEvent { Status = AttachmentStatusExt.Published, StatusChanged = DateTimeOffset.Now.AddDays(-1).AddMinutes(2), StatusText = "Published - Ready for use" },
                    }
                }
            );
        }

        /// <summary>
        /// Downloads the attachment data
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}/download")]
        public async Task<ActionResult> DownloadAttachmentData(
            Guid attachmentId)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes("justabinarydatastream");
            MemoryStream stream = new MemoryStream(byteArray);
            // Ugly Hack return for now
            return File(stream, "application/octet-stream");
        }

        /// <summary>
        /// Deletes the attachment
        /// </summary>
        /// <remarks>
        /// TODO: Consider if this should not be a hard delete, but rather a soft delete and if it should then be a different HTTP operation
        /// </remarks>
        /// <returns></returns>
        [HttpDelete]
        [Route("{attachmentId}")]
        public async Task<ActionResult<AttachmentOverviewExt>> DeleteAttachment(
            Guid attachmentId)
        {
            // Should this just give back HTTP Status codes?
            return new AttachmentOverviewExt
            {
                AttachmentId = attachmentId,
                Name = "TestName",
                SendersReference = "1234",
                DataType = "application/pdf",
                IntendedPresentation = IntendedPresentationTypeExt.HumanReadable,
                Status = AttachmentStatusExt.Deleted,
                StatusText = "Attachment is deleted",
                StatusChanged = DateTimeOffset.Now
            };
        }
        private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
