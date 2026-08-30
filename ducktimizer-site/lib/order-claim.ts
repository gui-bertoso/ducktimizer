import { env } from 'cloudflare:workers';

const encoder = new TextEncoder();

function base64Url(bytes: Uint8Array) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function createOrderClaim() {
  return base64Url(crypto.getRandomValues(new Uint8Array(32)));
}

export async function hashOrderClaim(token: string) {
  const key = await crypto.subtle.importKey(
    'raw', encoder.encode(env.DELIVERY_TOKEN_SECRET),
    { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'],
  );
  return base64Url(new Uint8Array(await crypto.subtle.sign('HMAC', key, encoder.encode(`claim|${token}`))));
}

export function orderClaimCookie(orderId: string, token: string) {
  return `ducktimizer_order=${encodeURIComponent(`${orderId}.${token}`)}; Path=/; Max-Age=172800; HttpOnly; Secure; SameSite=Lax`;
}

export function readOrderClaim(request: Request) {
  const cookie = request.headers.get('cookie') || '';
  const value = cookie.split(';').map(part => part.trim()).find(part => part.startsWith('ducktimizer_order='))?.slice('ducktimizer_order='.length);
  if (!value) return null;
  let decoded: string;
  try { decoded = decodeURIComponent(value); } catch { return null; }
  const separator = decoded.indexOf('.');
  if (separator < 1) return null;
  const orderId = decoded.slice(0, separator);
  const token = decoded.slice(separator + 1);
  if (!/^[0-9a-f-]{36}$/i.test(orderId) || !/^[A-Za-z0-9_-]{40,60}$/.test(token)) return null;
  return { orderId, token };
}
