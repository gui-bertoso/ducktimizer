import { env } from 'cloudflare:workers';
import { isAdmin, unauthorized } from '@/lib/admin-auth';
import { hasSameOrigin } from '@/lib/security';

const statuses = new Set(['new', 'known', 'in_progress', 'fixed', 'ignored']);
export async function PATCH(request: Request) {
  if (!hasSameOrigin(request)) return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (!(await isAdmin(request))) return unauthorized();
  const body = await request.json().catch(() => null) as { id?: string; status?: string } | null;
  if (!body?.id || !body.status || !statuses.has(body.status)) return Response.json({ message: 'Status inválido.' }, { status: 400 });
  await env.DB.prepare('UPDATE telemetry_errors SET status = ? WHERE id = ?').bind(body.status, body.id).run();
  await env.DB.prepare('INSERT INTO admin_audit (id, action, detail, created_at) VALUES (?, ?, ?, ?)').bind(crypto.randomUUID(), 'telemetry.status.updated', `${body.id}:${body.status}`, Date.now()).run();
  return Response.json({ updated: true });
}
