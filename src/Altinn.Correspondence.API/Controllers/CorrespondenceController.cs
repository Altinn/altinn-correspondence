using Altinn.Correspondence.API.Models;
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
        /// Insert a new Correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public CorrespondenceOverviewExt InsertCorrespondence(InitiateCorrespondenceExt insertCorrespondenceExt)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(insertCorrespondenceExt);
            _logger.LogInformation("Insert correspondence");
            //var commandRequest = InsertCorrespondenceMapper.MapToRequest(insertCorrespondenceExt);
            // var commandResult = await handler.Process(commandRequest);
            //return commandResult.Match(
            //    fileId => Ok(fileId.ToString()),
            //    Problem
            //);

            // Hack for now
            return new CorrespondenceOverviewExt
            {
                    CorrespondenceId = Guid.NewGuid(),
                    Recipient = insertCorrespondenceExt.Recipient,
                    Content = insertCorrespondenceExt.Content,
                    ResourceId = insertCorrespondenceExt.ResourceId,
                    Sender = insertCorrespondenceExt.Sender,
                    SendersReference = insertCorrespondenceExt.SendersReference,
                    VisibleDateTime = insertCorrespondenceExt.VisibleDateTime
            };
        }

        /// <summary>
        /// Get information about the Correspondence and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}")]
        public CorrespondenceOverviewExt GetCorrespondenceOverview(
            Guid correspondenceId)
        {   
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());
            //var queryResult = await handler.Process(new GetFileOverviewQueryRequest()
            //{
            //    FileId = fileId,
            //    Token = legacyToken,
            //    IsLegacy = true
            //});
            //return queryResult.Match(
            //    result => Ok(LegacyFileStatusOverviewExtMapper.MapToExternalModel(result.File)),
            //    Problem
            //);

            // Hack for now
            return new CorrespondenceOverviewExt
            {
                CorrespondenceId = correspondenceId,
                Recipient = "0192:234567890",
                Content = null,
                ResourceId = "Altinn-Correspondence-1_0",
                Sender = "0192:123456789",
                SendersReference = Guid.NewGuid().ToString(),
                VisibleDateTime = DateTime.Now.AddDays(-1)
            };
        }

        /// <summary>
        /// Get more detailed information about the Correspondence and its current status as well as noticiation statuses
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}/details")]
        public CorrespondenceDetailsExt GetCorrespondenceDetails(
            Guid correspondenceId)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());
            //var queryResult = await handler.Process(new GetFileOverviewQueryRequest()
            //{
            //    FileId = fileId,
            //    Token = legacyToken,
            //    IsLegacy = true
            //});
            //return queryResult.Match(
            //    result => Ok(LegacyFileStatusOverviewExtMapper.MapToExternalModel(result.File)),
            //    Problem
            //);

            // Hack for now
            return new CorrespondenceDetailsExt
            {
                CorrespondenceId = correspondenceId,
                Recipient = "0192:234567890",
                Content = null,
                ResourceId = "Altinn-Correspondence-1_0",
                Sender = "0192:123456789",
                SendersReference = Guid.NewGuid().ToString(),
                VisibleDateTime = DateTime.Now.AddDays(-1),
                Notifications = new List<CorrespondenceNotificationExt> { new CorrespondenceNotificationExt 
                { NotificationTemplate = "DefaultNewMessage", Created= DateTime.Now.AddDays(-1), RequestedSendTime = DateTime.Now.AddDays(-1), NotificationChannel = Models.Enums.NotificationChannelExt.Email }, new CorrespondenceNotificationExt
                { NotificationTemplate = "DefaultReminder", Created= DateTime.Now.AddDays(-1), RequestedSendTime = DateTime.Now.AddDays(13), NotificationChannel = Models.Enums.NotificationChannelExt.Sms } }
            }; ;
        }

        /// <summary>
        /// Upload attachment data
        /// </summary>
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
            //Request.EnableBuffering();
            //var commandResult = await handler.Process(new UploadFileCommandRequest()
            //{
            //    FileId = fileId,
            //    Token = token,
            //    Filestream = Request.Body
            //});
            //return commandResult.Match(
            //    fileId => Ok(fileId.ToString()),
            //    Problem
            //);


            return "OK";
        }
    }
}
