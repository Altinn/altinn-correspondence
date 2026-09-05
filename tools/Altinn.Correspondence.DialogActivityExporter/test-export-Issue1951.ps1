#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick test script for Issue #1951 with limited results

.DESCRIPTION
    Runs a quick test export with a small batch size (default: 2500 rows)
    to verify functionality and output format before running full production export.

    Test mode automatically limits to 2 batches - progress shows processed
    records only (no percentage/ETA).

    Note:
    Data is pre-filtered in helper table A2Iss1951A2Events during creation.
    The export includes all data from this table.

.PARAMETER BatchSize
    Number of rows to fetch per batch (default: 2500, minimum: 1000)

.PARAMETER OutputPath
    Path to output CSV file (default: C:\temp\test_export_1951_{timestamp}.csv)

.PARAMETER MaxBatches
    Maximum number of batches to export (default: 2)
    Limits the export to test format and function without full dataset

.PARAMETER UseAzureAd
    Use Azure AD authentication (default: true)
    Supports: Azure CLI, Visual Studio, VS Code, Managed Identity, etc.

.PARAMETER ConnectionString
    PostgreSQL connection string (optional, overrides Azure AD)
    Can also be configured in appsettings.json

.EXAMPLE
    .\test-export-Issue1951.ps1
    # Quick test with 2 batches (5000 rows) for Issue #1951
    # Shows: "Processed: 5,000 | 1,234 rows/sec | Elapsed: 00:00:04"

.EXAMPLE
    .\test-export-Issue1951.ps1 -BatchSize 5000 -MaxBatches 1
    # Test with 1 batch of 5000 rows

.EXAMPLE
    .\test-export-Issue1951.ps1 -MaxBatches 5
    # Test with 5 batches to get more data for verification

.EXAMPLE
    .\test-export-Issue1951.ps1 -OutputPath C:\temp\my_test.csv
    # Test with custom output path

.EXAMPLE
    .\test-export-Issue1951.ps1 -ConnectionString "Host=localhost;Database=correspondence_dev;Username=dev;Password=dev"
    # Test against local dev database

.NOTES
    Test mode benefits:
    - Fast startup (limited batches)
    - Shows processed records, rate, and elapsed time
    - Perfect for verifying query correctness and CSV format
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateRange(1000, 100000)]
    [int]$BatchSize = 2500,

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "",

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
    $OutputPath = "C:\temp\test_export_1951_$($timestamp).csv"
}

# Ensure output directory exists
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Created output directory: $outputDir" -ForegroundColor Green
}

# Build command arguments
$commandArgs = @(
    "--issue", "1951",
    "--output", $OutputPath,
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
Write-Host "   DialogActivityExporter - Quick Test (Issue #1951)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Issue:         1951 (Migrated Events)" -ForegroundColor White
Write-Host "Batch Size:    $BatchSize rows" -ForegroundColor White
Write-Host "Max Batches:   $MaxBatches (TEST MODE)" -ForegroundColor Yellow
Write-Host "Output:        $OutputPath" -ForegroundColor White
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
            Write-Host "Rows (approx): ~$estimatedRows (test mode: $MaxBatches batches x $BatchSize)" -ForegroundColor White
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
