import { eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { licenses, orders } from '@/db/schema';
import { isAdmin, unauthorized } from '@/lib/admin-auth';
import { createActivationKey, hashActivationKey } from '@/lib/activation-key';
import {
  hasSameOrigin,
  rateLimited,
  rateLimitResponse,
  requestTooLarge,
} from '@/lib/security';

export async function POST(request: Request) {
  if (!hasSameOrigin(request))
    return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (!(await isAdmin(request))) return unauthorized();
  if (requestTooLarge(request, 8_192))
    return Response.json(
      { message: 'Solicitação muito grande.' },
      { status: 413 },
    );
  if (await rateLimited(request, 'admin-license', 30, 300))
    return rateLimitResponse();
  try {
    const body = (await request.json()) as {
      orderId?: string;
      customer?: string;
      days?: number | null;
    };
    let customer = body.customer?.trim();
    let orderId = body.orderId?.trim();
    const db = getDb();

    if (orderId) {
      const order = await db
        .select()
        .from(orders)
        .where(eq(orders.id, orderId))
        .limit(1);
      if (!order.length)
        return Response.json(
          { message: 'Pedido não encontrado.' },
          { status: 404 },
        );
      if (order[0].status !== 'paid')
        return Response.json(
          {
            message:
              'A licença automática exige um pedido confirmado como pago.',
          },
          { status: 409 },
        );
      customer = order[0].name;
    }
    if (
      !customer ||
      customer.length < 2 ||
      customer.length > 80
    ) {
      return Response.json(
        { message: 'Informe um cliente válido.' },
        { status: 400 },
      );
    }
    const days = body.days === null ? null : Number(body.days);
    if (days !== null && (!Number.isInteger(days) || days < 1 || days > 3650)) {
      return Response.json(
        { message: 'A validade deve ficar entre 1 e 3650 dias.' },
        { status: 400 },
      );
    }

    if (!orderId) {
      orderId = crypto.randomUUID();
      const now = new Date();
      await db.insert(orders).values({
        id: orderId,
        name: customer,
        email: `manual-${orderId}@ducktimizer.local`,
        deviceId: 'UNBOUND',
        status: 'licensed',
        paidAt: now,
        createdAt: now,
        updatedAt: now,
      });
    } else {
      const existing = await db
        .select({ id: licenses.id })
        .from(licenses)
        .where(eq(licenses.orderId, orderId))
        .limit(1);
      if (existing.length)
        return Response.json(
          { message: 'Este pedido já possui licença.' },
          { status: 409 },
        );
    }

    const activationKey = createActivationKey();
    const expiresAt = days === null ? new Date('9999-12-31T23:59:59.999Z') : new Date(Date.now() + days * 86_400_000);
    await db.insert(licenses).values({
      id: crypto.randomUUID(),
      orderId,
      deviceId: 'UNBOUND',
      customer,
      licenseToken: activationKey,
      activationKeyHash: await hashActivationKey(activationKey),
      expiresAt,
      createdAt: new Date(),
    });
    await db
      .update(orders)
      .set({ status: 'licensed', updatedAt: new Date() })
      .where(eq(orders.id, orderId));
    return Response.json(
      { token: activationKey, expiresAt: expiresAt.toISOString(), orderId },
      { headers: { 'cache-control': 'no-store' } },
    );
  } catch (error) {
    console.error(
      'license_issue_failed',
      error instanceof Error ? error.message : 'unknown',
    );
    return Response.json(
      {
        message:
          'Não foi possível gerar a chave de ativação.',
      },
      { status: 500 },
    );
  }
}
