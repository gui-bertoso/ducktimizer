import { env } from 'cloudflare:workers';

const encoder = new TextEncoder();

async function fingerprint(request: Request) {
  const ip = request.headers.get('cf-connecting-ip') || 'unknown';
  const userAgent = (request.headers.get('user-agent') || '').slice(0, 160);
  const salt = env.ADMIN_SESSION_SECRET || env.ASAAS_WEBHOOK_TOKEN || 'ducktimizer';
  const digest = await crypto.subtle.digest('SHA-256', encoder.encode(`${salt}|${ip}|${userAgent}`));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
}

export function requestTooLarge(request: Request, maximumBytes: number) {
  const length = Number(request.headers.get('content-length') || 0);
  return Number.isFinite(length) && length > maximumBytes;
}

export function hasSameOrigin(request: Request) {
  const origin = request.headers.get('origin');
  return Boolean(origin && origin === new URL(request.url).origin);
}

export async function rateLimited(request: Request, bucket: string, maximum: number, windowSeconds: number) {
  const key = await fingerprint(request);
  const now = Math.floor(Date.now() / 1000);
  const start = now - (now % windowSeconds);
  await env.DB.prepare(`INSERT INTO rate_limits (bucket, key_hash, window_start, request_count)
    VALUES (?, ?, ?, 1)
    ON CONFLICT(bucket, key_hash) DO UPDATE SET
      window_start = CASE WHEN window_start < excluded.window_start THEN excluded.window_start ELSE window_start END,
      request_count = CASE WHEN window_start < excluded.window_start THEN 1 ELSE request_count + 1 END`)
    .bind(bucket, key, start).run();
  const row = await env.DB.prepare('SELECT window_start AS windowStart, request_count AS requestCount FROM rate_limits WHERE bucket = ? AND key_hash = ?')
    .bind(bucket, key).first<{ windowStart: number; requestCount: number }>();
  return Boolean(row && row.windowStart === start && row.requestCount > maximum);
}

export function rateLimitResponse() {
  return Response.json({ message: 'Muitas tentativas. Aguarde alguns minutos.' }, { status: 429, headers: { 'retry-after': '300' } });
}
