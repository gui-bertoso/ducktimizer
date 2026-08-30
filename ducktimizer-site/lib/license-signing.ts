import { env } from 'cloudflare:workers';

function decodePem(pem: string) {
  const base64 = pem.replace(
    /-----BEGIN PRIVATE KEY-----|-----END PRIVATE KEY-----|\s/g,
    '',
  );
  if (!base64) throw new Error('LICENSE_PRIVATE_KEY_PEM não configurada.');
  const binary = atob(base64);
  return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

function base64Url(bytes: Uint8Array) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

export async function issueLicense(
  customer: string,
  deviceId: string,
  days: number | null,
) {
  const key = await crypto.subtle.importKey(
    'pkcs8',
    decodePem(env.LICENSE_PRIVATE_KEY_PEM),
    { name: 'ECDSA', namedCurve: 'P-256' },
    false,
    ['sign'],
  );
  const expires =
    days === null
      ? new Date('9999-12-31T23:59:59.999Z')
      : new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const payload = new TextEncoder().encode(
    JSON.stringify({
      Customer: customer,
      DeviceId: deviceId.toUpperCase(),
      ExpiresUtc: expires.toISOString(),
      Edition: 'Pro',
    }),
  );
  const signature = new Uint8Array(
    await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, key, payload),
  );
  return { token: `${base64Url(payload)}.${base64Url(signature)}`, expires };
}
