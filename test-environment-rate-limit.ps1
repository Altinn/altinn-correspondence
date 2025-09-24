# Test environment-based rate limiting
$baseUrl = "https://localhost:7241"
$apiKey = "dev-api-key-12345"

Write-Host "Testing Environment-Based Rate Limiting" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Development environment should have 5 attempts per hour" -ForegroundColor Yellow
Write-Host "Production environment should have 10 attempts per hour" -ForegroundColor Yellow

# Test 1: Make multiple requests to test rate limiting
Write-Host "`nTest 1: Making multiple requests to test rate limiting" -ForegroundColor Yellow
$headers = @{ "X-API-Key" = $apiKey }

for ($i = 1; $i -le 7; $i++) {
    try {
        Write-Host "Request $i..." -NoNewline
        $response = Invoke-RestMethod -Uri "$baseUrl/correspondence/api/v1/statistics/generate-daily-summary" -Method Post -ContentType "application/json" -Body '{"Altinn2Included": true}' -Headers $headers -ErrorAction Stop
        Write-Host " ✅ Success" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 429) {
            Write-Host " ❌ Rate Limited (429 Too Many Requests)" -ForegroundColor Red
            Write-Host "   This is expected after exceeding the rate limit!" -ForegroundColor Yellow
            break
        } elseif ($_.Exception.Response.StatusCode -eq 401) {
            Write-Host " ❌ Unauthorized (401)" -ForegroundColor Red
        } else {
            Write-Host " ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`n=====================================" -ForegroundColor Green
Write-Host "Environment-Based Rate Limiting Test Complete" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Note: In development, you should see rate limiting after 5 requests" -ForegroundColor Cyan
Write-Host "In production, rate limiting would occur after 10 requests" -ForegroundColor Cyan
