using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;

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
        public AttachmentOverviewExt InitializeAttachment(InitializeAttachmentExt InitializeAttachmentExt)
        {
            _logger.LogInformation("Initialize attachment");
            
            // Hack for now
            return new AttachmentOverviewExt
            {
                AttachmentId = Guid.NewGuid(),
                AvailableForResourceIds = InitializeAttachmentExt.AvailableForResourceIds,
                Name = InitializeAttachmentExt.Name,
                FileName = InitializeAttachmentExt.FileName,
                SendersReference = InitializeAttachmentExt.SendersReference,
                DataType = InitializeAttachmentExt.DataType,
                ConsumerType = InitializeAttachmentExt.ConsumerType,
                Checksum = InitializeAttachmentExt.Checksum,
                IsEncrypted = InitializeAttachmentExt.IsEncrypted,
                Status = AttachmentStatusExt.Initialized,
                StatusText = "Initialized - awaiting upload",
                StatusChanged = DateTime.Now                
            };
        }

        /// <summary>
        /// Upload attachment data to Altinn Correspondence blob storage
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        public AttachmentOverviewExt UploadAttachmentData(
            Guid attachmentId
        )
        {
            _logger.LogInformation("Uploading attachment {attachmentId}", attachmentId.ToString());

            // Hack return for now
            return new AttachmentOverviewExt{
                AttachmentId = attachmentId,
                AvailableForResourceIds = null,
                Name = "TestName",
                SendersReference = "1234",
                DataType = "application/pdf",
                ConsumerType = ConsumerTypeExt.Gui,
                Status = AttachmentStatusExt.UploadProcessing,
                StatusText = "Uploaded - Awaitng procesing",
                StatusChanged = DateTime.Now
            }; 
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}")]
        public AttachmentOverviewExt GetAttachmentOverview(
            Guid attachmentId)
        {
            // Hack return for now
            return new AttachmentOverviewExt
            {
                AttachmentId = attachmentId,
                AvailableForResourceIds = null,
                Name = "TestName",
                SendersReference = "1234",
                DataType = "application/pdf",
                ConsumerType = ConsumerTypeExt.Gui,
                Status = AttachmentStatusExt.Published,
                StatusText = "Published - Ready for use",
                StatusChanged = DateTime.Now
            };
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}/details")]
        public AttachmentDetailsExt GetAttachmentDetails(
            Guid attachmentId)
        {
            // Hack return for now
            return new AttachmentDetailsExt
            {
                AttachmentId = attachmentId,
                AvailableForResourceIds = null,
                Name = "TestName",
                SendersReference = "1234",
                DataType = "application/pdf",
                ConsumerType = ConsumerTypeExt.Gui,
                Status = AttachmentStatusExt.Published,
                StatusText = "Ready for use",
                StatusChanged = DateTime.Now,
                StatusHistory = new List<AtachmentStatusEvent>() {
                    new AtachmentStatusEvent { Status = AttachmentStatusExt.Initialized, StatusChanged = DateTime.Now.AddDays(-1), StatusText = "Initialized - awaiting upload" },
                    new AtachmentStatusEvent { Status = AttachmentStatusExt.UploadProcessing, StatusChanged = DateTime.Now.AddDays(-1).AddMinutes(1), StatusText = "Uploaded - Awaitng procesing" },
                    new AtachmentStatusEvent { Status = AttachmentStatusExt.Published, StatusChanged = DateTime.Now.AddDays(-1).AddMinutes(2), StatusText = "Published - Ready for use" },
                }
            };
        }

        /// <summary>
        /// Downloads the attachment data
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}/download")]
        public string DownloadAttachmentData(
            Guid attachmentId)
        {
            // Ugly Hack return for now
            return "binarydatastream";
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
        public AttachmentOverviewExt DeleteAttachment(
            Guid attachmentId)
        {
            // Should this just give back HTTP Status codes?
            return new AttachmentOverviewExt
            {
                AttachmentId = attachmentId,
                AvailableForResourceIds = null,
                Name = "TestName",
                SendersReference = "1234",
                DataType = "application/pdf",
                ConsumerType = ConsumerTypeExt.Gui,
                Status = AttachmentStatusExt.Published,
                StatusText = "Ready for use",
                StatusChanged = DateTime.Now
            };
        }
    }
}
