#Requires -Version 7.0
[CmdletBinding()]
param([switch]$SkipInstaller)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj'
$publishDir = Join-Path $projectRoot 'outputs\publish'
$installerScript = Join-Path $projectRoot 'installer\Ducktimizer.iss'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'SDK do .NET 10 não encontrado. Instale o .NET 10 SDK e execute novamente.'
}

dotnet publish $projectFile -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o aplicativo.' }

& (Join-Path $publishDir 'Ducktimizer.exe') --self-test
if ($LASTEXITCODE -ne 0) { throw 'O autoteste do Ducktimizer falhou.' }

if ($SkipInstaller) {
    Write-Host "Executável pronto em $publishDir" -ForegroundColor Green
    return
}

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale-o ou execute com -SkipInstaller.' }

& $iscc $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }
Write-Host "Instalador pronto em $(Join-Path $projectRoot 'outputs\installer')" -ForegroundColor Green
