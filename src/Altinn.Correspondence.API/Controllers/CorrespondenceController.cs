using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    [ApiController]
    [Route("correspondence/api/v1/correspondence")]
    public class FileController : Controller
    {
        private readonly ILogger<FileController> _logger;

        public FileController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Insert a new Correspondence
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string InsertCorrespondence(InsertCorrespondenceExt insertCorrespondenceExt)
        {
            LogContextHelpers.EnrichLogsWithInsertCorrespondence(insertCorrespondenceExt);
            _logger.LogInformation("Insert correspondence");
            //var commandRequest = InsertCorrespondenceMapper.MapToRequest(insertCorrespondenceExt);
            // var commandResult = await handler.Process(commandRequest);
            //return commandResult.Match(
            //    fileId => Ok(fileId.ToString()),
            //    Problem
            //);

            return "OK";
        }

        /// <summary>
        /// Get information about the Correspondence and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}")]
        public string GetCorrespondenceOverview(
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

            return "OK";
        }

        /// <summary>
        /// Get more detailed information about the Correspondence and its current status as well as noticiation statuses
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{correspondenceId}/details")]
        public string GetCorrespondenceDetails(
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

            return "OK";
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
