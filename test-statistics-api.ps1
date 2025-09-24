# Test script for Statistics API with API key authentication
# This script tests both statistics endpoints with API key authentication

$baseUrl = "https://localhost:7241"
$apiKey = "dev-api-key-12345"
$generateEndpoint = "/correspondence/api/v1/statistics/generate-daily-summary"
$downloadEndpoint = "/correspondence/api/v1/statistics/generate-and-download-daily-summary"

Write-Host "Testing Statistics API with API Key Authentication" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host "Testing both endpoints: generate-daily-summary and generate-and-download-daily-summary" -ForegroundColor Yellow

# Test 1: Request without API key (should fail with 401)
Write-Host "`nTest 1: Request without API key (should fail with 401)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl$downloadEndpoint" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -ErrorAction Stop
    Write-Host "❌ FAILED: Request succeeded without API key" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "✅ PASSED: Correctly rejected request without API key (401 Unauthorized)" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 2: Request with wrong API key (should fail with 401)
Write-Host "`nTest 2: Request with wrong API key (should fail with 401)" -ForegroundColor Yellow
try {
    $headers = @{ "X-API-Key" = "wrong-api-key" }
    $response = Invoke-RestMethod -Uri "$baseUrl$downloadEndpoint" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -Headers $headers -ErrorAction Stop
    Write-Host "❌ FAILED: Request succeeded with wrong API key" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "✅ PASSED: Correctly rejected request with wrong API key (401 Unauthorized)" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 3: Test generate-daily-summary endpoint with correct API key
Write-Host "`nTest 3: Test generate-daily-summary endpoint with correct API key" -ForegroundColor Yellow
try {
    $headers = @{ "X-API-Key" = $apiKey }
    $response = Invoke-RestMethod -Uri "$baseUrl$generateEndpoint" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -Headers $headers -ErrorAction Stop
    Write-Host "✅ PASSED: generate-daily-summary with correct API key succeeded" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "❌ FAILED: generate-daily-summary with correct API key was rejected (401 Unauthorized)" -ForegroundColor Red
    } else {
        Write-Host "✅ PASSED: generate-daily-summary with correct API key passed authentication (got business logic error: $($_.Exception.Message))" -ForegroundColor Green
    }
}

# Test 4: Test generate-and-download-daily-summary endpoint with correct API key
Write-Host "`nTest 4: Test generate-and-download-daily-summary endpoint with correct API key" -ForegroundColor Yellow
try {
    $headers = @{ "X-API-Key" = $apiKey }
    $response = Invoke-RestMethod -Uri "$baseUrl$downloadEndpoint" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -Headers $headers -ErrorAction Stop
    Write-Host "✅ PASSED: generate-and-download-daily-summary with correct API key succeeded" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "❌ FAILED: generate-and-download-daily-summary with correct API key was rejected (401 Unauthorized)" -ForegroundColor Red
    } else {
        Write-Host "✅ PASSED: generate-and-download-daily-summary with correct API key passed authentication (got business logic error: $($_.Exception.Message))" -ForegroundColor Green
    }
}

# Test 5: Test generate-daily-summary endpoint without API key (should fail with 401)
Write-Host "`nTest 5: Test generate-daily-summary endpoint without API key (should fail with 401)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl$generateEndpoint" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -ErrorAction Stop
    Write-Host "❌ FAILED: generate-daily-summary succeeded without API key" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "✅ PASSED: generate-daily-summary correctly rejected request without API key (401 Unauthorized)" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 6: Test non-statistics endpoint (should not require API key)
Write-Host "`nTest 6: Test non-statistics endpoint (should not require API key)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/correspondence/api/v1/health" -Method Get -ErrorAction Stop
    Write-Host "✅ PASSED: Non-statistics endpoint accessible without API key" -ForegroundColor Green
} catch {
    Write-Host "❌ FAILED: Non-statistics endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=================================================" -ForegroundColor Green
Write-Host "API Key Authentication Test Complete" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
