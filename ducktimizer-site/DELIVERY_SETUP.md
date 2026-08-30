# Entrega automática pelo site

Quando o Asaas envia `CHECKOUT_PAID`, o Worker cria a licença permanente e libera ao comprador:

- licença vinculada ao Device ID;
- link privado do instalador, válido por 7 dias e até 5 downloads;
- confirmação da compra com pedido, valor, data e referência Asaas.

O instalador fica em um bucket R2 privado. Um cookie temporário e protegido vincula o navegador ao pedido sem colocar a licença na URL. O webhook usa idempotência para evitar duplicação.

## Configurar

1. Abra o PowerShell nesta pasta e execute `./setup-ducktimizer.ps1`.
2. Configure o painel e o Asaas pelo assistente.
3. Faça o deploy e conclua uma compra no Sandbox do Asaas.

O painel `/admin` mostra se a entrega foi acessada, se houve falha e quantos downloads foram usados. A confirmação exibida no site não substitui nota fiscal.
