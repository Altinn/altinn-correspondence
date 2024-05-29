using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.DownloadAttachmentQuery;
using Altinn.Correspondence.Application.GetAttachmentDetailsCommand;
using Altinn.Correspondence.Application.GetAttachmentOverviewCommand;
using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Altinn.Correspondence.Application.UploadAttachmentCommand;
using Altinn.Correspondence.Mappers;
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
    public async Task<ActionResult<AttachmentOverviewExt>> InitializeAttachment(InitializeAttachmentExt InitializeAttachmentExt, [FromServices] InitializeAttachmentCommandHandler handler, CancellationToken cancellationToken)
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
        [FromServices] UploadAttachmentCommandHandler handler,
        [FromServices] GetAttachmentOverviewCommandHandler attachmentOverviewHandler,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Uploading attachment {attachmentId}", attachmentId.ToString());

        Request.EnableBuffering();
        var commandResult = await handler.Process(new UploadAttachmentCommandRequest()
        {
            AttachmentId = attachmentId,
            UploadStream = Request.Body,
            ContentLength = Request.ContentLength ?? Request.Body.Length
        }, cancellationToken);
        var attachmentOverviewResult= await attachmentOverviewHandler.Process(attachmentId, cancellationToken);
        if (!attachmentOverviewResult.TryPickT0(out var attachmentOverview, out var error))
        {
            return Problem(error);
        }
        return commandResult.Match(
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
        [FromServices] GetAttachmentOverviewCommandHandler handler,
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
        [FromServices] GetAttachmentDetailsCommandHandler handler,
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
    /// Downloads the attachment data
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("{attachmentId}/download")]
    public async Task<ActionResult> DownloadAttachmentData(
        Guid attachmentId,
        [FromServices] DownloadAttachmentQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var commandResult = await handler.Process(new DownloadAttachmentQueryRequest()
        {
            AttachmentId = attachmentId
        }, cancellationToken);
        return commandResult.Match(
            result => File(result, "application/octet-stream"),
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
        Guid attachmentId)
    {
        // Should this just give back HTTP Status codes?
        return new AttachmentOverviewExt
        {
            AttachmentId = attachmentId,
            Name = "TestName",
            SendersReference = "1234",
            DataType = "application/pdf",
            IntendedPresentation = IntendedPresentationTypeExt.HumanReadable,
            Status = AttachmentStatusExt.Deleted,
            StatusText = "Attachment is deleted",
            StatusChanged = DateTimeOffset.Now
        };
    }
    private ActionResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
