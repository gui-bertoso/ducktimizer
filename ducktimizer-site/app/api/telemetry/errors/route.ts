import { env } from 'cloudflare:workers';
import { rateLimited, rateLimitResponse, requestTooLarge } from '@/lib/security';

export async function POST(request: Request) {
  if (requestTooLarge(request, 4_096)) return Response.json({ message: 'Solicitação muito grande.' }, { status: 413 });
  if (await rateLimited(request, 'telemetry-errors', 20, 3600)) return rateLimitResponse();
  const body = await request.json().catch(() => null) as { kind?: string; context?: string; message?: string; appVersion?: string } | null;
  if (!body?.kind || !body.context || !body.message) return Response.json({ message: 'Dados incompletos.' }, { status: 400 });
  await env.DB.prepare('INSERT INTO telemetry_errors (id, kind, context, message, app_version, created_at) VALUES (?, ?, ?, ?, ?, ?)')
    .bind(crypto.randomUUID(), body.kind.slice(0, 32), body.context.slice(0, 160), body.message.slice(0, 2000), body.appVersion?.slice(0, 32) ?? null, Date.now()).run();
  return Response.json({ received: true }, { headers: { 'cache-control': 'no-store' } });
}
