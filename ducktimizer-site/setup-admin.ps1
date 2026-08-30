$ErrorActionPreference = 'Stop'
$WorkerName = 'fluffy-octo-palm-tree'

Write-Host '=== Configurador do Ducktimizer Admin ===' -ForegroundColor Cyan
$Password = Read-Host 'Crie uma senha forte para o painel'
if ($Password.Length -lt 12) { throw 'Use uma senha com pelo menos 12 caracteres.' }

$DefaultKey = '..\outputs\Ducktimizer-Seller-Kit\ducktimizer-private.pem'
$KeyPath = Read-Host "Caminho da chave privada PEM [$DefaultKey]"
if ([string]::IsNullOrWhiteSpace($KeyPath)) { $KeyPath = $DefaultKey }
$ResolvedKey = Resolve-Path -LiteralPath $KeyPath
$PrivateKey = Get-Content -LiteralPath $ResolvedKey -Raw
if ($PrivateKey -notmatch 'BEGIN PRIVATE KEY') { throw 'O arquivo informado não é uma chave privada PKCS#8 válida.' }

$SessionSecret = ([Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'))

function Set-WorkerSecret([string]$Name, [string]$Value) {
  $SecretFile = Join-Path ([IO.Path]::GetTempPath()) ("ducktimizer-secret-" + [Guid]::NewGuid().ToString('N') + '.json')
  try {
    [IO.File]::WriteAllText($SecretFile, (@{ $Name = $Value } | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
    npx wrangler secret bulk $SecretFile --name $WorkerName
    if ($LASTEXITCODE -ne 0) { throw "Falha ao salvar o segredo $Name no Cloudflare." }
  } finally {
    if (Test-Path -LiteralPath $SecretFile) { Remove-Item -LiteralPath $SecretFile -Force }
  }
}

Write-Host 'Salvando os segredos no Cloudflare...'
Set-WorkerSecret 'ADMIN_PASSWORD' $Password
Set-WorkerSecret 'ADMIN_SESSION_SECRET' $SessionSecret
Set-WorkerSecret 'LICENSE_PRIVATE_KEY_PEM' $PrivateKey

Write-Host ''
Write-Host 'Painel administrativo configurado.' -ForegroundColor Green
Write-Host 'Acesse: https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/admin'
Write-Host 'A chave privada não foi copiada para os arquivos do site nem para o Git.'
