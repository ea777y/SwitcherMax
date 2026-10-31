# SwitcherMax build-and-package: run ONLY on Windows 10/11 with .NET 8 SDK + NSIS.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$build = Join-Path $root 'build-windows.ps1'
$script = Join-Path $root 'installer\SwitcherMax.nsi'
$exe = Join-Path $root 'dist\win-x64\SwitcherMax.exe'
$setupDir = Join-Path $root 'dist\setup'
$setup = Join-Path $setupDir 'SwitcherMax_Setup_v0.1.0_x64.exe'

if (-not (Test-Path -LiteralPath $build -PathType Leaf)) { throw "MISSING_BUILD_SCRIPT: $build" }
if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { throw "MISSING_NSIS_SCRIPT: $script" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'DOTNET_SDK_MISSING' }
if (-not ((& dotnet --list-sdks) -match '^8\.0\.')) { throw 'DOTNET_8_SDK_MISSING' }

Write-Host '=== SWITCHERMAX: TEST + SELF-CONTAINED EXE ===' -ForegroundColor Cyan
& $build
if ($LASTEXITCODE -ne 0) { throw "PUBLISH_FAILED_EXIT_CODE=$LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'PUBLISHED_EXE_MISSING' }

$paths = @(
    (Get-Command 'makensis.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'),
    (Join-Path $env:ProgramFiles 'NSIS\makensis.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\NSIS\makensis.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -Unique

if (-not $paths) {
    throw "NSIS_NOT_FOUND. Install on the build PC: winget install -e --id NSIS.NSIS --source winget"
}
$nsis = @($paths)[0]
New-Item -ItemType Directory -Path $setupDir -Force | Out-Null
if (Test-Path -LiteralPath $setup) { Remove-Item -LiteralPath $setup -Force }

Write-Host '=== SWITCHERMAX: CREATE OFFLINE SETUP ===' -ForegroundColor Cyan
Push-Location (Split-Path -Parent $script)
try {
    & $nsis /V3 (Split-Path -Leaf $script)
    if ($LASTEXITCODE -ne 0) { throw "NSIS_FAILED_EXIT_CODE=$LASTEXITCODE" }
}
finally { Pop-Location }

if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'INSTALLER_NOT_CREATED' }
Write-Host 'SETUP_BUILD: PASS' -ForegroundColor Green
Write-Host "SETUP: $setup"
Get-Item -LiteralPath $setup | Select-Object FullName, Length | Format-List
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | Format-List
