import { env } from 'cloudflare:workers';

type CheckoutResponse = {
  id?: string;
  link?: string;
  status?: string;
  errors?: Array<{ code?: string; description?: string }>;
};

export function asaasApiBase() {
  return env.ASAAS_ENVIRONMENT === 'production'
    ? 'https://api.asaas.com/v3'
    : 'https://api-sandbox.asaas.com/v3';
}

export function checkoutLink(checkout: CheckoutResponse) {
  if (checkout.link) return checkout.link;
  if (!checkout.id) return null;
  return env.ASAAS_ENVIRONMENT === 'production'
    ? `https://asaas.com/checkoutSession/show?id=${encodeURIComponent(checkout.id)}`
    : `https://sandbox.asaas.com/checkoutSession/show/${encodeURIComponent(checkout.id)}`;
}

export async function createAsaasCheckout(input: {
  orderId: string;
  name: string;
  email: string;
  siteUrl: string;
  price: number;
}) {
  if (!env.ASAAS_API_KEY) throw new Error('ASAAS_API_KEY não configurada.');

  const response = await fetch(`${asaasApiBase()}/checkouts`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      access_token: env.ASAAS_API_KEY,
      'content-type': 'application/json',
      'user-agent': 'Ducktimizer/3.3 checkout',
    },
    body: JSON.stringify({
      billingTypes: ['PIX', 'CREDIT_CARD'],
      chargeTypes: ['DETACHED'],
      minutesToExpire: 60,
      externalReference: input.orderId,
      callback: {
        successUrl: `${input.siteUrl}/pagamento/sucesso`,
        cancelUrl: `${input.siteUrl}/pagamento/cancelado`,
        expiredUrl: `${input.siteUrl}/pagamento/expirado`,
      },
      items: [
        {
          externalReference: 'ducktimizer-pro',
          name: 'Ducktimizer Pro',
          description:
            'Licença permanente individual vinculada a um computador',
          quantity: 1,
          value: input.price,
        },
      ],
      customerData: { name: input.name, email: input.email },
    }),
  });

  const result = (await response.json()) as CheckoutResponse;
  if (!response.ok || !result.id) {
    const detail =
      result.errors
        ?.map((error) => error.description)
        .filter(Boolean)
        .join(' ') || 'Falha ao criar checkout.';
    throw new Error(detail);
  }

  const link = checkoutLink(result);
  if (!link) throw new Error('O Asaas não retornou o link do checkout.');
  return { id: result.id, link };
}

export async function matchesWebhookToken(received: string | null) {
  if (!env.ASAAS_WEBHOOK_TOKEN || !received) return false;
  const encoder = new TextEncoder();
  const [expectedHash, receivedHash] = await Promise.all([
    crypto.subtle.digest('SHA-256', encoder.encode(env.ASAAS_WEBHOOK_TOKEN)),
    crypto.subtle.digest('SHA-256', encoder.encode(received)),
  ]);
  const expected = new Uint8Array(expectedHash);
  const actual = new Uint8Array(receivedHash);
  let difference = expected.length ^ actual.length;
  for (let index = 0; index < Math.min(expected.length, actual.length); index++)
    difference |= expected[index] ^ actual[index];
  return difference === 0;
}
