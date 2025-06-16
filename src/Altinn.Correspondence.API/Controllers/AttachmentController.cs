using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.DownloadAttachment;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.PurgeAttachment;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[Route("correspondence/api/v1/attachment")]
[Authorize]
public class AttachmentController(ILogger<CorrespondenceController> logger) : Controller
{
    private readonly ILogger<CorrespondenceController> _logger = logger;

    /// <summary>
    /// Initialize a new Attachment to be shared in correspondences
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// Only required if the attachment is to be shared, otherwise this is done as part of the Initialize Correspondence operation
    /// </remarks>
    /// <response code="200">Returns the attachment id</response>
    /// <response code="400"><ul>
    /// <li>2010: Filename is missing</li>
    /// <li>2011: Filename is too long</li>
    /// <li>2012: Filename contains invalid characters</li>
    /// <li>2013: Filetype not allowed</li>
    /// <li>4002: Could not retrieve party uuid from lookup in Altinn Register </li>
    /// <li>4009: Resource type is not supported. Resource must be of type GenericAccessResource or CorrespondenceService. </li>
    /// </ul></response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult<Guid>> InitializeAttachment(
        InitializeAttachmentExt InitializeAttachmentExt,
        [FromServices] InitializeAttachmentHandler handler,
        CancellationToken cancellationToken)
    {
        var commandRequest = InitializeAttachmentMapper.MapToRequest(InitializeAttachmentExt);
        var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);
        _logger.LogInformation("Initialize attachment");

        return commandResult.Match(
            id => Ok(id.ToString()),
            Problem
        );
    }

    /// <summary>
    /// Upload attachment data to Altinn Correspondence blob storage
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// </remarks>
    /// <response code="200">Returns attachment metadata</response>
    /// <response code="400"><ul>
    /// <li>2003: Cannot upload attachment to a correspondence that has been created</li>
    /// <li>2004: File must have content and has a max file size of 250 MB</li>
    /// <li>2005: File has already been or is being uploaded</li>
    /// <li>2008: Checksum mismatch</li>
    /// <li>2009: Could not get data location url</li>
    /// <li>4002: Could not retrieve party uuid from lookup in Altinn Register</li>
    /// </ul></response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    /// <response code="404">2001: The requested attachment was not found</response>
    /// <response code="502">2002: Error occurred during upload</response>
    [HttpPost]
    [Produces("application/json")]
    [Route("{attachmentId}/upload")]
    [Consumes("application/octet-stream")]
    [ProducesResponseType(typeof(AttachmentOverviewExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
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
        }, HttpContext.User, cancellationToken);
        var attachmentOverviewResult = await attachmentOverviewHandler.Process(attachmentId, HttpContext.User, cancellationToken);
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
    /// Get information about the attachment and its current status
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// </remarks>
    /// <response code="200">Returns attachment metadata</response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    /// <response code="404">2001: The requested attachment was not found</response>
    [HttpGet]
    [Route("{attachmentId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AttachmentOverviewExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult<AttachmentOverviewExt>> GetAttachmentOverview(
        Guid attachmentId,
        [FromServices] GetAttachmentOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get attachment overview {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, HttpContext.User, cancellationToken);

        return commandResult.Match(
            attachment => Ok(AttachmentOverviewMapper.MapToExternal(attachment)),
            Problem
        );
    }

    /// <summary>
    /// Get information about the attachment and its current status
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// </remarks>
    /// <response code="200">Returns attachment metadata</response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    /// <response code="404">2001: The requested attachment was not found</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AttachmentDetailsExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Route("{attachmentId}/details")]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult<AttachmentDetailsExt>> GetAttachmentDetails(
        Guid attachmentId,
        [FromServices] GetAttachmentDetailsHandler handler,
        CancellationToken cancellationToken)
    {

        _logger.LogInformation("Get attachment details {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, HttpContext.User, cancellationToken);

        return commandResult.Match(
            attachment => Ok(AttachmentDetailsMapper.MapToExternal(attachment)),
            Problem
        );
    }

    /// <summary>
    /// Deletes the attachment
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// </remarks>
    /// <response code="200">Returns no data</response>
    /// <response code="400"><ul> 
    /// <li>2006: File has already been purged</li>
    /// <li>2007: Attachment cannot be purged as it is linked to at least one existing correspondence</li>
    /// <li>4002: Could not retrieve party uuid from lookup in Altinn Register</li>
    /// </ul></response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    /// <response code="404">2001: The requested attachment was not found</response>
    [HttpDelete]
    [Route("{attachmentId}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult<AttachmentOverviewExt>> DeleteAttachment(
        Guid attachmentId,
        [FromServices] PurgeAttachmentHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delete attachment {attachmentId}", attachmentId.ToString());

        var commandResult = await handler.Process(attachmentId, HttpContext.User, cancellationToken);

        return commandResult.Match(
            _ => Ok(null),
            Problem
        );
    }
    /// <summary>
    /// Downloads the attachment data
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:correspondence.write <br/>
    /// </remarks>
    /// <response code="200">Returns the attachment</response>
    /// <response code="401">4001: You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization</response>
    /// <response code="404">2001: The requested attachment was not found</response>
    [HttpGet]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Route("{attachmentId}/download")]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult> DownloadAttachmentData(
        Guid attachmentId,
        [FromServices] DownloadAttachmentHandler handler,
        CancellationToken cancellationToken)
    {
        var commandResult = await handler.Process(new DownloadAttachmentRequest()
        {
            AttachmentId = attachmentId
        }, HttpContext.User, cancellationToken);
        return commandResult.Match(
            result => File(result, "application/octet-stream"),
            Problem
        );
    }
    private ActionResult Problem(Error error) => Problem(
        detail: error.Message, 
        statusCode: (int)error.StatusCode, 
        extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
}
