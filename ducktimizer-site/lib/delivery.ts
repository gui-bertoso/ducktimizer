import { env } from 'cloudflare:workers';

const encoder = new TextEncoder();
const DEFAULT_INSTALLER_KEY = 'Ducktimizer-Setup-3.3.0-win-x64.exe';

function base64Url(bytes: Uint8Array) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

function escapeHtml(value: string) {
  return value.replace(
    /[&<>"']/g,
    (character) =>
      ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;',
      })[character]!,
  );
}

async function hmac(value: string) {
  const key = await crypto.subtle.importKey(
    'raw',
    encoder.encode(env.DELIVERY_TOKEN_SECRET),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  );
  return base64Url(
    new Uint8Array(
      await crypto.subtle.sign('HMAC', key, encoder.encode(value)),
    ),
  );
}

export function createDeliveryToken(orderId: string) {
  return hmac(`download|${orderId}`);
}

export function hashDeliveryToken(token: string) {
  return hmac(`stored|${token}`);
}

export function installerObjectKey() {
  return env.INSTALLER_OBJECT_KEY?.trim() || DEFAULT_INSTALLER_KEY;
}

type DeliveryEmail = {
  orderId: string;
  eventId: string;
  name: string;
  email: string;
  deviceId: string;
  checkoutId: string | null;
  paidAt: Date;
  licenseToken: string;
  downloadToken: string;
};

export async function sendDeliveryEmail(input: DeliveryEmail) {
  const siteUrl = (env.PUBLIC_SITE_URL || '').replace(/\/$/, '');
  if (!siteUrl.startsWith('https://'))
    throw new Error('PUBLIC_SITE_URL precisa usar HTTPS.');
  if (!env.RESEND_API_KEY || !env.DELIVERY_FROM_EMAIL)
    throw new Error('Entrega por e-mail não configurada.');

  const downloadUrl = `${siteUrl}/api/download?token=${encodeURIComponent(input.downloadToken)}`;
  const paidAt = new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'long',
    timeStyle: 'short',
    timeZone: 'America/Sao_Paulo',
  }).format(input.paidAt);
  const orderShort = input.orderId.slice(0, 8).toUpperCase();
  const safeName = escapeHtml(input.name);
  const safeLicense = escapeHtml(input.licenseToken);
  const safeDevice = escapeHtml(input.deviceId);
  const safeCheckout = escapeHtml(input.checkoutId || 'Não informado');
  const html = `<!doctype html><html><body style="margin:0;background:#101419;color:#eaf6ff;font-family:Arial,sans-serif"><div style="max-width:620px;margin:auto;padding:36px 20px"><div style="border:1px solid #303841;border-radius:22px;background:#171c22;padding:30px"><p style="margin:0;color:#9edcff;font-size:13px">DUCKTIMIZER PRO</p><h1 style="margin:10px 0 8px;font-size:28px">Pagamento confirmado.</h1><p style="color:#a8b3bf;line-height:1.6">Olá, ${safeName}. Sua licença permanente já está pronta.</p><a href="${downloadUrl}" style="display:inline-block;margin:18px 0 24px;background:#dff4ff;color:#101419;text-decoration:none;font-weight:bold;padding:14px 20px;border-radius:12px">Baixar instalador</a><p style="font-size:12px;color:#8794a2">O link é individual, permite até 5 downloads e expira em 7 dias.</p><h2 style="font-size:16px;margin-top:28px">Sua licença</h2><div style="word-break:break-all;background:#0f1318;border-radius:12px;padding:14px;font-family:monospace;font-size:12px;color:#dff4ff">${safeLicense}</div><h2 style="font-size:16px;margin-top:28px">Comprovante da compra</h2><table style="width:100%;color:#a8b3bf;font-size:13px;line-height:1.8"><tr><td>Pedido</td><td style="text-align:right;color:#fff">${orderShort}</td></tr><tr><td>Produto</td><td style="text-align:right;color:#fff">Ducktimizer Pro — licença permanente</td></tr><tr><td>Valor</td><td style="text-align:right;color:#fff">R$ 17,50</td></tr><tr><td>Confirmado em</td><td style="text-align:right;color:#fff">${escapeHtml(paidAt)}</td></tr><tr><td>Referência Asaas</td><td style="text-align:right;color:#fff">${safeCheckout}</td></tr><tr><td>Device ID</td><td style="text-align:right;color:#fff">${safeDevice}</td></tr></table><p style="margin-top:24px;color:#71808f;font-size:11px;line-height:1.5">Este e-mail confirma a entrega digital e não substitui documento fiscal quando ele for legalmente exigível.</p></div></div></body></html>`;
  const text = `Ducktimizer Pro\n\nOlá, ${input.name}. Pagamento confirmado.\n\nBaixe o instalador: ${downloadUrl}\nO link permite até 5 downloads e expira em 7 dias.\n\nLICENÇA PERMANENTE:\n${input.licenseToken}\n\nCOMPROVANTE DA COMPRA\nPedido: ${orderShort}\nProduto: Ducktimizer Pro — licença permanente\nValor: R$ 17,50\nConfirmado em: ${paidAt}\nReferência Asaas: ${input.checkoutId || 'Não informado'}\nDevice ID: ${input.deviceId}\n\nEsta confirmação de entrega digital não substitui documento fiscal quando legalmente exigível.`;

  const response = await fetch('https://api.resend.com/emails', {
    method: 'POST',
    headers: {
      authorization: `Bearer ${env.RESEND_API_KEY}`,
      'content-type': 'application/json',
      'idempotency-key': `purchase/${input.eventId}`,
    },
    body: JSON.stringify({
      from: env.DELIVERY_FROM_EMAIL,
      to: [input.email],
      subject: `Seu Ducktimizer Pro está pronto — pedido ${orderShort}`,
      html,
      text,
    }),
  });
  const result = (await response.json().catch(() => ({}))) as {
    id?: string;
    message?: string;
  };
  if (!response.ok || !result.id)
    throw new Error(
      `Resend ${response.status}: ${result.message || 'falha no envio'}`,
    );
  return result.id;
}
