using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.GenerateStatisticsReport;
using Microsoft.AspNetCore.Mvc;
using Parquet.Serialization;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]  // Hide from public API documentation for now
[Route("correspondence/api/v1/statistics")]
public class StatisticsController(ILogger<StatisticsController> logger) : Controller
{
    private readonly ILogger<StatisticsController> _logger = logger;

    /// <summary>
    /// Generate detailed statistics report with all correspondences and their service owner information
    /// </summary>
    /// <remarks>
    /// This generates a parquet file with detailed correspondence data including ServiceOwnerId.
    /// The file is stored locally and contains individual correspondence records grouped by service owner.
    /// </remarks>
    /// <response code="200">Returns the generated report information</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-report")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GenerateStatisticsReportResponse), StatusCodes.Status200OK)]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
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

    /// <summary>
    /// Generate a summary report with correspondence counts per service owner
    /// </summary>
    /// <remarks>
    /// This endpoint automatically generates a new detailed parquet report and then 
    /// creates an in-memory statistical summary showing counts and percentages per service owner.
    /// No request parameters are needed.
    /// </remarks>
    /// <response code="200">Returns the statistics summary</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-summary")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StatisticsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateSummary(
        [FromServices] GenerateStatisticsReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to generate statistics summary received");

        try
        {
            // First generate the detailed report
            var result = await handler.Process(HttpContext.User, cancellationToken);
            
            return result.Match(
                response => {
                    // Read the generated parquet file and create summary
                    var summary = CreateSummaryFromParquetFile(response.FilePath, response.Environment);
                    return Ok(summary);
                },
                error => Problem(error)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate statistics summary");
            return StatusCode(500, "Failed to generate statistics summary");
        }
    }

    private StatisticsSummaryResponse CreateSummaryFromParquetFile(string filePath, string environment)
    {
        _logger.LogInformation("Creating summary from parquet file: {filePath}", filePath);

        // Read the parquet file using the parquet-friendly model
        var parquetData = ParquetSerializer.DeserializeAsync<ParquetCorrespondenceData>(filePath)
            .GetAwaiter()
            .GetResult()
            .ToList();

        // Convert back to CorrespondenceReportData for processing
        var reportData = parquetData.Select(p => new CorrespondenceReportData
        {
            CorrespondenceId = p.CorrespondenceId,
            ServiceOwnerId = string.IsNullOrEmpty(p.ServiceOwnerId) ? null : p.ServiceOwnerId,
            ServiceOwnerName = string.IsNullOrEmpty(p.ServiceOwnerName) ? null : p.ServiceOwnerName,
            ResourceId = p.ResourceId,
            Sender = p.Sender,
            Recipient = p.Recipient,
            SendersReference = string.IsNullOrEmpty(p.SendersReference) ? null : p.SendersReference,
            Created = DateTimeOffset.Parse(p.Created),
            RequestedPublishTime = DateTimeOffset.Parse(p.RequestedPublishTime),
            ReportDate = DateTimeOffset.Parse(p.ReportDate),
            Environment = p.Environment,
            ServiceOwnerMigrationStatus = p.ServiceOwnerMigrationStatus
        }).ToList();

        _logger.LogInformation("Read {count} records from parquet file", reportData.Count);

        // Group by service owner and calculate statistics
        var serviceOwnerGroups = reportData
            .Where(r => !string.IsNullOrEmpty(r.ServiceOwnerId))
            .GroupBy(r => new { r.ServiceOwnerId, r.ServiceOwnerName })
            .Select(g => new ServiceOwnerSummary
            {
                ServiceOwnerId = g.Key.ServiceOwnerId!,
                ServiceOwnerName = g.Key.ServiceOwnerName,
                CorrespondenceCount = g.Count(),
                PercentageOfTotal = (decimal)g.Count() / reportData.Count * 100,
                UniqueResourceCount = g.Select(r => r.ResourceId).Distinct().Count(),
                MostRecentCorrespondence = g.Max(r => r.Created)
            })
            .OrderByDescending(s => s.CorrespondenceCount)
            .ToList();

        // Calculate date range
        DateRange? dateRange = null;
        if (reportData.Count > 0)
        {
            dateRange = new DateRange
            {
                From = reportData.Min(r => r.Created),
                To = reportData.Max(r => r.Created)
            };
        }

        var summary = new StatisticsSummaryResponse
        {
            ServiceOwnerSummaries = serviceOwnerGroups,
            TotalCorrespondences = reportData.Count,
            TotalServiceOwners = serviceOwnerGroups.Count,
            GeneratedAt = DateTimeOffset.UtcNow,
            Environment = environment,
            DateRange = dateRange
        };

        _logger.LogInformation("Created summary with {serviceOwnerCount} service owners and {totalCount} correspondences", 
            summary.TotalServiceOwners, summary.TotalCorrespondences);

        return summary;
    }

    private ActionResult Problem(Error error) => Problem(
        detail: error.Message,
        statusCode: (int)error.StatusCode,
        extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
}
