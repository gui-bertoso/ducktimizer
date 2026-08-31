import { env } from 'cloudflare:workers';

const encoder = new TextEncoder();
const DEFAULT_INSTALLER_KEY = 'Ducktimizer-Setup-3.4.2-win-x64.exe';

function base64Url(bytes: Uint8Array) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
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
