import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { orders, webhookEvents } from '@/db/schema';
import { matchesWebhookToken } from '@/lib/asaas';
import { requestTooLarge } from '@/lib/security';

type CheckoutEvent = {
  id?: string;
  event?: string;
  checkout?: { id?: string; externalReference?: string; status?: string };
};

export async function POST(request: Request) {
  if (requestTooLarge(request, 65_536)) return Response.json({ received: false }, { status: 413 });
  if (!await matchesWebhookToken(request.headers.get('asaas-access-token'))) {
    return Response.json({ received: false }, { status: 401 });
  }

  let payload: CheckoutEvent;
  try { payload = await request.json() as CheckoutEvent; }
  catch { return Response.json({ received: false }, { status: 400 }); }

  const allowedEvents = new Set(['CHECKOUT_PAID', 'CHECKOUT_CANCELED', 'CHECKOUT_EXPIRED']);
  if (!payload.id || payload.id.length > 120 || !payload.event || !allowedEvents.has(payload.event)) return Response.json({ received: false }, { status: 400 });
  if (payload.checkout?.id && payload.checkout.id.length > 120) return Response.json({ received: false }, { status: 400 });
  if (payload.checkout?.externalReference && !/^[0-9a-f-]{36}$/i.test(payload.checkout.externalReference)) return Response.json({ received: false }, { status: 400 });
  const db = getDb();
  const duplicate = await db.select({ id: webhookEvents.id }).from(webhookEvents).where(eq(webhookEvents.id, payload.id)).limit(1);
  if (duplicate.length) return Response.json({ received: true, duplicate: true });

  const checkoutId = payload.checkout?.id ?? null;

  const reference = payload.checkout?.externalReference;
  if (checkoutId || reference) {
    const where = checkoutId && reference
      ? and(eq(orders.asaasCheckoutId, checkoutId), eq(orders.id, reference))
      : checkoutId ? eq(orders.asaasCheckoutId, checkoutId) : eq(orders.id, reference!);
    const now = new Date();
    if (payload.event === 'CHECKOUT_PAID') {
      await db.update(orders).set({ status: 'paid', paidAt: now, updatedAt: now }).where(where);
    } else if (payload.event === 'CHECKOUT_CANCELED' || payload.event === 'CHECKOUT_EXPIRED') {
      await db.update(orders).set({ status: 'cancelled', updatedAt: now }).where(where);
    }
  }

  await db.insert(webhookEvents).values({ id: payload.id, event: payload.event, checkoutId, receivedAt: new Date() });

  return Response.json({ received: true });
}
