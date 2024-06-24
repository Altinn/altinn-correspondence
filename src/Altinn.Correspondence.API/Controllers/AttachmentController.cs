﻿using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.PurgeAttachment;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[Route("correspondence/api/v1/attachment")]
public class AttachmentController(ILogger<CorrespondenceController> logger) : Controller
{
    private readonly ILogger<CorrespondenceController> _logger = logger;

    /// <summary>
    /// Initialize a new Attachment
    /// </summary>
    /// <remarks>Only required if the attachment is to be shared, otherwise this is done as part of the Initialize Correspondence operation</remarks>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<Guid>> InitializeAttachment(InitializeAttachmentExt InitializeAttachmentExt, [FromServices] InitializeAttachmentHandler handler, CancellationToken cancellationToken)
    {

        var commandRequest = InitializeAttachmentMapper.MapToRequest(InitializeAttachmentExt);
        var commandResult = await handler.Process(commandRequest, cancellationToken);
        _logger.LogInformation("Initialize attachment");

        return commandResult.Match(
            id => Ok(id.ToString()),
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
        Guid attachmentId,
        [FromServices] UploadAttachmentHandler uploadAttachmentHandler,
        [FromServices] GetAttachmentOverviewHandler attachmentOverviewHandler,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Uploading attachment {attachmentId}", attachmentId.ToString());

        Request.EnableBuffering();
        var uploadAttachmentResult = await uploadAttachmentHandler.Process(new UploadAttachmentRequest()
        {
            AttachmentId = attachmentId,
            UploadStream = Request.Body,
            ContentLength = Request.ContentLength ?? Request.Body.Length
        }, cancellationToken);
        var attachmentOverviewResult = await attachmentOverviewHandler.Process(attachmentId, cancellationToken);
        if (!attachmentOverviewResult.TryPickT0(out var attachmentOverview, out var error))
        {
            return Problem(error);
        }
        return uploadAttachmentResult.Match(
            attachment => Ok(AttachmentOverviewMapper.MapToExternal(attachmentOverview)),
            Problem
        );
    }

    /// <summary>
    /// Get information about the file and its current status
    /// </summary>
    /// <returns>AttachmentOverviewExt</returns>
    [HttpGet]
    [Route("{attachmentId}")]
    public async Task<ActionResult<AttachmentOverviewExt>> GetAttachmentOverview(
        Guid attachmentId,
        [FromServices] GetAttachmentOverviewHandler handler,
        CancellationToken cancellationToken)
    {

        _logger.LogInformation("Get attachment overview {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, cancellationToken);

        return commandResult.Match(
            attachment => Ok(AttachmentOverviewMapper.MapToExternal(attachment)),
            Problem
        );
    }

    /// <summary>
    /// Get information about the file and its current status
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("{attachmentId}/details")]
    public async Task<ActionResult<AttachmentDetailsExt>> GetAttachmentDetails(
        Guid attachmentId,
        [FromServices] GetAttachmentDetailsHandler handler,
        CancellationToken cancellationToken)
    {

        _logger.LogInformation("Get attachment details {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, cancellationToken);

        return commandResult.Match(
            attachment => Ok(AttachmentDetailsMapper.MapToExternal(attachment)),
            Problem
        );
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
        Guid attachmentId,
        [FromServices] PurgeAttachmentHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delete attachment {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, cancellationToken);

        return commandResult.Match(
            _ => Ok(null),
            Problem
        );
    }
    private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
