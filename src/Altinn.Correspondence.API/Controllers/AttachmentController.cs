using Altinn.Correspondence.API.Models;
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
        private readonly ILogger<FileController> _logger;

        public AttachmentController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Insert a new Attachment
        /// </summary>
        /// <remarks>Only required if the attachment is to be shared</remarks>
        /// <returns></returns>
        [HttpPost]
        public string InitiateAttachment(InitiateAttachmentExt initiateAttachmentExt)
        {
            //LogContextHelpers.EnrichLogsWithInsertCorrespondence(initiateAttachmentExt);
            _logger.LogInformation("Initiate attachment");
            //var commandRequest = InsertCorrespondenceMapper.MapToRequest(insertCorrespondenceExt);
            // var commandResult = await handler.Process(commandRequest);
            //return commandResult.Match(
            //    attachmentId => Ok(attachmentId.ToString()),
            //    Problem
            //);

            
            string attachmentId = Guid.NewGuid().ToString();
            return attachmentId; // StandAloneAttachmentOverviewExt
        }

        /// <summary>
        /// Upload attachment data
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{attachmentId}/upload")]
        [Consumes("application/octet-stream")]
        public string UploadAttachmentData(
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


            return "OK"; // AttachmentOverviewExt with status awaitprocessing
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{attachmentId}")]
        public string GetAttachmentOverview(
            Guid fileId)
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

            return "BLAH"; // StandAloneAttachmentOverviewExt
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
    }
}
