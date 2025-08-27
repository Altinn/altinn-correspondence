using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.GenerateStatisticsReport;
using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]  // Hide from public API documentation for now
[Route("correspondence/api/v1/statistics")]
[Authorize]
public class StatisticsController(ILogger<StatisticsController> logger) : Controller
{
    private readonly ILogger<StatisticsController> _logger = logger;

    /// <summary>
    /// Generate statistics report for correspondence counts per service owner
    /// </summary>
    /// <remarks>
    /// This is an MVP implementation that generates a parquet file with correspondence statistics.
    /// The file is stored locally and contains counts per service owner.
    /// </remarks>
    /// <response code="200">Returns the generated report information</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-report")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]  // Require maintenance permissions for now
    [Produces("application/json")]
    [ProducesResponseType(typeof(GenerateStatisticsReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateStatisticsReport(
        [FromServices] GenerateStatisticsReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to generate statistics report received");
        
        var result = await handler.Process(HttpContext.User, cancellationToken);
        
        return result.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Download generated statistics report file
    /// </summary>
    /// <param name="fileName">Name of the file to download</param>
    /// <returns>The parquet file</returns>
    [HttpGet]
    [Route("download/{fileName}")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DownloadReport(string fileName)
    {
        _logger.LogInformation("Request to download statistics report file: {fileName}", fileName);
        
        // Basic validation to prevent directory traversal
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        {
            _logger.LogWarning("Invalid file name requested: {fileName}", fileName);
            return BadRequest("Invalid file name");
        }

        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        var filePath = Path.Combine(reportsDir, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Requested file not found: {filePath}", filePath);
            return NotFound("File not found");
        }

        try
        {
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {filePath}", filePath);
            return StatusCode(500, "Error reading file");
        }
    }

    /// <summary>
    /// List available statistics report files
    /// </summary>
    /// <returns>List of available report files</returns>
    [HttpGet]
    [Route("reports")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult ListReports()
    {
        _logger.LogInformation("Request to list available statistics reports");
        
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        
        if (!Directory.Exists(reportsDir))
        {
            return Ok(new List<object>());
        }

        try
        {
            var files = Directory.GetFiles(reportsDir, "*.parquet")
                .Select(file => new
                {
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Size = new FileInfo(file).Length,
                    Created = new FileInfo(file).CreationTime,
                    LastModified = new FileInfo(file).LastWriteTime
                })
                .OrderByDescending(f => f.Created)
                .ToList();

            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing report files");
            return StatusCode(500, "Error listing files");
        }
    }

    private ActionResult Problem(Error error) => Problem(
        detail: error.Message,
        statusCode: (int)error.StatusCode,
        extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
}
