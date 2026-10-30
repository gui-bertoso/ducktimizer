#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Ducktimizer.csproj'
$output = Join-Path $PSScriptRoot "outputs/portable/$Runtime"
$releases = Join-Path $PSScriptRoot 'outputs/releases'
$stage = Join-Path $releases "package-$Runtime"
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version

# These simulated checks do not apply Windows changes.
dotnet run --project $project --configuration Release -- --self-test
if ($LASTEXITCODE -ne 0) {
    throw 'Self-tests failed. No executable was published.'
}

dotnet publish $project -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) {
    throw 'Portable build failed.'
}

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $output 'Ducktimizer.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'TERMS.txt') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $stage -Recurse

$archive = Join-Path $releases "Ducktimizer-$version-$Runtime-portable.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding utf8NoBOM
Write-Host "Portable release: $archive"
Write-Host "SHA-256: $hash"
