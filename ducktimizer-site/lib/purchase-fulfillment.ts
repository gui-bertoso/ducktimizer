import { eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { deliveries, licenses, orders } from '@/db/schema';
import { createDeliveryToken, hashDeliveryToken } from '@/lib/delivery';
import { issueLicense } from '@/lib/license-signing';

export async function fulfillPaidOrder(orderId: string, now = new Date()) {
  const db = getDb();
  const order = (await db.select().from(orders).where(eq(orders.id, orderId)).limit(1))[0];
  if (!order) throw new Error('Pedido não encontrado.');

  await db.update(orders).set({ status: 'paid', paidAt: order.paidAt ?? now, updatedAt: now }).where(eq(orders.id, order.id));
  let license = (await db.select().from(licenses).where(eq(licenses.orderId, order.id)).limit(1))[0];
  if (!license) {
    const issued = await issueLicense(order.name, order.deviceId, null);
    await db.insert(licenses).values({
      id: crypto.randomUUID(), orderId: order.id, deviceId: order.deviceId,
      customer: order.name, licenseToken: issued.token,
      expiresAt: issued.expires, createdAt: now,
    });
    license = (await db.select().from(licenses).where(eq(licenses.orderId, order.id)).limit(1))[0];
  }

  const downloadToken = await createDeliveryToken(order.id);
  const tokenHash = await hashDeliveryToken(downloadToken);
  let delivery = (await db.select().from(deliveries).where(eq(deliveries.orderId, order.id)).limit(1))[0];
  if (!delivery) {
    await db.insert(deliveries).values({
      id: crypto.randomUUID(), orderId: order.id, tokenHash,
      expiresAt: new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000), createdAt: now,
    });
    delivery = (await db.select().from(deliveries).where(eq(deliveries.orderId, order.id)).limit(1))[0];
  }
  await db.update(orders).set({ status: 'licensed', updatedAt: now }).where(eq(orders.id, order.id));
  return { order, license, delivery };
}
