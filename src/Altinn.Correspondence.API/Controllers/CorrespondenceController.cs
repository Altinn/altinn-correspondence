using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Helpers;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        /// Initiate a new Correspondence
        /// </summary>
        /// <remarks>
        /// Requires uploads of specified attachments if any before it can be Published
        /// If no attachments are specified, should go directly to Published
        /// </remarks>
        /// <returns></returns>
        [HttpPost]
        public CorrespondenceOverviewExt InitiateCorrespondence(InitiateCorrespondenceExt initiateCorrespondence)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(initiateCorrespondence);
            _logger.LogInformation("Initiate correspondence");

            // Hack return for now
            return new CorrespondenceOverviewExt
            {
                    CorrespondenceId = Guid.NewGuid(),
                    Recipient = initiateCorrespondence.Recipient,
                    Content = initiateCorrespondence.Content,
                    ResourceId = initiateCorrespondence.ResourceId,
                    Sender = initiateCorrespondence.Sender,
                    SendersReference = initiateCorrespondence.SendersReference,
                    CreatedDateTime = DateTime.Now,
                    VisibleDateTime = initiateCorrespondence.VisibleDateTime,
                    Status = CorrespondenceStatusExt.Initialized,
                    StatusText = "Initialized Successfully - waiting for attachment upload",
                    StatusChanged = DateTime.Now
            };
        }

        /// <summary>
        /// Initiate a new Correspondence with attachment data as single operation
        /// </summary>
        /// <remarks>        
        /// TODO: How to solve this for multiple attachment data blobs?
        /// </remarks>
        /// <returns></returns>
        [HttpPost]
        public CorrespondenceOverviewExt InitiateCorrespondenceAndUploadData(InitiateCorrespondenceExt initiateCorrespondence)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(initiateCorrespondence);
            _logger.LogInformation("Insert correspondence");

            // Hack return for now
            return new CorrespondenceOverviewExt
            {
                CorrespondenceId = Guid.NewGuid(),
                Recipient = initiateCorrespondence.Recipient,
                Content = initiateCorrespondence.Content,
                ResourceId = initiateCorrespondence.ResourceId,
                Sender = initiateCorrespondence.Sender,
                SendersReference = initiateCorrespondence.SendersReference,
                CreatedDateTime = DateTime.Now,
                VisibleDateTime = initiateCorrespondence.VisibleDateTime,
                Status = CorrespondenceStatusExt.Published,
                StatusText = "Initialized and Published successfully",
                StatusChanged = DateTime.Now
            };
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
        public CorrespondenceOverviewExt GetCorrespondenceOverview(
            Guid correspondenceId)
        {   
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            // Hack return for now
            return new CorrespondenceOverviewExt
            {
                CorrespondenceId = correspondenceId,
                Recipient = "0192:234567890",
                Content = null,
                ResourceId = "Altinn-Correspondence-1_0",
                Sender = "0192:123456789",
                SendersReference = Guid.NewGuid().ToString(),
                CreatedDateTime = DateTime.Now.AddDays(-2),
                VisibleDateTime = DateTime.Now.AddDays(-1),
                Status = CorrespondenceStatusExt.Published,
                StatusText = "Initialized and Published successfully",
                StatusChanged = DateTime.Now.AddDays(-2)
            };
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
        public CorrespondenceDetailsExt GetCorrespondenceDetails(
            Guid correspondenceId)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            // Hack return for now
            return new CorrespondenceDetailsExt
            {
                CorrespondenceId = correspondenceId,
                Recipient = "0192:234567890",
                Content = null,
                ResourceId = "Altinn-Correspondence-1_0",
                Sender = "0192:123456789",
                SendersReference = Guid.NewGuid().ToString(),
                CreatedDateTime = DateTime.Now.AddDays(-2),
                VisibleDateTime = DateTime.Now.AddDays(-1),
                Notifications = new List<CorrespondenceNotificationExt> { 
                    new CorrespondenceNotificationExt { NotificationTemplate = "DefaultNewMessage", Created= DateTime.Now.AddDays(-1), RequestedSendTime = DateTime.Now.AddDays(-1), NotificationChannel = Models.Enums.NotificationChannelExt.Email },
                    new CorrespondenceNotificationExt { NotificationTemplate = "DefaultReminder", Created= DateTime.Now.AddDays(-1), RequestedSendTime = DateTime.Now.AddDays(13), NotificationChannel = Models.Enums.NotificationChannelExt.Sms }
                }
            }; ;
        }

        /// <summary>
        /// Upload attachment data
        /// </summary>
        /// <remarks>
        /// TODO: Remove this operation and instead just use the attachment controller to avoid duplicate operations/code ??
        /// </remarks>
        /// <returns></returns>
        [HttpPost]
        [Route("{correspondenceId}/{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        public string UploadAttachmentData(
            Guid correspondenceId,
            Guid attachmentId
        )
        {
            _logger.LogInformation("Uploading attachment {attachmentId} for correspondence {correspondenceId}", attachmentId.ToString(), correspondenceId.ToString());

            // Hack return for now
            return "OK";
        }
    }
}
