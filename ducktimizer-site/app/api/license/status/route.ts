import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { licenses } from '@/db/schema';
import { signMessage } from '@/lib/license-signing';
import { rateLimited, rateLimitResponse, requestTooLarge } from '@/lib/security';

const devicePattern = /^(?:[A-F0-9]{4}-){5}[A-F0-9]{4}$/;

export async function POST(request: Request) {
  if (requestTooLarge(request, 2_048)) return Response.json({ message: 'Solicitação muito grande.' }, { status: 413 });
  if (await rateLimited(request, 'license-status', 30, 3600)) return rateLimitResponse();
  const body = await request.json().catch(() => null) as { licenseId?: string; deviceId?: string } | null;
  const licenseId = body?.licenseId?.trim() ?? '';
  const deviceId = body?.deviceId?.trim().toUpperCase() ?? '';
  if (!/^[0-9a-f-]{36}$/i.test(licenseId) || !devicePattern.test(deviceId)) return Response.json({ active: false }, { status: 400 });
  const license = (await getDb().select({ enabled: licenses.enabled, expiresAt: licenses.expiresAt }).from(licenses).where(and(eq(licenses.id, licenseId), eq(licenses.deviceId, deviceId))).limit(1))[0];
  if (!license || !license.enabled || license.expiresAt < new Date()) return Response.json({ active: false }, { headers: { 'cache-control': 'no-store' } });
  const leaseUntil = new Date(Date.now() + 7 * 86_400_000).toISOString();
  const signature = await signMessage(`${licenseId}|${deviceId}|${leaseUntil}`);
  return Response.json({ active: true, leaseUntil, signature }, { headers: { 'cache-control': 'no-store' } });
}
