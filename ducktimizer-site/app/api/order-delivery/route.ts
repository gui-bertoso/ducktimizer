import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { deliveries, licenses, orders } from '@/db/schema';
import { createDeliveryToken, hashDeliveryToken } from '@/lib/delivery';
import { hashOrderClaim, readOrderClaim } from '@/lib/order-claim';
import { rateLimited, rateLimitResponse } from '@/lib/security';

export async function GET(request: Request) {
  if (await rateLimited(request, 'order-delivery', 60, 300)) return rateLimitResponse();
  const claim = readOrderClaim(request);
  if (!claim) return Response.json({ message: 'Compra não identificada.' }, { status: 401 });
  const claimHash = await hashOrderClaim(claim.token);
  const db = getDb();
  const order = (await db.select().from(orders).where(and(eq(orders.id, claim.orderId), eq(orders.claimTokenHash, claimHash))).limit(1))[0];
  if (!order) return Response.json({ message: 'Acesso à compra expirado.' }, { status: 401 });
  if (order.status === 'pending' || order.status === 'paid') {
    return Response.json({ status: 'pending', order: order.id.slice(0, 8).toUpperCase() }, { headers: { 'cache-control': 'no-store' } });
  }
  if (order.status === 'cancelled') return Response.json({ status: 'cancelled' }, { headers: { 'cache-control': 'no-store' } });

  const [license, delivery] = await Promise.all([
    db.select().from(licenses).where(eq(licenses.orderId, order.id)).limit(1),
    db.select().from(deliveries).where(eq(deliveries.orderId, order.id)).limit(1),
  ]);
  if (!license.length || !delivery.length) return Response.json({ status: 'pending' }, { status: 202, headers: { 'cache-control': 'no-store' } });
  const now = new Date();
  const downloadToken = await createDeliveryToken(order.id);
  const downloadTokenHash = await hashDeliveryToken(downloadToken);
  if (!delivery[0].deliveredAt || delivery[0].expiresAt <= now || delivery[0].tokenHash !== downloadTokenHash) {
    await db.update(deliveries).set({
      deliveredAt: delivery[0].deliveredAt ?? now,
      expiresAt: delivery[0].expiresAt <= now ? new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000) : delivery[0].expiresAt,
      tokenHash: downloadTokenHash,
      lastError: null,
    }).where(eq(deliveries.id, delivery[0].id));
  }
  return Response.json({
    status: 'ready',
    order: order.id.slice(0, 8).toUpperCase(),
    customer: order.name,
    activationStatus: order.deviceId === 'UNBOUND' ? 'Aguardando primeira ativação' : 'Vinculada ao dispositivo',
    paidAt: order.paidAt?.toISOString() ?? null,
    price: (order.amountCents / 100).toFixed(2).replace('.', ','),
    license: license[0].licenseToken,
    downloadUrl: `/api/download?token=${encodeURIComponent(downloadToken)}`,
    downloadsUsed: delivery[0].downloadCount,
    downloadsAllowed: delivery[0].maxDownloads,
  }, { headers: { 'cache-control': 'no-store' } });
}
