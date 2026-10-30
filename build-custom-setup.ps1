$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw '.NET SDK not found.' }
& $dotnet publish .\Ducktimizer.Setup\Ducktimizer.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\outputs\custom-setup
if ($LASTEXITCODE -ne 0) { throw 'Failed to create the setup.' }
Write-Host "Setup created in .\outputs\custom-setup\Ducktimizer-Setup.exe" -ForegroundColor Green
