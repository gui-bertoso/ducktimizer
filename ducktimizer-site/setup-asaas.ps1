$ErrorActionPreference = 'Stop'

$WorkerName = 'fluffy-octo-palm-tree'
$SiteUrl = 'https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev'
$WebhookUrl = "$SiteUrl/api/webhooks/asaas"

Write-Host '=== Configurador Asaas do Ducktimizer ===' -ForegroundColor Cyan
$ApiKey = Read-Host 'Cole a API Key do Asaas'
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw 'A API Key é obrigatória.' }

$Choice = Read-Host 'Ambiente: digite S para Sandbox ou P para Produção'
$Environment = if ($Choice.Trim().ToUpperInvariant() -eq 'P') { 'production' } else { 'sandbox' }
$ApiBase = if ($Environment -eq 'production') { 'https://api.asaas.com/v3' } else { 'https://api-sandbox.asaas.com/v3' }
$NotificationEmail = Read-Host 'E-mail que receberá alertas do webhook'
if ($NotificationEmail -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Informe um e-mail válido.' }

$WebhookToken = ([Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'))

function Set-WorkerSecret([string]$Name, [string]$Value) {
  $Value | npx wrangler secret put $Name --name $WorkerName
  if ($LASTEXITCODE -ne 0) { throw "Falha ao salvar o segredo $Name no Cloudflare." }
}

Write-Host 'Salvando configurações seguras no Cloudflare...'
Set-WorkerSecret 'ASAAS_API_KEY' $ApiKey
Set-WorkerSecret 'ASAAS_WEBHOOK_TOKEN' $WebhookToken
Set-WorkerSecret 'ASAAS_ENVIRONMENT' $Environment
Set-WorkerSecret 'PUBLIC_SITE_URL' $SiteUrl
Set-WorkerSecret 'DUCKTIMIZER_PRICE' '39.90'

$WebhookBody = @{
  name = 'Ducktimizer Checkout'
  url = $WebhookUrl
  email = $NotificationEmail
  enabled = $true
  interrupted = $false
  apiVersion = 3
  authToken = $WebhookToken
  sendType = 'SEQUENTIALLY'
  events = @('CHECKOUT_CREATED', 'CHECKOUT_PAID', 'CHECKOUT_CANCELED', 'CHECKOUT_EXPIRED')
} | ConvertTo-Json -Depth 4

Write-Host 'Cadastrando webhook no Asaas...'
Invoke-RestMethod -Uri "$ApiBase/webhooks" -Method Post -Headers @{ access_token = $ApiKey; accept = 'application/json' } -ContentType 'application/json' -Body $WebhookBody | Out-Null

Write-Host ''
Write-Host 'Asaas configurado com sucesso.' -ForegroundColor Green
Write-Host "Checkout: $Environment"
Write-Host "Webhook: $WebhookUrl"
Write-Host 'A API Key e o token não foram gravados nos arquivos do projeto.'
