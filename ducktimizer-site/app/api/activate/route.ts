import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { licenses, orders } from '@/db/schema';
import { activationKeyPattern, hashActivationKey, normalizeActivationKey } from '@/lib/activation-key';
import { issueLicense } from '@/lib/license-signing';
import { rateLimited, rateLimitResponse, requestTooLarge } from '@/lib/security';

const devicePattern = /^(?:[A-F0-9]{4}-){5}[A-F0-9]{4}$/;

export async function POST(request: Request) {
  if (requestTooLarge(request, 4_096)) return Response.json({ message: 'Solicitação muito grande.' }, { status: 413 });
  if (await rateLimited(request, 'license-activation', 8, 900)) return rateLimitResponse();
  let body: { key?: string; deviceId?: string };
  try { body = await request.json() as { key?: string; deviceId?: string }; }
  catch { return Response.json({ message: 'Dados inválidos.' }, { status: 400 }); }
  const key = normalizeActivationKey(body.key ?? '');
  const deviceId = body.deviceId?.trim().toUpperCase() ?? '';
  if (!activationKeyPattern.test(key) || !devicePattern.test(deviceId)) {
    return Response.json({ message: 'Chave ou dispositivo inválido.' }, { status: 400 });
  }
  const keyHash = await hashActivationKey(key);
  const db = getDb();
  const license = (await db.select().from(licenses).where(eq(licenses.activationKeyHash, keyHash)).limit(1))[0];
  if (!license) return Response.json({ message: 'Chave de ativação inválida.' }, { status: 404 });
  if (license.expiresAt < new Date()) {
    return Response.json({ message: 'Esta licença expirou.' }, { status: 410 });
  }
  if (license.deviceId !== 'UNBOUND' && license.deviceId !== deviceId) {
    return Response.json({ message: 'Esta licença já está vinculada a outro computador.' }, { status: 409 });
  }
  if (license.deviceId === deviceId && license.activatedToken) {
    return Response.json({ token: license.activatedToken }, { headers: { 'cache-control': 'no-store' } });
  }
  if (license.deviceId === 'UNBOUND') {
    await db.update(licenses).set({ deviceId, activatedAt: new Date() }).where(and(eq(licenses.id, license.id), eq(licenses.deviceId, 'UNBOUND')));
    const bound = (await db.select({ deviceId: licenses.deviceId }).from(licenses).where(eq(licenses.id, license.id)).limit(1))[0];
    if (bound?.deviceId !== deviceId) {
      return Response.json({ message: 'Esta licença acabou de ser vinculada a outro computador.' }, { status: 409 });
    }
  }
  const days = license.expiresAt.getUTCFullYear() >= 9999 ? null : Math.max(1, Math.ceil((license.expiresAt.getTime() - Date.now()) / 86_400_000));
  const issued = await issueLicense(license.customer, deviceId, days);
  await db.update(licenses).set({ activatedToken: issued.token, activatedAt: new Date() }).where(eq(licenses.id, license.id));
  await db.update(orders).set({ deviceId, updatedAt: new Date() }).where(eq(orders.id, license.orderId));
  return Response.json({ token: issued.token }, { headers: { 'cache-control': 'no-store' } });
}
