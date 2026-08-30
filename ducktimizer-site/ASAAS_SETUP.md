# Ativar pagamentos Asaas

O site já contém checkout por Pix/cartão, webhook autenticado e conciliação no D1.

## Configuração rápida

1. Gere uma API Key na conta Sandbox ou Produção do Asaas.
2. Abra o PowerShell nesta pasta.
3. Execute `./setup-ducktimizer.ps1`.
4. Cole as credenciais solicitadas pelo assistente único.

O configurador cria automaticamente um token de webhook forte, salva os segredos no Worker `fluffy-octo-palm-tree` e cadastra estes eventos no Asaas:

- `CHECKOUT_CREATED`
- `CHECKOUT_PAID`
- `CHECKOUT_CANCELED`
- `CHECKOUT_EXPIRED`

Webhook público: `https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/api/webhooks/asaas`

Comece pelo Sandbox. Quando validar a compra completa, execute novamente escolhendo Produção e remova/desative no painel do Asaas o webhook antigo de Sandbox caso ele apareça duplicado.

Nunca coloque a API Key no GitHub, no código do navegador ou em mensagens públicas.
