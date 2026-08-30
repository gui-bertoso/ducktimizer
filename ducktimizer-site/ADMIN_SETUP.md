# Painel administrativo

O painel fica em `/admin` e mostra pedidos, pagamentos, licenças e acessos dos últimos 30 dias.

## Ativação

1. Abra o PowerShell na pasta `ducktimizer-site`.
2. Execute `./setup-ducktimizer.ps1`.
3. Preencha o assistente único; ele configura painel, Asaas e entrega.

O script salva no Cloudflare, como segredos criptografados do Worker:

- `ADMIN_PASSWORD`
- `ADMIN_SESSION_SECRET`
- `LICENSE_PRIVATE_KEY_PEM`

A chave privada nunca é enviada ao navegador e não entra no Git. Guarde também uma cópia offline segura: sem ela não será possível emitir novas licenças compatíveis com a chave pública já embutida no aplicativo.

## Emissão

- Use **Gerar** em um pedido pago para vinculá-lo automaticamente.
- Use o formulário vazio para criar licenças manuais de teste.
- Compras confirmadas geram licença permanente. Para testes manuais, escolha uma validade ou marque **Licença permanente**.
- A chave gerada aparece uma vez no resultado para ser copiada.
