#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick test script for DialogActivityExporter with limited results

.DESCRIPTION
    Runs a quick test export with a small batch size (1000 rows by default)
    to verify functionality and output format before running full production export.

    Test mode automatically skips expensive COUNT queries - progress shows processed
    records only (no percentage/ETA).

.PARAMETER Issue
    Issue number: 1951, 1716, or 'all' (default: 1951)

.PARAMETER BatchSize
    Number of rows to fetch per batch (default: 1000, minimum: 1000)

.PARAMETER OutputPath
    Path to output CSV file (default: C:\temp\test_export_{issue}_{timestamp}.csv)

.PARAMETER CutoffDate
    Cutoff timestamp (default: 2026-05-19 11:35:59)

.PARAMETER MaxBatches
    Maximum number of batches to export (default: 2)
    Limits the export to test format and function without full dataset
    Automatically skips COUNT queries for faster startup

.PARAMETER UseAzureAd
    Use Azure AD authentication (default: true)
    Supports: Azure CLI, Visual Studio, VS Code, Managed Identity, etc.

.PARAMETER ConnectionString
    PostgreSQL connection string (optional, overrides Azure AD)
    Can also be configured in appsettings.json

.EXAMPLE
    .\test-export.ps1
    # Quick test with 2 batches (2000 rows) for Issue #1951
    # Shows: "Processed: 2,000 | 1,234 rows/sec | Elapsed: 00:00:02"

.EXAMPLE
    .\test-export.ps1 -Issue 1716 -BatchSize 5000 -MaxBatches 1
    # Test Issue #1716 with 1 batch of 5000 rows
    # Fast startup (no COUNT query)

.EXAMPLE
    .\test-export.ps1 -MaxBatches 5
    # Test with 5 batches to get more data for verification

.EXAMPLE
    .\test-export.ps1 -Issue all -OutputPath C:\temp\my_test.csv
    # Test both issues combined (fast startup, no COUNT queries)

.EXAMPLE
    .\test-export.ps1 -ConnectionString "Host=localhost;Database=correspondence_dev;Username=dev;Password=dev"
    # Test against local dev database

.NOTES
    Test mode benefits:
    - Instant startup (no expensive COUNT queries)
    - Shows processed records, rate, and elapsed time
    - Perfect for verifying query correctness and CSV format
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('1951', '1716', 'all')]
    [string]$Issue = '1951',

    [Parameter(Mandatory=$false)]
    [ValidateRange(1000, 100000)]
    [int]$BatchSize = 1000,

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "",

    [Parameter(Mandatory=$false)]
    [string]$CutoffDate = "2026-05-19 11:35:59",

    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 100)]
    [int]$MaxBatches = 2,

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
    $tempPath = [System.IO.Path]::GetTempPath()
    $OutputPath = Join-Path $tempPath "test_export_$($Issue)_$($timestamp).csv"
}

# Ensure output directory exists
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Created output directory: $outputDir" -ForegroundColor Green
}

# Build command arguments
$commandArgs = @(
    "--issue", $Issue,
    "--output", $OutputPath,
    "--cutoff", $CutoffDate,
    "--batch-size", $BatchSize,
    "--max-batches", $MaxBatches,
    "--yes"
)

# Add connection string or Azure AD flag
if (-not [string]::IsNullOrEmpty($ConnectionString)) {
    $commandArgs += "--connection"
    $commandArgs += $ConnectionString
} elseif ($UseAzureAd) {
    $commandArgs += "--azure-ad"
}

# Display test configuration
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   DialogActivityExporter - Quick Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Issue:         $Issue" -ForegroundColor White
Write-Host "Batch Size:    $BatchSize rows" -ForegroundColor White
Write-Host "Max Batches:   $MaxBatches (TEST MODE - No COUNT queries)" -ForegroundColor Yellow
Write-Host "Output:        $OutputPath" -ForegroundColor White
Write-Host "Cutoff Date:   $CutoffDate" -ForegroundColor White
Write-Host ""
Write-Host "Progress Format: Processed records only (no percentage/ETA)" -ForegroundColor DarkGray
Write-Host ""

# Run the export
try {
    Write-Host "Starting export..." -ForegroundColor Green
    Write-Host ""

    dotnet run -- $commandArgs

    $exitCode = $LASTEXITCODE

    Write-Host ""

    if ($exitCode -eq 0) {
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

            Write-Host "Output file:   $OutputPath" -ForegroundColor White
            Write-Host "File size:     $fileSize" -ForegroundColor White
            # Calculate approximate row count for test mode without loading entire file
            $estimatedRows = $MaxBatches * $BatchSize
            Write-Host "Rows (approx): ~$estimatedRows (test mode: $MaxBatches batches × $BatchSize)" -ForegroundColor White
            Write-Host ""            
        }
    } else {
        Write-Host "[ERROR] Export failed with exit code: $exitCode" -ForegroundColor Red
        exit $exitCode
    }
} catch {
    Write-Host ""
    Write-Host "[ERROR] Error running export:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
