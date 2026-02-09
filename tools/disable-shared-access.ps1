#Requires -Version 5.1
#Requires -Modules Az.Storage

<#
.SYNOPSIS
    Disables SharedKeyAccess for all storage accounts in an Azure subscription.

.DESCRIPTION
    This script iterates through all storage accounts in the current Azure subscription
    and disables SharedKeyAccess (shared key authorization), forcing the use of Azure AD authentication.

.PARAMETER SubscriptionId
    Optional. The Azure subscription ID. If not provided, uses the current subscription context.

.PARAMETER WhatIf
    Optional. Shows what would happen without making any changes.

.EXAMPLE
    .\Disable-StorageAccountSharedKeyAccess.ps1
    
.EXAMPLE
    .\Disable-StorageAccountSharedKeyAccess.ps1 -SubscriptionId "your-subscription-id"
    
.EXAMPLE
    .\Disable-StorageAccountSharedKeyAccess.ps1 -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId
)

$ErrorActionPreference = 'Stop'

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Warning', 'Error', 'Success')]
        [string]$Level = 'Info'
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Success' { 'Green' }
    }
    
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

try {
    # Check if logged in to Azure
    Write-Log "Checking Azure connection..." -Level Info
    $context = Get-AzContext
    
    if (-not $context) {
        Write-Log "Not connected to Azure. Please run Connect-AzAccount first." -Level Error
        exit 1
    }
    
    # Set subscription if provided
    if ($SubscriptionId) {
        Write-Log "Setting subscription context to: $SubscriptionId" -Level Info
        Set-AzContext -SubscriptionId $SubscriptionId | Out-Null
        $context = Get-AzContext
    }
    
    Write-Log "Working with subscription: $($context.Subscription.Name) ($($context.Subscription.Id))" -Level Info
    Write-Log "Account: $($context.Account.Id)" -Level Info
    Write-Log "" -Level Info
    
    # Get all storage accounts
    Write-Log "Retrieving all storage accounts in the subscription..." -Level Info
    $storageAccounts = Get-AzStorageAccount
    
    if (-not $storageAccounts) {
        Write-Log "No storage accounts found in the subscription." -Level Warning
        exit 0
    }
    
    Write-Log "Found $($storageAccounts.Count) storage account(s)" -Level Info
    Write-Log "" -Level Info
    
    $successCount = 0
    $skipCount = 0
    $errorCount = 0
    
    foreach ($storageAccount in $storageAccounts) {
        $accountName = $storageAccount.StorageAccountName
        $resourceGroup = $storageAccount.ResourceGroupName
        
        try {
            # Check current state
            $allowSharedKeyAccess = $storageAccount.AllowSharedKeyAccess
            
            if ($allowSharedKeyAccess -eq $false) {
                Write-Log "[$accountName] SharedKeyAccess already disabled - skipping" -Level Info
                $skipCount++
                continue
            }
            
            if ($PSCmdlet.ShouldProcess($accountName, "Disable SharedKeyAccess")) {
                Write-Log "[$accountName] Disabling SharedKeyAccess..." -Level Info
                
                Set-AzStorageAccount `
                    -ResourceGroupName $resourceGroup `
                    -Name $accountName `
                    -AllowSharedKeyAccess $false | Out-Null
                
                Write-Log "[$accountName] SharedKeyAccess disabled successfully" -Level Success
                $successCount++
            }
            else {
                Write-Log "[$accountName] WHATIF: Would disable SharedKeyAccess" -Level Warning
                $successCount++
            }
        }
        catch {
            Write-Log "[$accountName] Failed to disable SharedKeyAccess: $($_.Exception.Message)" -Level Error
            $errorCount++
        }
    }
    
    # Summary
    Write-Log "" -Level Info
    Write-Log "===== SUMMARY =====" -Level Info
    Write-Log "Total storage accounts: $($storageAccounts.Count)" -Level Info
    Write-Log "Successfully updated: $successCount" -Level Success
    Write-Log "Already disabled: $skipCount" -Level Info
    if ($errorCount -gt 0) {
        Write-Log "Errors: $errorCount" -Level Error
    }
    else {
        Write-Log "Errors: $errorCount" -Level Info
    }
    
    if ($WhatIfPreference) {
        Write-Log "" -Level Info
        Write-Log "This was a WhatIf run. No changes were made." -Level Warning
        Write-Log "Run without -WhatIf to apply changes." -Level Warning
    }
}
catch {
    Write-Log "Script failed with error: $($_.Exception.Message)" -Level Error
    Write-Log $_.ScriptStackTrace -Level Error
    exit 1
}