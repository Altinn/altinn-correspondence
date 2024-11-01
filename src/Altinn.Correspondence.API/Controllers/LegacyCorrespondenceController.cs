﻿using System.Runtime.CompilerServices;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers
{
    /// <summary>
    /// The LegacyCorrespondenceController allows integration from the Altinn 2 Portal to allow legacy users access to Altinn 3 Correspondence
    /// As such it overrides some standad authentication mechanisms
    /// </summary>
    [ApiController]
    [Route("correspondence/api/v1/legacy/correspondence")]
    [Authorize(Policy = AuthorizationConstants.Legacy)]
    public class LegacyCorrespondenceController : Controller
    {
        private readonly ILogger<LegacyCorrespondenceController> _logger;

        public LegacyCorrespondenceController(ILogger<LegacyCorrespondenceController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get an overview of the Correspondence and its current status
        /// </summary>
        /// <remarks>
        /// Provides a summary for Receivers
        /// </remarks>
        /// <returns>Overview information about the correspondence</returns>
        [HttpGet]
        [Route("{correspondenceId}/overview")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<CorrespondenceOverviewExt>> GetCorrespondenceOverview(
            Guid correspondenceId,
            [FromQuery] int onBehalfOfPartyId,
            [FromServices] LegacyGetCorrespondenceOverviewHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence overview for {correspondenceId}", correspondenceId.ToString());

            var request = new LegacyGetCorrespondenceOverviewRequest
            {
                CorrespondenceId = correspondenceId,
                PartyId = onBehalfOfPartyId
            };

            var commandResult = await handler.Process(request, cancellationToken);

            return commandResult.Match(
                data => Ok(CorrespondenceOverviewMapper.MapToExternal(data)),
                Problem
            );
        }

        /// <summary>
        /// Get status history for the Correspondence, as well as notification statuses, if available
        /// </summary>
        [HttpGet]
        [Route("{correspondenceId}/history")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<LegacyGetCorrespondenceHistoryResponse>> GetCorrespondenceHistory( // TODO: Should this be LegacyCorrespondenceHistoryExt? 
            Guid correspondenceId,
            [FromQuery] string onBehalfOfPartyId,
            [FromServices] LegacyGetCorrespondenceHistoryHandler handler,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting Correspondence history for {correspondenceId}", correspondenceId.ToString());

            var commandResult = await handler.Process(new LegacyGetCorrespondenceHistoryRequest
            {
                CorrespondenceId = correspondenceId,
                OnBehalfOfPartyId = onBehalfOfPartyId
            }, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Gets a list of Correspondences for the authenticated user based on complex search criteria
        /// </summary>
        /// <returns>A list of overall Correspondence data and pagination metadata</returns>
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<CorrespondencesExt>> GetCorrespondences(
            LegacyGetCorrespondencesRequestExt request,
            [FromServices] LegacyGetCorrespondencesHandler handler,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Get correspondences for receiver");

            LegacyGetCorrespondencesRequest req = LegacyGetCorrespondencesMapper.MapToRequest(request);

            var commandResult = await handler.Process(req, cancellationToken);

            return commandResult.Match(
                data => Ok(data),
                Problem
            );
        }

        /// <summary>
        /// Download an attachment from a Correspondence
        /// </summary>
        [HttpGet]
        [Route("{correspondenceId}/attachment/{attachmentId}/download")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult> DownloadCorrespondenceAttachment(
            Guid correspondenceId,
            Guid attachmentId,
            [FromServices] LegacyDownloadCorrespondenceAttachmentHandler handler,
            CancellationToken cancellationToken)
        {
            var commandResult = await handler.Process(new DownloadCorrespondenceAttachmentRequest()
            {
                CorrespondenceId = correspondenceId,
                AttachmentId = attachmentId
            }, cancellationToken);

            return commandResult.Match<ActionResult>(
                result => File(result.Stream, "application/octet-stream", result.FileName),
                Problem
            );
        }

        private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
