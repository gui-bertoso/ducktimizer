# Guia do vendedor — Ducktimizer Pro

## Arquivo mais importante

Faça duas cópias seguras de `ducktimizer-private.pem`, de preferência em mídias diferentes e protegidas por senha. Essa chave não acompanha o aplicativo, não deve ir para o GitHub e nunca deve ser enviada ao cliente.

## Ativação de um cliente

1. O cliente abre o Ducktimizer e envia o Device ID mostrado na tela.
2. Gere uma licença informando a chave privada, nome do cliente, Device ID e validade.

```powershell
dotnet run --project .\tools\Ducktimizer.LicenseGenerator -- "C:\CAMINHO\ducktimizer-private.pem" "Nome do cliente" "XXXX-XXXX-XXXX-XXXX-XXXX-XXXX" vitalicia
```

Para uma licença de 365 dias, troque `vitalicia` por `365`.

3. Envie somente o texto gerado ao cliente. Ele cola esse texto na tela de ativação.

## Limites da proteção

A licença impede que a mesma chave funcione normalmente em outro computador. Nenhum software executado no PC do cliente é totalmente impossível de modificar; assinatura de código, ofuscação e validação online podem aumentar a proteção em versões futuras.
