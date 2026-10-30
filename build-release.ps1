#Requires -Version 7.0
[CmdletBinding()]
param([switch]$SkipInstaller)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj'
$publishDir = Join-Path $projectRoot 'outputs\publish'
$installerScript = Join-Path $projectRoot 'installer\Ducktimizer.iss'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 10 SDK not found. Install the .NET 10 SDK and try again.'
}

dotnet publish $projectFile -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Failed to publish the application.' }

& (Join-Path $publishDir 'Ducktimizer.exe') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Ducktimizer self-test failed.' }

if ($SkipInstaller) {
    Write-Host "Executable ready in $publishDir" -ForegroundColor Green
    return
}

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found. Install it or use -SkipInstaller.' }

& $iscc $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Failed to generate the installer.' }
Write-Host "Installer ready in $(Join-Path $projectRoot 'outputs\installer')" -ForegroundColor Green
