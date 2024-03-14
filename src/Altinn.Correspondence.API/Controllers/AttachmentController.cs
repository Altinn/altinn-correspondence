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
        /// Insert a new Attachment
        /// </summary>
        /// <remarks>Only required if the attachment is to be shared</remarks>
        /// <returns></returns>
        [HttpPost]
        public AttachmentOverviewExt InitiateAttachment(InitiateAttachmentExt initiateAttachmentExt)
        {
            //LogContextHelpers.EnrichLogsWithInsertCorrespondence(initiateAttachmentExt);
            _logger.LogInformation("Initiate attachment");
            //var commandRequest = InsertCorrespondenceMapper.MapToRequest(insertCorrespondenceExt);
            // var commandResult = await handler.Process(commandRequest);
            //return commandResult.Match(
            //    attachmentId => Ok(attachmentId.ToString()),
            //    Problem
            //);
            
            // Hack for now
            return new AttachmentOverviewExt
            {
                AttachmentId = Guid.NewGuid(),
                AvailableForResourceIds = initiateAttachmentExt.AvailableForResourceIds,
                Name = initiateAttachmentExt.Name,
                FileName = initiateAttachmentExt.FileName,
                SendersReference = initiateAttachmentExt.SendersReference,
                AttachmentType = initiateAttachmentExt.AttachmentType,
                Checksum = initiateAttachmentExt.Checksum,
                IsEncrypted = initiateAttachmentExt.IsEncrypted,
                AttachmentStatus = AttachmentStatusExt.Initialized,
                AttachmentStatusText = "Initialized - awaiting upload",
                AttachmentStatusChanged = DateTime.Now                
            };
        }

        /// <summary>
        /// Upload attachment data
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
            //Request.EnableBuffering();
            //var commandResult = await handler.Process(new UploadFileCommandRequest()
            //{
            //    AttachmentId = attachmentId,
            //    Token = token,
            //    Filestream = Request.Body
            //});
            //return commandResult.Match(
            //    attachmentId => Ok(fileId.ToString()),
            //    Problem
            //);

            // Hack for now AttachmentOverviewExt with status UploadProcessing
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
            //LogContextHelpers.EnrichLogsWithToken(legacyToken);
            //_logger.LogInformation("Legacy - Getting file overview for {fileId}", fileId.ToString());
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
            //LogContextHelpers.EnrichLogsWithToken(token);
            //_logger.LogInformation("Downloading file {fileId}", fileId.ToString());
            //var queryResult = await handler.Process(new DownloadFileQueryRequest()
            //{
            //    FileId = fileId,
            //    Token = token
            //});
            //return queryResult.Match<ActionResult>(
            //    result => File(result.Stream, "application/octet-stream", result.Filename),
            //    Problem
            //);

            return "OK";
        }

        /// <summary>
        /// Deletes the attachment
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Route("{attachmentId}")]
        public AttachmentOverviewExt DeleteAttachment(
            Guid attachmentId)
        {
            //LogContextHelpers.EnrichLogsWithToken(token);
            //_logger.LogInformation("Downloading file {fileId}", fileId.ToString());
            //var queryResult = await handler.Process(new DownloadFileQueryRequest()
            //{
            //    FileId = fileId,
            //    Token = token
            //});
            //return queryResult.Match<ActionResult>(
            //    result => File(result.Stream, "application/octet-stream", result.Filename),
            //    Problem
            //);

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
