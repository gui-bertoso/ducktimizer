# Entrega automática por e-mail

Quando o Asaas envia `CHECKOUT_PAID`, o Worker cria a licença permanente, registra a entrega e envia ao comprador:

- licença vinculada ao Device ID;
- link privado do instalador, válido por 7 dias e até 5 downloads;
- confirmação da compra com pedido, valor, data e referência Asaas.

O instalador fica em um bucket R2 privado. O e-mail leva um link assinado; ele não expõe o arquivo publicamente. O webhook e o envio na Resend usam idempotência para evitar duplicação.

## Configurar

1. Na Resend, verifique o domínio que será usado no remetente.
2. Crie uma API Key com permissão de envio.
3. Abra o PowerShell nesta pasta e execute `./setup-delivery.ps1`.
4. Cole a chave e informe o remetente, por exemplo `Ducktimizer <vendas@seudominio.com>`.
5. Faça o deploy e conclua uma compra no Sandbox do Asaas.

O painel `/admin` mostra se o e-mail foi enviado, se houve falha e quantos downloads foram usados. O “comprovante” no e-mail confirma a compra e a entrega digital; ele não é uma nota fiscal.
