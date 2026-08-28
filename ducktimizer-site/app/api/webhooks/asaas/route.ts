import { eq, or } from 'drizzle-orm';
import { getDb } from '@/db';
import { orders, webhookEvents } from '@/db/schema';
import { matchesWebhookToken } from '@/lib/asaas';

type CheckoutEvent = {
  id?: string;
  event?: string;
  checkout?: { id?: string; externalReference?: string; status?: string };
};

export async function POST(request: Request) {
  if (!await matchesWebhookToken(request.headers.get('asaas-access-token'))) {
    return Response.json({ received: false }, { status: 401 });
  }

  let payload: CheckoutEvent;
  try { payload = await request.json() as CheckoutEvent; }
  catch { return Response.json({ received: false }, { status: 400 }); }

  if (!payload.id || !payload.event) return Response.json({ received: false }, { status: 400 });
  const db = getDb();
  const duplicate = await db.select({ id: webhookEvents.id }).from(webhookEvents).where(eq(webhookEvents.id, payload.id)).limit(1);
  if (duplicate.length) return Response.json({ received: true, duplicate: true });

  const checkoutId = payload.checkout?.id ?? null;
  await db.insert(webhookEvents).values({ id: payload.id, event: payload.event, checkoutId, receivedAt: new Date() });

  const reference = payload.checkout?.externalReference;
  if (checkoutId || reference) {
    const where = checkoutId && reference
      ? or(eq(orders.asaasCheckoutId, checkoutId), eq(orders.id, reference))
      : checkoutId ? eq(orders.asaasCheckoutId, checkoutId) : eq(orders.id, reference!);
    const now = new Date();
    if (payload.event === 'CHECKOUT_PAID') {
      await db.update(orders).set({ status: 'paid', paidAt: now, updatedAt: now }).where(where);
    } else if (payload.event === 'CHECKOUT_CANCELED' || payload.event === 'CHECKOUT_EXPIRED') {
      await db.update(orders).set({ status: 'cancelled', updatedAt: now }).where(where);
    }
  }

  return Response.json({ received: true });
}
