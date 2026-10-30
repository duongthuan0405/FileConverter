param (
    [string]$Action = "build"
)

$solutionPath = "FileConverterService/FileConverterService.slnx"

switch ($Action.ToLower()) {
    "build" {
        Write-Host "Building solution: $solutionPath..." -ForegroundColor Cyan
        dotnet build $solutionPath
    }
    "clean" {
        Write-Host "Cleaning solution: $solutionPath..." -ForegroundColor Yellow
        dotnet clean $solutionPath
    }
    "check-gotenberg" {
        Write-Host "Checking Gotenberg service at http://localhost:3000/health..." -ForegroundColor Cyan
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:3000/health" -Method Get -TimeoutSec 3 -ErrorAction Stop
            Write-Host "Gotenberg is UP and running!" -ForegroundColor Green
            $response | ConvertTo-Json
        } catch {
            Write-Warning "Gotenberg is NOT accessible at http://localhost:3000. Ensure docker compose up is running in LibreOffice/Gotenberg."
        }
    }
    default {
        Write-Host "Usage: ./helper.ps1 -Action [build | clean | check-gotenberg]" -ForegroundColor Gray
    }
}
