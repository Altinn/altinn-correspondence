using System.Diagnostics;
using Altinn.Correspondence.DialogActivityExporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

// Parse arguments
var options = ParseArguments(args, config, logger);
if (options == null)
{
    return 1;
}

// If connection string is not provided, try to build it automatically using Azure CLI
if (string.IsNullOrEmpty(options.ConnectionString))
{
    var autoConnection = await TryBuildAzureConnectionAsync(logger);
    if (autoConnection != null)
    {
        options.ConnectionString = autoConnection;
        logger.LogInformation("Using automatic Azure AD authentication");
    }
    else
    {
        logger.LogError("No connection string provided and automatic Azure authentication failed");
        return 1;
    }
}

// Display configuration
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Altinn Correspondence - Dialog Activity Exporter    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine($"Issue:        {(options.ExportBoth ? "ALL (1716 + 1951)" : options.IssueNumber.ToString())}");
Console.WriteLine($"Output:       {options.OutputPath}");
Console.WriteLine($"Cutoff Date:  {options.CutoffTimestamp:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Batch Size:   {options.BatchSize:N0} rows");
Console.WriteLine($"Connection:   {MaskConnectionString(options.ConnectionString)}");
Console.WriteLine();

// Confirm
if (!options.SkipConfirmation)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Continue? (y/n): ");
    Console.ResetColor();
    var key = Console.ReadKey();
    Console.WriteLine();
    if (key.Key != ConsoleKey.Y)
    {
        logger.LogInformation("Export cancelled by user");
        return 0;
    }
}

// Create service and run export
var exportService = new DialogActivityExportService(
    options.ConnectionString,
    options.BatchSize,
    loggerFactory.CreateLogger<DialogActivityExportService>());

var progress = new Progress<ExportProgress>(p =>
{
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', Console.WindowWidth)); // Clear line
    Console.SetCursorPosition(0, Console.CursorTop);

    var percent = p.PercentComplete;
    var bar = CreateProgressBar(percent, 40);
    var rate = p.TotalProcessed / p.ElapsedTime.TotalSeconds;
    var eta = p.EstimatedTimeRemaining;

    Console.ForegroundColor = percent < 50 ? ConsoleColor.Yellow : ConsoleColor.Green;
    Console.Write($"[{bar}] {percent:F2}% ");
    Console.ResetColor();
    Console.Write($"| {p.TotalProcessed:N0}/{p.TotalCount:N0} | {rate:F0} rows/sec | ETA: {eta:hh\\:mm\\:ss}");
});

var stopwatch = Stopwatch.StartNew();

try
{
    if (options.ExportBoth)
    {
        // Export both issues to single CSV
        await exportService.ExportBothToCSVAsync(
            options.OutputPath,
            options.CutoffTimestamp,
            options.OldestCorrespondenceDate,
            progress,
            CancellationToken.None);
    }
    else
    {
        // Export single issue
        await exportService.ExportToCSVAsync(
            options.OutputPath,
            options.IssueNumber,
            options.CutoffTimestamp,
            options.OldestCorrespondenceDate,
            progress,
            CancellationToken.None);
    }

    stopwatch.Stop();

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine();
    Console.WriteLine("✓ Export completed successfully!");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"Duration:     {stopwatch.Elapsed:hh\\:mm\\:ss}");
    Console.WriteLine($"Output file:  {options.OutputPath}");
    Console.WriteLine($"File size:    {GetFileSize(options.OutputPath)}");
    Console.WriteLine();

    return 0;
}
catch (Exception ex)
{
    stopwatch.Stop();

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ Export failed!");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"Error: {ex.Message}");
    logger.LogError(ex, "Export failed");

    return 1;
}

// Helper methods
static ExportOptions? ParseArguments(string[] args, IConfiguration config, ILogger logger)
{
    if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
    {
        ShowHelp();
        return null;
    }

    var issue = GetArgument(args, "--issue", config["Issue"]);
    var output = GetArgument(args, "--output", config["OutputPath"]);
    var connectionString = GetArgument(args, "--connection", config["ConnectionString"]);
    var cutoff = GetArgument(args, "--cutoff", config["CutoffTimestamp"]);
    var oldest = GetArgument(args, "--oldest", config["OldestDate"]);
    var batchSizeStr = GetArgument(args, "--batch-size", config["BatchSize"] ?? "50000");
    var skipConfirm = args.Contains("--yes") || args.Contains("-y");
    var useAzureAd = args.Contains("--azure-ad") || args.Contains("--azure");

    // If --azure-ad flag is set, connectionString will be built automatically later
    if (!useAzureAd && string.IsNullOrEmpty(connectionString))
    {
        logger.LogError("Missing connection string. Use --connection or --azure-ad flag.");
        return null;
    }

    if (string.IsNullOrEmpty(issue) || string.IsNullOrEmpty(output) || string.IsNullOrEmpty(cutoff))
    {
        logger.LogError("Missing required arguments. Use --help for usage information.");
        return null;
    }

    var issueNumber = 0;
    var exportBoth = false;

    if (issue?.ToLower() == "all")
    {
        exportBoth = true;
    }
    else if (!int.TryParse(issue, out issueNumber) || (issueNumber != 1951 && issueNumber != 1716))
    {
        logger.LogError("Invalid issue number. Must be 1951, 1716, or 'all'.");
        return null;
    }

    if (!DateTime.TryParse(cutoff, out var cutoffDate))
    {
        logger.LogError("Invalid cutoff date format. Use 'yyyy-MM-dd HH:mm:ss'");
        return null;
    }

    DateTime? oldestDate = null;
    if (!string.IsNullOrEmpty(oldest))
    {
        if (!DateTime.TryParse(oldest, out var parsedOldest))
        {
            logger.LogError("Invalid oldest date format. Use 'yyyy-MM-dd HH:mm:ss'");
            return null;
        }
        oldestDate = parsedOldest;
    }

    if (!int.TryParse(batchSizeStr, out var batchSize) || batchSize < 1000)
    {
        logger.LogError("Invalid batch size. Must be >= 1000");
        return null;
    }

    return new ExportOptions
    {
        IssueNumber = issueNumber,
        ExportBoth = exportBoth,
        OutputPath = output,
        ConnectionString = connectionString ?? "", // Will be populated later if using Azure AD
        CutoffTimestamp = cutoffDate,
        OldestCorrespondenceDate = oldestDate,
        BatchSize = batchSize,
        SkipConfirmation = skipConfirm,
        UseAzureAd = useAzureAd
    };
}

static string? GetArgument(string[] args, string flag, string? defaultValue)
{
    var index = Array.IndexOf(args, flag);
    if (index >= 0 && index < args.Length - 1)
        return args[index + 1];
    return defaultValue;
}

static void ShowHelp()
{
    Console.WriteLine("Altinn Correspondence - Dialog Activity Exporter");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  DialogActivityExporter --issue <1951|1716|all> --output <path> --connection <connstr> --cutoff <date> [options]");
    Console.WriteLine();
    Console.WriteLine("Required Arguments:");
    Console.WriteLine("  --issue        Issue number (1951, 1716, or 'all' for both)");
    Console.WriteLine("  --output       Output CSV file path");
    Console.WriteLine("  --cutoff       Cutoff timestamp (yyyy-MM-dd HH:mm:ss)");
    Console.WriteLine();
    Console.WriteLine("Connection (choose one):");
    Console.WriteLine("  --connection   PostgreSQL connection string");
    Console.WriteLine("  --azure-ad     Use automatic Azure AD authentication (requires Azure CLI)");
    Console.WriteLine();
    Console.WriteLine("Optional Arguments:");
    Console.WriteLine("  --oldest       Oldest correspondence date (yyyy-MM-dd HH:mm:ss) - Issue 1951 only");
    Console.WriteLine("  --batch-size   Batch size (default: 50000)");
    Console.WriteLine("  -y, --yes      Skip confirmation prompt");
    Console.WriteLine("  -h, --help     Show this help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  # Export Issue #1951");
    Console.WriteLine("  DialogActivityExporter --issue 1951 \\");
    Console.WriteLine("    --output C:\\temp\\issue1951.csv \\");
    Console.WriteLine("    --connection \"Host=localhost;Database=correspondence;Username=user;Password=pass\" \\");
    Console.WriteLine("    --cutoff \"2026-05-19 11:35:59\" \\");
    Console.WriteLine("    --oldest \"2019-03-23\"");
    Console.WriteLine();
    Console.WriteLine("  # Export Issue #1716");
    Console.WriteLine("  DialogActivityExporter --issue 1716 \\");
    Console.WriteLine("    --output C:\\temp\\issue1716.csv \\");
    Console.WriteLine("    --connection \"Host=localhost;Database=correspondence;Username=user;Password=pass\" \\");
    Console.WriteLine("    --cutoff \"2026-02-15 00:00:00\"");
    Console.WriteLine();
    Console.WriteLine("  # Export BOTH issues to single CSV");
    Console.WriteLine("  DialogActivityExporter --issue all \\");
    Console.WriteLine("    --output C:\\temp\\all_issues.csv \\");
    Console.WriteLine("    --connection \"Host=localhost;Database=correspondence;Username=user;Password=pass\" \\");
    Console.WriteLine("    --cutoff \"2026-05-19 11:35:59\" \\");
    Console.WriteLine("    --oldest \"2019-03-23\"");
    Console.WriteLine();
    Console.WriteLine("  # Use Azure AD authentication (automatic)");
    Console.WriteLine("  DialogActivityExporter --issue all \\");
    Console.WriteLine("    --output C:\\temp\\all_issues.csv \\");
    Console.WriteLine("    --azure-ad \\");
    Console.WriteLine("    --cutoff \"2026-05-19 11:35:59\" \\");
    Console.WriteLine("    --oldest \"2019-03-23\"");
}

static string CreateProgressBar(double percent, int width)
{
    var filled = (int)(percent / 100.0 * width);
    return new string('█', filled) + new string('░', width - filled);
}

static string MaskConnectionString(string connStr)
{
    var parts = connStr.Split(';');
    var masked = parts.Select(p =>
    {
        if (p.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
            return "Password=***";
        return p;
    });
    return string.Join(';', masked);
}

static async Task<string?> TryBuildAzureConnectionAsync(ILogger logger)
{
    try
    {
        logger.LogInformation("Attempting to get Azure AD access token using Azure CLI...");

        // Get access token using Azure CLI
        var tokenProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "az",
                Arguments = "account get-access-token --resource-type oss-rdbms --query accessToken -o tsv",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        tokenProcess.Start();
        var token = await tokenProcess.StandardOutput.ReadToEndAsync();
        var error = await tokenProcess.StandardError.ReadToEndAsync();
        await tokenProcess.WaitForExitAsync();

        if (tokenProcess.ExitCode != 0 || string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Failed to get Azure access token: {Error}", error);
            return null;
        }

        token = token.Trim();

        // Get current Windows username
        var username = Environment.UserName;
        logger.LogInformation("Current Windows user: {Username}", username);

        // Build connection string with Azure AD token
        var connectionString = $"Host=altinn-corr-prod-dbserver.postgres.database.azure.com;" +
                              $"Port=5432;" +
                              $"Database=correspondence;" +
                              $"Username={username}@ai-dev.no;" +
                              $"Password={token};" +
                              $"SSL Mode=Require;";

        logger.LogInformation("Successfully built Azure AD connection string");
        return connectionString;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to automatically build Azure connection");
        return null;
    }
}

static string GetFileSize(string path)
{
    if (!File.Exists(path)) return "0 bytes";
    var bytes = new FileInfo(path).Length;
    string[] sizes = { "bytes", "KB", "MB", "GB" };
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
        order++;
        len /= 1024;
    }
    return $"{len:F2} {sizes[order]}";
}

class ExportOptions
{
    public int IssueNumber { get; set; }
    public bool ExportBoth { get; set; }
    public string OutputPath { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public DateTime CutoffTimestamp { get; set; }
    public DateTime? OldestCorrespondenceDate { get; set; }
    public int BatchSize { get; set; }
    public bool SkipConfirmation { get; set; }
    public bool UseAzureAd { get; set; }
}
