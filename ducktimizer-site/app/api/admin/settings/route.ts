import { env } from 'cloudflare:workers';
import { isAdmin, unauthorized } from '@/lib/admin-auth';
import { getSettings, settingDefaults } from '@/lib/settings';
import { hasSameOrigin, requestTooLarge } from '@/lib/security';

export async function GET(request: Request) {
  if (!await isAdmin(request)) return unauthorized();
  return Response.json(await getSettings(), { headers: { 'cache-control': 'no-store' } });
}

export async function PUT(request: Request) {
  if (!hasSameOrigin(request)) return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (!await isAdmin(request)) return unauthorized();
  if (requestTooLarge(request, 16_384)) return Response.json({ message: 'Solicitação muito grande.' }, { status: 413 });
  const body = await request.json().catch(() => null) as Record<string, unknown> | null;
  if (!body) return Response.json({ message: 'Dados inválidos.' }, { status: 400 });
  const allowed = Object.keys(settingDefaults);
  const updates: Array<[string, string]> = [];
  for (const key of allowed) {
    if (!(key in body)) continue;
    const raw = body[key];
    if (typeof raw !== 'string' && typeof raw !== 'number') continue;
    const value = String(raw).trim().slice(0, key === 'releaseNotes' ? 1000 : 300);
    updates.push([key, value]);
  }
  const price = typeof body.price === 'string' || typeof body.price === 'number' ? Number(body.price) : Number.NaN;
  const promoPrice = typeof body.promoPrice === 'string' || typeof body.promoPrice === 'number' ? (body.promoPrice === '' ? null : Number(body.promoPrice)) : null;
  if (!Number.isFinite(price) || price < 1 || price > 10_000 || (promoPrice !== null && (!Number.isFinite(promoPrice) || promoPrice < 1 || promoPrice >= price))) {
    return Response.json({ message: 'Revise os preços. A promoção deve ser menor que o preço normal.' }, { status: 400 });
  }
  const now = Math.floor(Date.now() / 1000);
  await env.DB.batch(updates.map(([key, value]) => env.DB.prepare('INSERT INTO settings (key,value,updated_at) VALUES (?,?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at').bind(key, value, now)));
  await env.DB.prepare('INSERT INTO admin_audit (id,action,detail,created_at) VALUES (?,?,?,?)').bind(crypto.randomUUID(), 'settings.updated', `Campos: ${updates.map(item => item[0]).join(', ')}`, now).run();
  return Response.json({ saved: true });
}
