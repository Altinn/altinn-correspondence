#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Production export script for Issue #1951 (Migrated Events)

.DESCRIPTION
	Exports Dialog Activity records for Issue #1951 (NOT Synced from Altinn2)
	- Status 4 (Read): ~190M rows
	- Status 6 (Confirmed): ~846K rows
	- Total: ~190.8M rows
	- Estimated time: 2-3 hours at ~25K rows/sec

	Features:
	- Resumable: Can be stopped/restarted (checkpoint file tracks progress)
	- Progress tracking: Shows percentage, rate, ETA
	- Optimized queries: Uses index scans for fast batch processing

	Note:
	Data is pre-filtered in helper table A2Iss1951A2Events during creation.
	The export includes all data from this table.

.PARAMETER OutputPath
	Path to output CSV file (optional)
	Default: C:\temp\dialog_activity_export_1951_{timestamp}.csv

.PARAMETER BatchSize
	Number of rows to fetch per batch (default: 5000)
	Recommended: 5000-10000 for optimal performance

.PARAMETER ThrottleDelayMs
	Delay in milliseconds between fast batches to avoid Azure throttling (default: 1000)
	Set to 0 to disable throttling mitigation
	Recommended: 1000-5000ms depending on Azure database tier

.PARAMETER FreshStart
	Force fresh start (ignore existing checkpoint)
	Use this if you want to restart from beginning

.PARAMETER UseAzureAd
	Use Azure AD authentication (default: true)
	Supports: Azure CLI, Visual Studio, VS Code, Managed Identity, etc.

.PARAMETER ConnectionString
	PostgreSQL connection string (optional, overrides Azure AD)
	Example: "Host=server.postgres.database.azure.com;Port=5432;Database=correspondence;..."

.EXAMPLE
	.\export-1951-production.ps1
	# Full export with defaults to C:\temp\dialog_activity_export_1951_{timestamp}.csv

.EXAMPLE
	.\export-1951-production.ps1 -OutputPath C:\exports\dialog_activity_1951.csv
	# Full export to specific path

.EXAMPLE
	.\export-1951-production.ps1 -BatchSize 10000
	# Larger batches for potentially faster export (if network/DB can handle it)

.EXAMPLE
	.\export-1951-production.ps1 -ThrottleDelayMs 5000
	# Use 5-second delay between batches to avoid Azure throttling

.EXAMPLE
	.\export-1951-production.ps1 -OutputPath C:\exports\1951.csv -FreshStart
	# Restart from beginning (ignore checkpoint)

.NOTES
	Resumable Export:
	- Checkpoint file: <OutputPath>.checkpoint.json
	- Automatically created/updated after each batch
	- Delete checkpoint file to force fresh start
	- Can resume after Ctrl+C or failure

	Performance:
	- Batch 1 (cold cache): ~200ms (22K rows/sec)
	- Subsequent batches: ~60-100ms (25-50K rows/sec)
	- Average: ~25K rows/sec = ~2-3 hours for 190M rows

	Monitoring:
	- Progress updates every batch with percentage and ETA
	- Final summary includes total time and average rate
#>

[CmdletBinding()]
param(
	[Parameter(Mandatory=$false)]
	[string]$OutputPath = "",

	[Parameter(Mandatory=$false)]
	[ValidateRange(1000, 100000)]
	[int]$BatchSize = 5000,

	[Parameter(Mandatory=$false)]
	[ValidateRange(0, 60000)]
	[int]$ThrottleDelayMs = 1000,

	[Parameter(Mandatory=$false)]
	[switch]$FreshStart,

	[Parameter(Mandatory=$false)]
	[bool]$UseAzureAd = $true,

	[Parameter(Mandatory=$false)]
	[string]$ConnectionString = ""
)

# Set working directory to script location
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Generate default output path if not provided
if ([string]::IsNullOrEmpty($OutputPath)) {
	$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
	$OutputPath = "C:\temp\dialog_activity_export_1951_$($timestamp).csv"
}

# Validate output path
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
	Write-Host "Creating output directory: $outputDir" -ForegroundColor Yellow
	New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Check for existing checkpoint
$checkpointPath = "$OutputPath.checkpoint.json"
$hasCheckpoint = (Test-Path $checkpointPath) -and -not $FreshStart

# Build command arguments
$commandArgs = @(
	"--issue", "1951",
	"--output", $OutputPath,
	"--batch-size", $BatchSize,
	"--throttle-delay", $ThrottleDelayMs,
	"--yes"
)

# Add fresh start flag if requested
if ($FreshStart) {
	$commandArgs += "--fresh-start"
}

# Add connection string or Azure AD flag
if (-not [string]::IsNullOrEmpty($ConnectionString)) {
	$commandArgs += "--connection"
	$commandArgs += $ConnectionString
} elseif ($UseAzureAd) {
	$commandArgs += "--azure-ad"
}

# Display configuration
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   Issue #1951 Production Export - Migrated Events" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Dataset:       ~190.8M rows (Status 4: 190M, Status 6: 846K)" -ForegroundColor White
Write-Host "Output:        $OutputPath" -ForegroundColor White
Write-Host "Batch Size:    $($BatchSize.ToString('N0')) rows" -ForegroundColor White
Write-Host "Mode:          $(if ($hasCheckpoint) { 'RESUME (checkpoint exists)' } else { 'FULL EXPORT' })" -ForegroundColor $(if ($hasCheckpoint) { 'Yellow' } else { 'White' })
Write-Host ""
Write-Host "Estimated Time: 2-3 hours (~25K rows/sec average)" -ForegroundColor DarkGray
Write-Host ""

if ($hasCheckpoint) {
	Write-Host "[WARNING] Checkpoint file found: $checkpointPath" -ForegroundColor Yellow
	Write-Host "   The export will resume from last saved position." -ForegroundColor Yellow
	Write-Host "   Use -FreshStart to ignore checkpoint and start over." -ForegroundColor DarkGray
	Write-Host ""
}

# Confirmation prompt
Write-Host "Press any key to start export or Ctrl+C to cancel..." -ForegroundColor Green
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
Write-Host ""

# Run the export
$startTime = Get-Date
try {
	Write-Host "Starting export at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')..." -ForegroundColor Green
	Write-Host ""

	dotnet run -- $commandArgs

	$exitCode = $LASTEXITCODE
	$endTime = Get-Date
	$duration = $endTime - $startTime

	Write-Host ""
	Write-Host "================================================================" -ForegroundColor Cyan

	if ($exitCode -eq 0) {
		Write-Host ""
		Write-Host "[SUCCESS] Export completed successfully!" -ForegroundColor Green
		Write-Host ""

		# Show file info
		if (Test-Path $OutputPath) {
			$fileInfo = Get-Item $OutputPath
			$fileSize = if ($fileInfo.Length -lt 1MB) { 
				"{0:N2} KB" -f ($fileInfo.Length / 1KB) 
			} else { 
				"{0:N2} MB" -f ($fileInfo.Length / 1MB) 
			}

			$lineCount = (Get-Content $OutputPath | Measure-Object -Line).Lines - 1  # Subtract header

			Write-Host "Output File:   $OutputPath" -ForegroundColor White
			Write-Host "File Size:     $fileSize" -ForegroundColor White
			Write-Host "Total Rows:    $($lineCount.ToString('N0'))" -ForegroundColor White
			Write-Host "Duration:      $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor White
			Write-Host "Avg Rate:      $([math]::Round($lineCount / $duration.TotalSeconds, 0).ToString('N0')) rows/sec" -ForegroundColor White
			Write-Host ""
			Write-Host "Checkpoint file deleted (export complete)" -ForegroundColor DarkGray
		}
	} else {
		Write-Host ""
		Write-Host "[ERROR] Export failed with exit code: $exitCode" -ForegroundColor Red
		Write-Host ""
		if (Test-Path $checkpointPath) {
			Write-Host "Checkpoint file preserved: $checkpointPath" -ForegroundColor Yellow
			Write-Host "You can resume by running this script again." -ForegroundColor Yellow
		}
		Write-Host ""
		exit $exitCode
	}
} catch {
	Write-Host ""
	Write-Host "[ERROR] Error running export:" -ForegroundColor Red
	Write-Host $_.Exception.Message -ForegroundColor Red
	Write-Host ""
	if (Test-Path $checkpointPath) {
		Write-Host "Checkpoint file preserved: $checkpointPath" -ForegroundColor Yellow
		Write-Host "You can resume by running this script again." -ForegroundColor Yellow
	}
	Write-Host ""
	exit 1
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
