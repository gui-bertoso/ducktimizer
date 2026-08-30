$ErrorActionPreference = 'Stop'
$WorkerName = 'fluffy-octo-palm-tree'
$BucketName = 'ducktimizer-downloads'
$InstallerPath = '..\outputs\installer\Ducktimizer-Setup-3.3.0-win-x64.exe'
$InstallerKey = 'Ducktimizer-Setup-3.3.0-win-x64.exe'

Write-Host '=== Entrega automática do Ducktimizer ===' -ForegroundColor Cyan
$ResendKey = Read-Host 'Cole a API Key da Resend'
if ($ResendKey -notmatch '^re_') { throw 'A API Key da Resend parece inválida.' }
$FromEmail = Read-Host 'Remetente verificado (ex.: Ducktimizer <vendas@seudominio.com>)'
if ($FromEmail -notmatch '@') { throw 'Informe um remetente válido de um domínio verificado na Resend.' }
$ResolvedInstaller = Resolve-Path -LiteralPath $InstallerPath
$InstallerHash = (Get-FileHash -LiteralPath $ResolvedInstaller -Algorithm SHA256).Hash
$TokenSecret = ([Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'))

function Set-WorkerSecret([string]$Name, [string]$Value) {
  $Value | npx wrangler secret put $Name --name $WorkerName
  if ($LASTEXITCODE -ne 0) { throw "Falha ao salvar o segredo $Name no Cloudflare." }
}

Write-Host 'Criando o cofre privado de instaladores...'
npx wrangler r2 bucket create $BucketName 2>$null
if ($LASTEXITCODE -ne 0) { Write-Host 'O bucket já existe; continuando.' -ForegroundColor DarkGray }

Write-Host 'Enviando o instalador para o Cloudflare R2...'
npx wrangler r2 object put "$BucketName/$InstallerKey" --file $ResolvedInstaller --content-type application/octet-stream --remote
if ($LASTEXITCODE -ne 0) { throw 'Falha ao enviar o instalador.' }

Write-Host 'Salvando os segredos da entrega...'
Set-WorkerSecret 'RESEND_API_KEY' $ResendKey
Set-WorkerSecret 'DELIVERY_FROM_EMAIL' $FromEmail
Set-WorkerSecret 'DELIVERY_TOKEN_SECRET' $TokenSecret
Set-WorkerSecret 'INSTALLER_OBJECT_KEY' $InstallerKey
Set-WorkerSecret 'INSTALLER_SHA256' $InstallerHash

Write-Host ''
Write-Host 'Entrega automática configurada.' -ForegroundColor Green
Write-Host "Instalador: $InstallerKey"
Write-Host "SHA-256: $InstallerHash"
Write-Host 'Faça o deploy do site para ativar o novo fluxo.'
