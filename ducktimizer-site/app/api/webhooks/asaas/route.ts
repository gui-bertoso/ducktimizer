import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { deliveries, licenses, orders, webhookEvents } from '@/db/schema';
import { matchesWebhookToken } from '@/lib/asaas';
import { createDeliveryToken, hashDeliveryToken } from '@/lib/delivery';
import { issueLicense } from '@/lib/license-signing';
import { requestTooLarge } from '@/lib/security';

type CheckoutEvent = {
  id?: string;
  event?: string;
  checkout?: { id?: string; externalReference?: string; status?: string };
};

export async function POST(request: Request) {
  if (requestTooLarge(request, 65_536))
    return Response.json({ received: false }, { status: 413 });
  if (!(await matchesWebhookToken(request.headers.get('asaas-access-token')))) {
    return Response.json({ received: false }, { status: 401 });
  }

  let payload: CheckoutEvent;
  try {
    payload = (await request.json()) as CheckoutEvent;
  } catch {
    return Response.json({ received: false }, { status: 400 });
  }

  const allowedEvents = new Set([
    'CHECKOUT_PAID',
    'CHECKOUT_CANCELED',
    'CHECKOUT_EXPIRED',
  ]);
  if (
    !payload.id ||
    payload.id.length > 120 ||
    !payload.event ||
    !allowedEvents.has(payload.event)
  )
    return Response.json({ received: false }, { status: 400 });
  if (payload.checkout?.id && payload.checkout.id.length > 120)
    return Response.json({ received: false }, { status: 400 });
  if (
    payload.checkout?.externalReference &&
    !/^[0-9a-f-]{36}$/i.test(payload.checkout.externalReference)
  )
    return Response.json({ received: false }, { status: 400 });
  const db = getDb();
  const duplicate = await db
    .select({ id: webhookEvents.id })
    .from(webhookEvents)
    .where(eq(webhookEvents.id, payload.id))
    .limit(1);
  if (duplicate.length)
    return Response.json({ received: true, duplicate: true });

  const checkoutId = payload.checkout?.id ?? null;

  const reference = payload.checkout?.externalReference;
  if (!checkoutId && !reference)
    return Response.json({ received: false }, { status: 400 });
  const where =
    checkoutId && reference
      ? and(eq(orders.asaasCheckoutId, checkoutId), eq(orders.id, reference))
      : checkoutId
        ? eq(orders.asaasCheckoutId, checkoutId)
        : eq(orders.id, reference!);
  const matching = await db.select().from(orders).where(where).limit(1);
  if (!matching.length)
    return Response.json({ received: false }, { status: 404 });
  const order = matching[0];
  const now = new Date();

  if (payload.event === 'CHECKOUT_PAID') {
    try {
      await db
        .update(orders)
        .set({ status: 'paid', paidAt: order.paidAt ?? now, updatedAt: now })
        .where(eq(orders.id, order.id));

      let license = (
        await db
          .select()
          .from(licenses)
          .where(eq(licenses.orderId, order.id))
          .limit(1)
      )[0];
      if (!license) {
        const issued = await issueLicense(order.name, order.deviceId, null);
        await db.insert(licenses).values({
          id: crypto.randomUUID(),
          orderId: order.id,
          deviceId: order.deviceId,
          customer: order.name,
          licenseToken: issued.token,
          expiresAt: issued.expires,
          createdAt: now,
        });
        license = (
          await db
            .select()
            .from(licenses)
            .where(eq(licenses.orderId, order.id))
            .limit(1)
        )[0];
      }

      const downloadToken = await createDeliveryToken(order.id);
      const tokenHash = await hashDeliveryToken(downloadToken);
      let delivery = (
        await db
          .select()
          .from(deliveries)
          .where(eq(deliveries.orderId, order.id))
          .limit(1)
      )[0];
      if (!delivery) {
        await db.insert(deliveries).values({
          id: crypto.randomUUID(),
          orderId: order.id,
          tokenHash,
          expiresAt: new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000),
          createdAt: now,
        });
        delivery = (
          await db
            .select()
            .from(deliveries)
            .where(eq(deliveries.orderId, order.id))
            .limit(1)
        )[0];
      }

      await db
        .update(orders)
        .set({ status: 'licensed', updatedAt: now })
        .where(eq(orders.id, order.id));
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message.slice(0, 500)
          : 'Falha desconhecida';
      const existingDelivery = await db
        .select({ id: deliveries.id })
        .from(deliveries)
        .where(eq(deliveries.orderId, order.id))
        .limit(1);
      if (existingDelivery.length)
        await db
          .update(deliveries)
          .set({ lastError: message })
          .where(eq(deliveries.id, existingDelivery[0].id));
      console.error('site_delivery_failed', message);
      return Response.json(
        { received: false, retry: true },
        { status: 503, headers: { 'retry-after': '60' } },
      );
    }
  } else {
    await db
      .update(orders)
      .set({ status: 'cancelled', updatedAt: now })
      .where(eq(orders.id, order.id));
  }

  await db
    .insert(webhookEvents)
    .values({
      id: payload.id,
      event: payload.event,
      checkoutId,
      receivedAt: now,
    });

  return Response.json({ received: true });
}
