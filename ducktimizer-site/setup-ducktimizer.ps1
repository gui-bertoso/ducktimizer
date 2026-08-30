$ErrorActionPreference = 'Stop'

$WorkerName = 'fluffy-octo-palm-tree'
$SiteUrl = 'https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev'
$WebhookUrl = "$SiteUrl/api/webhooks/asaas"
$BucketName = 'ducktimizer-downloads'
$InstallerKey = 'Ducktimizer-Setup-3.3.0-win-x64.exe'
$DefaultInstaller = '..\outputs\installer\Ducktimizer-Setup-3.3.0-win-x64.exe'
$DefaultPrivateKey = '..\outputs\Ducktimizer-Seller-Kit\ducktimizer-private.pem'
$SecretFile = $null

function New-RandomSecret {
  $Bytes = [byte[]]::new(48)
  [Security.Cryptography.RandomNumberGenerator]::Fill($Bytes)
  try { return [Convert]::ToBase64String($Bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') }
  finally { [Array]::Clear($Bytes, 0, $Bytes.Length) }
}

function Read-Required([string]$Prompt) {
  $Value = Read-Host $Prompt
  if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Prompt é obrigatório." }
  return $Value.Trim()
}

Write-Host ''
Write-Host '╔══════════════════════════════════════╗' -ForegroundColor Cyan
Write-Host '║   CONFIGURADOR ÚNICO DUCKTIMIZER    ║' -ForegroundColor Cyan
Write-Host '╚══════════════════════════════════════╝' -ForegroundColor Cyan
Write-Host 'Admin + Asaas + entrega no site. Uma vez só, sem novela.' -ForegroundColor DarkGray
Write-Host ''

Write-Host '[1/4] Painel administrativo' -ForegroundColor Yellow
$AdminPassword = Read-Required 'Crie a senha do painel (mínimo 12 caracteres)'
if ($AdminPassword.Length -lt 12) { throw 'Use uma senha com pelo menos 12 caracteres.' }
$PrivateKeyPath = Read-Host "Chave privada PEM [$DefaultPrivateKey]"
if ([string]::IsNullOrWhiteSpace($PrivateKeyPath)) { $PrivateKeyPath = $DefaultPrivateKey }
$PrivateKey = Get-Content -LiteralPath (Resolve-Path -LiteralPath $PrivateKeyPath) -Raw
if ($PrivateKey -notmatch 'BEGIN PRIVATE KEY') { throw 'A chave privada PKCS#8 é inválida.' }

Write-Host '[2/4] Pagamentos Asaas' -ForegroundColor Yellow
$AsaasKey = Read-Required 'Cole a API Key do Asaas'
$EnvironmentChoice = Read-Host 'Ambiente: S para Sandbox ou P para Produção'
$AsaasEnvironment = if ($EnvironmentChoice.Trim().ToUpperInvariant() -eq 'P') { 'production' } else { 'sandbox' }
$AsaasBase = if ($AsaasEnvironment -eq 'production') { 'https://api.asaas.com/v3' } else { 'https://api-sandbox.asaas.com/v3' }
$NotificationEmail = Read-Required 'E-mail que receberá alertas do pagamento'
if ($NotificationEmail -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Informe um e-mail válido.' }

Write-Host '[3/4] Entrega pelo site' -ForegroundColor Yellow
$InstallerPath = Read-Host "Caminho do instalador [$DefaultInstaller]"
if ([string]::IsNullOrWhiteSpace($InstallerPath)) { $InstallerPath = $DefaultInstaller }
$ResolvedInstaller = Resolve-Path -LiteralPath $InstallerPath
$InstallerHash = (Get-FileHash -LiteralPath $ResolvedInstaller -Algorithm SHA256).Hash

$Secrets = @{
  ADMIN_PASSWORD = $AdminPassword
  ADMIN_SESSION_SECRET = New-RandomSecret
  LICENSE_PRIVATE_KEY_PEM = $PrivateKey
  ASAAS_API_KEY = $AsaasKey
  ASAAS_WEBHOOK_TOKEN = New-RandomSecret
  ASAAS_ENVIRONMENT = $AsaasEnvironment
  PUBLIC_SITE_URL = $SiteUrl
  DUCKTIMIZER_PRICE = '17.50'
  DELIVERY_TOKEN_SECRET = New-RandomSecret
  INSTALLER_OBJECT_KEY = $InstallerKey
  INSTALLER_SHA256 = $InstallerHash
}

try {
  Write-Host '[4/4] Configurando Cloudflare e Asaas' -ForegroundColor Yellow
  $SecretFile = Join-Path ([IO.Path]::GetTempPath()) ("ducktimizer-secrets-" + [Guid]::NewGuid().ToString('N') + '.json')
  [IO.File]::WriteAllText($SecretFile, ($Secrets | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
  npx wrangler secret bulk $SecretFile --name $WorkerName
  if ($LASTEXITCODE -ne 0) { throw 'Falha ao salvar os segredos no Cloudflare.' }

  $BucketList = npx wrangler r2 bucket list 2>&1
  if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar o Cloudflare R2.' }
  if (($BucketList -join "`n") -notmatch [regex]::Escape($BucketName)) {
    npx wrangler r2 bucket create $BucketName
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o bucket privado.' }
  }
  npx wrangler r2 object put "$BucketName/$InstallerKey" --file $ResolvedInstaller --content-type application/octet-stream --remote
  if ($LASTEXITCODE -ne 0) { throw 'Falha ao enviar o instalador.' }

  $WebhookBody = @{
    name = 'Ducktimizer Checkout'
    url = $WebhookUrl
    email = $NotificationEmail
    enabled = $true
    interrupted = $false
    apiVersion = 3
    authToken = $Secrets.ASAAS_WEBHOOK_TOKEN
    sendType = 'SEQUENTIALLY'
    events = @('CHECKOUT_CREATED', 'CHECKOUT_PAID', 'CHECKOUT_CANCELED', 'CHECKOUT_EXPIRED')
  } | ConvertTo-Json -Depth 4
  $AsaasHeaders = @{ access_token = $AsaasKey; accept = 'application/json' }
  $ExistingWebhooks = Invoke-RestMethod -Uri "$AsaasBase/webhooks?limit=100" -Method Get -Headers $AsaasHeaders
  $ExistingWebhook = @($ExistingWebhooks.data) | Where-Object { $_.url -eq $WebhookUrl } | Select-Object -First 1
  if ($ExistingWebhook) {
    Invoke-RestMethod -Uri "$AsaasBase/webhooks/$($ExistingWebhook.id)" -Method Put -Headers $AsaasHeaders -ContentType 'application/json' -Body $WebhookBody | Out-Null
  } else {
    Invoke-RestMethod -Uri "$AsaasBase/webhooks" -Method Post -Headers $AsaasHeaders -ContentType 'application/json' -Body $WebhookBody | Out-Null
  }

  Write-Host ''
  Write-Host 'Tudo configurado com sucesso. O pato está operacional 🦆' -ForegroundColor Green
  Write-Host "Painel: $SiteUrl/admin"
  Write-Host "Asaas: $AsaasEnvironment"
  Write-Host "Instalador: $InstallerKey"
  Write-Host "SHA-256: $InstallerHash"
} finally {
  if ($SecretFile -and (Test-Path -LiteralPath $SecretFile)) { Remove-Item -LiteralPath $SecretFile -Force }
  $AdminPassword = $null
  $PrivateKey = $null
  $AsaasKey = $null
  $Secrets = $null
}
