using Altinn.Correspondence.API.Swagger;
using Altinn.Correspondence.API.Filters;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.GenerateReport;
using Altinn.Correspondence.API.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[HideFromPublicApi]
[Route("correspondence/api/v1/statistics")]
[ServiceFilter(typeof(StatisticsApiKeyFilter))]
public class StatisticsController(ILogger<StatisticsController> logger) : Controller
{
    private readonly ILogger<StatisticsController> _logger = logger;

    /// <summary>
    /// Enqueue generation of a monthly or daily summary report (one row per notification)
    /// </summary>
    /// <remarks>
    /// Enqueues a Hangfire background job that builds a parquet file and uploads/overwrites it
    /// in blob storage. Defaults to the preceding UTC day. Pass year+month for a monthly report,
    /// or year+month+day for a single completed UTC day (today and future days are rejected).
    /// The daily recurring job still regenerates the current (or previous) month so older monthly
    /// files stay unchanged. Returns immediately with a job id. Use the download endpoint after
    /// the job has completed. Requires API key authentication via X-API-Key header.
    /// Rate limiting is enforced per IP address.
    /// </remarks>
    /// <param name="request">Request parameters including whether to include Altinn2 correspondences</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="202">Returns the enqueued Hangfire job id</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-daily-summary")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EnqueueDailySummaryReportResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateDailySummary(
        [FromBody] GenerateDailySummaryReportRequest request,
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to enqueue daily summary report generation received");

        try
        {
            // Use default request if none provided
            request ??= new GenerateDailySummaryReportRequest()
            {
                Altinn2Included = false
            };
            
            var result = await handler.Process(request, cancellationToken);
            
            return result.Match(
                response => Accepted(response),
                Problem
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue daily summary report generation");
            return StatusCode(500, "Failed to enqueue daily summary report generation");
        }
    }

    /// <summary>
    /// Endpoint exists for legacy reasons. When Finops has changed to use new endpoint this should be deleted.
    /// </summary>
    /// <param name="request">Request parameters including whether to include Altinn2 correspondences</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the parquet file with metadata</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-and-download-daily-summary")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateAndDownloadDailySummary(
        [FromBody] GenerateDailySummaryReportRequest request,
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        // Exists for legacy reasons - does same as DownloadDailySummary
        _logger.LogInformation("Request to generate and download daily summary report received");

        try
        {
            // Use default request if none provided
            request ??= new GenerateDailySummaryReportRequest()
            {
                Altinn2Included = false
            };

            var result = await handler.DownloadReportFile(request, cancellationToken);

            return result.Match(
                response => {
                    // Add metadata to response headers
                    Response.Headers["X-File-Hash"] = response.FileHash;
                    Response.Headers["X-File-Size"] = response.FileSizeBytes.ToString();
                    Response.Headers["X-Service-Owner-Count"] = response.ServiceOwnerCount.ToString();
                    Response.Headers["X-Total-Correspondence-Count"] = response.TotalCorrespondenceCount.ToString();
                    Response.Headers["X-Generated-At"] = response.GeneratedAt.ToString("O"); // ISO 8601 format
                    Response.Headers["X-Environment"] = response.Environment;
                    Response.Headers["X-Altinn2-Included"] = response.Altinn2Included.ToString();

                    return File(response.FileStream, "application/octet-stream", response.FileName);
                },
                Problem
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and download daily summary report");
            return StatusCode(500, "Failed to generate and download daily summary report");
        }
    }
    /// <summary>
    /// Download a monthly or daily summary report with one row per notification
    /// </summary>
    /// <remarks>
    /// Returns the parquet file for the requested UTC month or day. Defaults to the preceding
    /// UTC day when year/month/day are omitted. Today and future UTC days are rejected.
    /// If a single-day report does not exist yet, it is generated inline in the request
    /// (days are small enough). Monthly reports must already exist (enqueue generate first).
    /// Requires API key authentication via X-API-Key header.
    /// Rate limiting is enforced per IP address.
    /// </remarks>
    /// <param name="request">Request parameters including optional year/month/day and whether to include Altinn2 correspondences</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the parquet file with metadata</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="404">Report for the requested month was not found (monthly only)</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("download-daily-report")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DownloadDailySummary(
        [FromBody] GenerateDailySummaryReportRequest request,
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to download daily summary report received");

        try
        {
            // Use default request if none provided
            request ??= new GenerateDailySummaryReportRequest()
            {
                Altinn2Included = false
            };

            var result = await handler.DownloadReportFile(request, cancellationToken);

            return result.Match(
                response => {
                    // Add metadata to response headers
                    Response.Headers["X-File-Hash"] = response.FileHash;
                    Response.Headers["X-File-Size"] = response.FileSizeBytes.ToString();
                    Response.Headers["X-Service-Owner-Count"] = response.ServiceOwnerCount.ToString();
                    Response.Headers["X-Total-Correspondence-Count"] = response.TotalCorrespondenceCount.ToString();
                    Response.Headers["X-Generated-At"] = response.GeneratedAt.ToString("O"); // ISO 8601 format
                    Response.Headers["X-Environment"] = response.Environment;
                    Response.Headers["X-Altinn2-Included"] = response.Altinn2Included.ToString();

                    return File(response.FileStream, "application/octet-stream", response.FileName);
                },
                Problem
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and download daily summary report");
            return StatusCode(500, "Failed to generate and download daily summary report");
        }
    }



    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
