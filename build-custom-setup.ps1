$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw 'SDK .NET não encontrado.' }
& $dotnet publish .\Ducktimizer.Setup\Ducktimizer.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\outputs\custom-setup
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o setup.' }
Write-Host "Setup criado em .\outputs\custom-setup\Ducktimizer-Setup.exe" -ForegroundColor Green
