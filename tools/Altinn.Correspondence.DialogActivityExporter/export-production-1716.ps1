#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production export script for Dialog Activity Issue #1716

.DESCRIPTION
    Runs full production export of Dialog Activity records for Issue #1716
    (Synced Events from Altinn2) using optimized A2Iss1716A2Events helper table.

    This script:
    - Exports ~9.97M rows (Status 4 + Status 6 events)
    - Uses helper table optimization (50-60 minutes estimated)
    - Includes progress tracking with percentage and ETA
    - Supports resume from checkpoint on interruption
    - Uses Azure AD authentication by default

.PARAMETER OutputPath
    Path to output CSV file
    Default: C:\temp\dialog_activity_export_1716_{timestamp}.csv

.PARAMETER CutoffDate
    Export records with StatusChanged/SyncedFromAltinn2 before this date
    Default: Current date/time
    Format: yyyy-MM-dd HH:mm:ss (e.g., "2026-02-15 14:30:00")

.PARAMETER BatchSize
    Number of rows per batch (default: 5000)
    Optimal range: 5000 rows (proven stable with Azure PostgreSQL network throttling)
    Recommended: Keep at 5000 unless specific testing indicates otherwise

.PARAMETER UseAzureAd
    Use Azure AD authentication (default: true)
    Supports: Azure CLI, Visual Studio, VS Code, Managed Identity
    Falls back to other auth methods automatically

.PARAMETER ConnectionString
    PostgreSQL connection string (optional, overrides Azure AD)
    Can also be configured in appsettings.json

.PARAMETER FreshStart
    Start fresh export, ignoring any checkpoint file (default: false)
    Use if you want to restart from beginning

.EXAMPLE
    .\export-production-1716.ps1
    # Run full export with defaults:
    # - Output: C:\temp\dialog_activity_export_1716_{timestamp}.csv
    # - Cutoff: Current date/time
    # - Batch size: 10,000 rows
    # - Azure AD authentication

.EXAMPLE
    .\export-production-1716.ps1 -OutputPath "D:\exports\issue_1716.csv"
    # Export to specific location

.EXAMPLE
    .\export-production-1716.ps1 -CutoffDate "2026-02-15 14:30:00"
    # Export records before specific date/time

.EXAMPLE
    .\export-production-1716.ps1 -BatchSize 20000
    # Use larger batch size for faster export (if network is fast)

.EXAMPLE
    .\export-production-1716.ps1 -FreshStart
    # Start fresh, ignoring checkpoint file

.NOTES
    Prerequisites:
    1. Indexes created on A2Iss1716A2Events helper table
       - Run: Optimize_A2Iss1716A2Events_Indexes.sql
       - Verify: Both indexes exist and valid

    2. Table statistics updated
       - Run: ANALYZE correspondence."A2Iss1716A2Events";

    3. Azure AD authentication configured (if using -UseAzureAd)
       - Run: az login
       - Verify: az account show

    4. Sufficient disk space
       - Required: ~2.5 GB minimum (output file ~2 GB)
       - Recommended: 5 GB free for safety margin

    Expected Performance:
    - Total rows: ~9.97M (Status 4 + Status 6)
    - Batch size 5,000: ~1,994 batches
    - Time per batch: 250-300ms
    - Total time: 9-12 minutes
    - Throughput: 15,000 rows/sec
    - Output size: ~2 GB

    Progress Display:
    - Shows: "Processed: 5,234,567 / 9,970,000 (52.5%) | 15,000 rows/sec | ETA: 00:05:23"
    - Updates every batch
    - Includes checkpoint save after each batch

    Resume Support:
    - Checkpoint file: {output_path}.checkpoint.json
    - Automatically resumes if interrupted
    - Use -FreshStart to ignore checkpoint

    Monitoring:
    - Check logs for timing: "Batch timing: Fetch=...ms, Merge=...ms, Write=...ms"
    - Watch for consistent timing (250-300ms per batch)
    - If batches slow down significantly, check database load

    Troubleshooting:
    - If slow (>1s per batch): Check Status 6 index selection
    - If connection fails: Verify Azure AD login (az account show)
    - If out of disk: Need ~400MB for output CSV
    - If interrupted: Just restart - will resume from checkpoint

    Related Documentation:
    - Migration Guide: A2Iss1716A2Events_Helper_Table_Migration.md
    - Performance Analysis: A2Iss1716A2Events_Production_Verification.md
    - Index Setup: Optimize_A2Iss1716A2Events_Indexes.sql
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "",

    [Parameter(Mandatory=$false)]
    [string]$CutoffDate = "2026-02-15",

    [Parameter(Mandatory=$false)]
    [ValidateRange(1000, 100000)]
    [int]$BatchSize = 5000,

    [Parameter(Mandatory=$false)]
    [bool]$UseAzureAd = $true,

    [Parameter(Mandatory=$false)]
    [string]$ConnectionString = "",

    [Parameter(Mandatory=$false)]
    [switch]$FreshStart
)

# Set working directory to script location
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Generate default output path if not provided
if ([string]::IsNullOrEmpty($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutputPath = "C:\temp\dialog_activity_export_1716_$($timestamp).csv"
}

# Use current date/time as cutoff if not provided
if ([string]::IsNullOrEmpty($CutoffDate)) {
    $CutoffDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
}

# Ensure output directory exists
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    Write-Host "Creating output directory: $outputDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Check if checkpoint exists (resume scenario)
$checkpointPath = "$OutputPath.checkpoint.json"
$isResume = $false
if ((Test-Path $checkpointPath) -and -not $FreshStart) {
    $checkpoint = Get-Content $checkpointPath | ConvertFrom-Json
    $isResume = $true
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host "   CHECKPOINT FOUND - RESUME MODE" -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Previous export interrupted at:" -ForegroundColor White
    Write-Host "  Processed:    $($checkpoint.TotalProcessed) rows" -ForegroundColor Cyan
    Write-Host "  Last Cursor:  $($checkpoint.LastCursorId)" -ForegroundColor DarkGray
    Write-Host "  Timestamp:    $($checkpoint.Timestamp)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Export will resume from checkpoint." -ForegroundColor Green
    Write-Host "Use -FreshStart to ignore checkpoint and start over." -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Press Enter to resume, or Ctrl+C to cancel..." -ForegroundColor Yellow
    Read-Host
}

# Build command arguments
$commandArgs = @(
    "--issue", "1716",
    "--output", $OutputPath,
    "--cutoff", $CutoffDate,
    "--batch-size", $BatchSize,
    "--yes"
)

# Add fresh start flag if requested
if ($FreshStart) {
    $commandArgs += "--fresh"
}

# Add connection string or Azure AD flag
if (-not [string]::IsNullOrEmpty($ConnectionString)) {
    $commandArgs += "--connection"
    $commandArgs += $ConnectionString
} elseif ($UseAzureAd) {
    $commandArgs += "--azure-ad"
}

# Display production configuration
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   Dialog Activity Export - Issue #1716 (PRODUCTION)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Issue:         1716 (Synced from Altinn2)" -ForegroundColor White
Write-Host "Output:        $OutputPath" -ForegroundColor White
Write-Host "Cutoff Date:   $CutoffDate" -ForegroundColor White
Write-Host "Batch Size:    $($BatchSize.ToString('N0')) rows" -ForegroundColor White
Write-Host "Mode:          $(if ($isResume) { 'RESUME from checkpoint' } else { 'FULL EXPORT (fresh start)' })" -ForegroundColor $(if ($isResume) { 'Yellow' } else { 'Green' })
Write-Host ""
Write-Host "Estimated:     ~9.97M rows, 9-12 minutes, ~2 GB output" -ForegroundColor DarkGray
Write-Host "Checkpoint:    Saved after each batch (resume support)" -ForegroundColor DarkGray
Write-Host ""

# Pre-flight checks
Write-Host "Pre-flight checks:" -ForegroundColor Cyan

# Check Azure AD authentication if enabled
if ($UseAzureAd -and [string]::IsNullOrEmpty($ConnectionString)) {
    Write-Host "  [1/3] Azure AD authentication..." -ForegroundColor White -NoNewline
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        if ($account) {
            Write-Host " OK" -ForegroundColor Green
            Write-Host "        Logged in as: $($account.user.name)" -ForegroundColor DarkGray
        } else {
            Write-Host " ERROR" -ForegroundColor Red
            Write-Host ""
            Write-Host "ERROR: Not logged into Azure CLI. Please run:" -ForegroundColor Red
            Write-Host "  az login" -ForegroundColor Yellow
            exit 1
        }
    } catch {
        Write-Host " ?" -ForegroundColor Yellow
        Write-Host "        Warning: Could not verify Azure CLI login" -ForegroundColor Yellow
        Write-Host "        Export will attempt Azure AD authentication anyway" -ForegroundColor DarkGray
    }
} else {
    Write-Host "  [1/3] Connection string provided... OK" -ForegroundColor Green
}

# Check output directory writable
Write-Host "  [2/3] Output directory writable..." -ForegroundColor White -NoNewline
try {
    $testFile = Join-Path $outputDir "write_test_$(Get-Random).tmp"
    "test" | Out-File $testFile -ErrorAction Stop
    Remove-Item $testFile -ErrorAction SilentlyContinue
    Write-Host " OK" -ForegroundColor Green
} catch {
    Write-Host " ERROR" -ForegroundColor Red
    Write-Host ""
    Write-Host "ERROR: Cannot write to output directory: $outputDir" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Check disk space (need ~2.5 GB for full export)
Write-Host "  [3/3] Disk space..." -ForegroundColor White -NoNewline
try {
    $drive = (Get-Item $outputDir).PSDrive
    $freeSpaceGB = [math]::Round($drive.Free / 1GB, 2)
    if ($freeSpaceGB -gt 3) {
        Write-Host " OK" -ForegroundColor Green
        Write-Host "        Available: $($freeSpaceGB) GB" -ForegroundColor DarkGray
    } elseif ($freeSpaceGB -gt 2) {
        Write-Host " WARNING" -ForegroundColor Yellow
        Write-Host "        Warning: Only $($freeSpaceGB) GB available" -ForegroundColor Yellow
        Write-Host "        Export needs ~2.5 GB. Tight but should work." -ForegroundColor Yellow
    } else {
        Write-Host " ERROR" -ForegroundColor Red
        Write-Host ""
        Write-Host "ERROR: Insufficient disk space: $($freeSpaceGB) GB available" -ForegroundColor Red
        Write-Host "Export needs ~2.5 GB (output file ~2 GB + safety margin)" -ForegroundColor Red
        Write-Host "Please free up disk space and try again." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host " ?" -ForegroundColor Yellow
    Write-Host "        Warning: Could not check disk space" -ForegroundColor Yellow
}


Write-Host ""
Write-Host "Ready to start export." -ForegroundColor Green
Write-Host ""

# Confirmation prompt
if (-not $isResume) {
    Write-Host "This will export ~9.97M rows (estimated 9-12 minutes, ~2 GB output)." -ForegroundColor Yellow
    Write-Host "Press Enter to continue, or Ctrl+C to cancel..." -ForegroundColor Yellow
    Read-Host
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "   EXPORT STARTING" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# Run the export
$startTime = Get-Date

try {
    dotnet run -- $commandArgs

    $exitCode = $LASTEXITCODE
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host ""
    if ($exitCode -eq 0) {
        Write-Host "============================================================" -ForegroundColor Green
        Write-Host "   EXPORT COMPLETED SUCCESSFULLY" -ForegroundColor Green
        Write-Host "============================================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Duration:      $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor White
        Write-Host "Output file:   $OutputPath" -ForegroundColor White

        # Get file size
        if (Test-Path $OutputPath) {
            $fileInfo = Get-Item $OutputPath
            $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
            Write-Host "File size:     $fileSizeMB MB" -ForegroundColor White

            # Estimate row count from file size (~200 bytes per row average)
            $estimatedRows = [math]::Round(($fileInfo.Length / 200))
            Write-Host "Rows (approx): ~$($estimatedRows.ToString('N0'))" -ForegroundColor White

            # Calculate throughput
            if ($duration.TotalSeconds -gt 0) {
                $rowsPerSec = [math]::Round($estimatedRows / $duration.TotalSeconds)
                Write-Host "Throughput:    $($rowsPerSec.ToString('N0')) rows/sec" -ForegroundColor White
            }
        }

        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Verify row count matches expected (~9.97M)" -ForegroundColor White
        Write-Host "2. Spot check data for accuracy" -ForegroundColor White
        Write-Host "3. Upload to target system (if applicable)" -ForegroundColor White
        Write-Host ""

        # Clean up checkpoint file on success
        if (Test-Path $checkpointPath) {
            Remove-Item $checkpointPath -Force
            Write-Host "Checkpoint file cleaned up." -ForegroundColor DarkGray
            Write-Host ""
        }

        exit 0
    } else {
        Write-Host "============================================================" -ForegroundColor Red
        Write-Host "   EXPORT FAILED" -ForegroundColor Red
        Write-Host "============================================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Exit code:     $exitCode" -ForegroundColor Red
        Write-Host "Duration:      $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor White
        Write-Host ""
        Write-Host "Check the logs above for error details." -ForegroundColor Yellow

        if (Test-Path $checkpointPath) {
            Write-Host ""
            Write-Host "Checkpoint file preserved - you can resume by running this script again." -ForegroundColor Yellow
        }

        Write-Host ""
        exit $exitCode
    }
} catch {
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "   EXPORT FAILED WITH EXCEPTION" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error:         $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Duration:      $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor White
    Write-Host ""
    Write-Host "Stack trace:" -ForegroundColor Yellow
    Write-Host $_.Exception.StackTrace -ForegroundColor DarkGray

    if (Test-Path $checkpointPath) {
        Write-Host ""
        Write-Host "Checkpoint file preserved - you can resume by running this script again." -ForegroundColor Yellow
    }

    Write-Host ""
    exit 1
}
