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
                AttachmentType = InitializeAttachmentExt.AttachmentType,
                Checksum = InitializeAttachmentExt.Checksum,
                IsEncrypted = InitializeAttachmentExt.IsEncrypted,
                AttachmentStatus = AttachmentStatusExt.Initialized,
                AttachmentStatusText = "Initialized - awaiting upload",
                AttachmentStatusChanged = DateTime.Now                
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
                AttachmentStatus = Models.Enums.AttachmentStatusExt.UploadProcessing,
                AttachmentStatusText = "Uploaded - Awaitng procesing",
                AttachmentStatusChanged = DateTime.Now
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
                AttachmentStatus = Models.Enums.AttachmentStatusExt.Published,
                AttachmentStatusText = "Ready for use",
                AttachmentStatusChanged = DateTime.Now
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
                AttachmentStatus = Models.Enums.AttachmentStatusExt.Published,
                AttachmentStatusText = "Ready for use",
                AttachmentStatusChanged = DateTime.Now
            };
        }
    }
}
