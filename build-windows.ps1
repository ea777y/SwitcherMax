# Run on Windows 10/11 with .NET 8 SDK installed.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    Write-Host '=== SWITCHERMAX V0.1: CORE TESTS ===' -ForegroundColor Cyan
    dotnet run --project '.\tests\SwitcherMax.Tests\SwitcherMax.Tests.csproj' --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'CORE_TESTS_FAILED' }
    Write-Host '=== SWITCHERMAX V0.1: WINDOWS BUILD ===' -ForegroundColor Cyan
    dotnet publish '.\src\SwitcherMax\SwitcherMax.csproj' --configuration Release --runtime win-x64 --self-contained true --output '.\dist\win-x64' -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'PUBLISH_FAILED' }
    $exe = '.\dist\win-x64\SwitcherMax.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'EXE_MISSING' }
    Write-Host 'BUILD_STATUS: PASS' -ForegroundColor Green
    Write-Host "EXE: $(Resolve-Path $exe)"
    Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Format-List
} finally { Pop-Location }
