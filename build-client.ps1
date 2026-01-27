#!/usr/bin/env pwsh
# Build Fable client code before building SenderApp

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ClientDir = Join-Path $ScriptDir "SenderApp" "Client"

Write-Host "Building Fable client..." -ForegroundColor Cyan

Push-Location $ScriptDir
try {
    Write-Host "Restoring dotnet tools..." -ForegroundColor Yellow
    dotnet tool restore
} finally {
    Pop-Location
}

Push-Location $ClientDir

try {
    # Install npm dependencies if needed
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
        npm install
    }

    # Run Fable compiler
    npm run build

    Write-Host "Fable build complete!" -ForegroundColor Green
} finally {
    Pop-Location
}
