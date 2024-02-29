using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [Route("correspondence/api/v1/attachment")]
    public class SharedAttachmentController : Controller
    {
        private readonly ILogger<FileController> _logger;

        public SharedAttachmentController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Insert a new Correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string InitiateAttachment(InitiateSharedAttachmentExt initiateSharedAttachmentExt)
        {
            //LogContextHelpers.EnrichLogsWithInsertCorrespondence(initiateAttachmentExt);
            _logger.LogInformation("Initiate attachment");
            //var commandRequest = InsertCorrespondenceMapper.MapToRequest(insertCorrespondenceExt);
            // var commandResult = await handler.Process(commandRequest);
            //return commandResult.Match(
            //    fileId => Ok(fileId.ToString()),
            //    Problem
            //);

            return "OK"; // StandAloneAttachmentOverviewExt
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
            //    FileId = fileId,
            //    Token = token,
            //    Filestream = Request.Body
            //});
            //return commandResult.Match(
            //    fileId => Ok(fileId.ToString()),
            //    Problem
            //);


            return "OK"; // StandAloneAttachmentOverviewExt with status awaitprocessing
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
