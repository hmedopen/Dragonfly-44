<#
.SYNOPSIS
    Dragonfly 44 - Build and Run Workflow Script
.DESCRIPTION
    Builds the solution (or runs tests) and launches Dragonfly 44 on Windows.
.PARAMETER Configuration
    Build configuration: 'Debug' (default) or 'Release'.
.PARAMETER Test
    If specified, runs the test suite before launching.
.PARAMETER NoLaunch
    If specified, builds the application without launching it.
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Test,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Locate dotnet CLI with valid SDK
$dotnetCmd = $null
$candidatePaths = @(
    "$env:USERPROFILE\.dotnet\dotnet.exe",
    "C:\Program Files\dotnet\dotnet.exe"
)
foreach ($cand in $candidatePaths) {
    if (Test-Path $cand) {
        $dotnetCmd = $cand
        break
    }
}
if (-not $dotnetCmd) {
    $cmdObj = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmdObj) {
        $dotnetCmd = $cmdObj.Source
    }
}

if (-not $dotnetCmd) {
    Write-Error ".NET SDK executable could not be found. Please ensure .NET 8 SDK is installed."
    exit 1
}

Write-Host "Using .NET CLI: $dotnetCmd" -ForegroundColor Cyan

# 1. Optionally run tests
if ($Test) {
    Write-Host "Running automated tests..." -ForegroundColor Yellow
    & $dotnetCmd test "$ScriptDir\Dragonfly44.sln" -c $Configuration --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Automated tests failed! Aborting run."
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed successfully!" -ForegroundColor Green
}

# 2. Build or Publish Presentation project
$exePath = $null

if ($Configuration -eq "Release") {
    $appDir = "$ScriptDir\App"
    Write-Host "Publishing self-contained Release deployment to $appDir..." -ForegroundColor Yellow
    & $dotnetCmd publish "$ScriptDir\src\Dragonfly44.Presentation\Dragonfly44.Presentation.csproj" -c Release -r win-x64 --self-contained true -o $appDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Release publish failed! Please check errors."
        exit $LASTEXITCODE
    }
    $exePath = "$appDir\Dragonfly44.Presentation.exe"
} else {
    Write-Host "Building Dragonfly 44 Presentation ($Configuration)..." -ForegroundColor Yellow
    & $dotnetCmd build "$ScriptDir\src\Dragonfly44.Presentation\Dragonfly44.Presentation.csproj" -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed! Please fix build errors."
        exit $LASTEXITCODE
    }
    $exePath = "$ScriptDir\src\Dragonfly44.Presentation\bin\x64\$Configuration\net8.0-windows10.0.19041.0\Dragonfly44.Presentation.exe"
}

Write-Host "Build succeeded!" -ForegroundColor Green

# 3. Locate executable
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at expected location: $exePath"
    exit 1
}

if ($NoLaunch) {
    Write-Host "Build complete. Executable ready at: $exePath" -ForegroundColor Cyan
    exit 0
}

Write-Host "Launching Dragonfly 44: $exePath" -ForegroundColor Green
Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath)
