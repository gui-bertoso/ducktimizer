import { env } from 'cloudflare:workers';

const cookieName = 'ducktimizer_admin';
const encoder = new TextEncoder();

function base64Url(bytes: Uint8Array) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

async function hmac(value: string) {
  const key = await crypto.subtle.importKey('raw', encoder.encode(env.ADMIN_SESSION_SECRET), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  return base64Url(new Uint8Array(await crypto.subtle.sign('HMAC', key, encoder.encode(value))));
}

async function safeEqual(left: string, right: string) {
  const [a, b] = await Promise.all([
    crypto.subtle.digest('SHA-256', encoder.encode(left)),
    crypto.subtle.digest('SHA-256', encoder.encode(right)),
  ]);
  const aa = new Uint8Array(a); const bb = new Uint8Array(b);
  let difference = aa.length ^ bb.length;
  for (let index = 0; index < Math.min(aa.length, bb.length); index++) difference |= aa[index] ^ bb[index];
  return difference === 0;
}

export async function passwordIsValid(password: string) {
  return Boolean(env.ADMIN_PASSWORD && await safeEqual(password, env.ADMIN_PASSWORD));
}

export async function createAdminCookie() {
  if (!env.ADMIN_SESSION_SECRET || env.ADMIN_SESSION_SECRET.length < 32) throw new Error('ADMIN_SESSION_SECRET inválido.');
  const expires = Math.floor(Date.now() / 1000) + 8 * 60 * 60;
  const value = `${expires}.${await hmac(String(expires))}`;
  return `${cookieName}=${value}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=28800`;
}

export function clearAdminCookie() {
  return `${cookieName}=; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=0`;
}

export async function isAdmin(request: Request) {
  if (!env.ADMIN_SESSION_SECRET) return false;
  const cookie = request.headers.get('cookie')?.split(';').map((part) => part.trim()).find((part) => part.startsWith(`${cookieName}=`));
  const token = cookie?.slice(cookieName.length + 1);
  if (!token) return false;
  const [expiresText, signature] = token.split('.');
  const expires = Number(expiresText);
  if (!expires || expires < Math.floor(Date.now() / 1000) || !signature) return false;
  return safeEqual(signature, await hmac(expiresText));
}

export function unauthorized() {
  return Response.json({ message: 'Sessão administrativa necessária.' }, { status: 401 });
}
