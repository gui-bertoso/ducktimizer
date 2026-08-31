import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { orders } from '@/db/schema';
import { createOrderClaim, hashOrderClaim, orderClaimCookie } from '@/lib/order-claim';
import { hasSameOrigin, rateLimited, rateLimitResponse, requestTooLarge } from '@/lib/security';

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const devicePattern = /^(?:[A-F0-9]{4}-){5}[A-F0-9]{4}$/;

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (requestTooLarge(request, 4_096)) return Response.json({ message: 'Solicitação muito grande.' }, { status: 413 });
  if (await rateLimited(request, 'purchase-recovery', 6, 900)) return rateLimitResponse();
  let body: { email?: string; deviceId?: string };
  try { body = await request.json() as { email?: string; deviceId?: string }; }
  catch { return Response.json({ message: 'Dados inválidos.' }, { status: 400 }); }
  const email = body.email?.trim().toLowerCase() || '';
  const deviceId = body.deviceId?.trim().toUpperCase() || '';
  if (!emailPattern.test(email) || email.length > 120 || !devicePattern.test(deviceId)) {
    return Response.json({ message: 'Informe o e-mail e o Device ID completos.' }, { status: 400 });
  }
  const db = getDb();
  const order = (await db.select().from(orders).where(and(eq(orders.email, email), eq(orders.deviceId, deviceId))).limit(1))[0];
  if (!order || !['paid', 'licensed'].includes(order.status)) {
    return Response.json({ message: 'Compra confirmada não encontrada com esses dados.' }, { status: 404 });
  }
  const claimToken = createOrderClaim();
  const claimTokenHash = await hashOrderClaim(claimToken);
  await db.update(orders).set({ claimTokenHash, updatedAt: new Date() }).where(eq(orders.id, order.id));
  return Response.json(
    { deliveryUrl: '/pagamento/sucesso' },
    { headers: { 'set-cookie': orderClaimCookie(order.id, claimToken), 'cache-control': 'no-store' } },
  );
}
