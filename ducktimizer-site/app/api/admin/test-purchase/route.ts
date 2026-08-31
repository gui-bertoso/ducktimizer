import { getDb } from '@/db';
import { orders } from '@/db/schema';
import { isAdmin, unauthorized } from '@/lib/admin-auth';
import { createOrderClaim, hashOrderClaim, orderClaimCookie } from '@/lib/order-claim';
import { fulfillPaidOrder } from '@/lib/purchase-fulfillment';
import { hasSameOrigin, rateLimited, rateLimitResponse } from '@/lib/security';

function testDeviceId() {
  const bytes = crypto.getRandomValues(new Uint8Array(12));
  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('').toUpperCase();
  return hex.match(/.{4}/g)!.join('-');
}

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (!await isAdmin(request)) return unauthorized();
  if (await rateLimited(request, 'admin-test-purchase', 5, 3600)) return rateLimitResponse();
  try {
    const now = new Date();
    const orderId = crypto.randomUUID();
    const claimToken = createOrderClaim();
    const claimTokenHash = await hashOrderClaim(claimToken);
    await getDb().insert(orders).values({
      id: orderId,
      name: 'Teste Ducktimizer',
      email: `teste-${orderId.slice(0, 8)}@ducktimizer.local`,
      deviceId: testDeviceId(),
      status: 'pending',
      claimTokenHash,
      legalAcceptedAt: now,
      createdAt: now,
      updatedAt: now,
    });
    await fulfillPaidOrder(orderId, now);
    return Response.json(
      { deliveryUrl: '/pagamento/sucesso', orderId },
      { status: 201, headers: { 'set-cookie': orderClaimCookie(orderId, claimToken), 'cache-control': 'no-store' } },
    );
  } catch (error) {
    console.error('test_purchase_failed', error instanceof Error ? error.message : 'unknown');
    return Response.json({ message: 'Não foi possível simular a compra.' }, { status: 500 });
  }
}
