import { and, eq } from 'drizzle-orm';
import { env } from 'cloudflare:workers';
import { getDb } from '@/db';
import { orders } from '@/db/schema';
import { createAsaasCheckout } from '@/lib/asaas';
import {
  rateLimited,
  rateLimitResponse,
  requestTooLarge,
} from '@/lib/security';

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const devicePattern = /^(?:[A-F0-9]{4}-){5}[A-F0-9]{4}$/;

export async function POST(request: Request) {
  try {
    if (requestTooLarge(request, 8_192))
      return Response.json(
        { message: 'Solicitação muito grande.' },
        { status: 413 },
      );
    if (await rateLimited(request, 'checkout', 8, 300))
      return rateLimitResponse();
    const body = (await request.json()) as Record<string, string>;
    if (body.website)
      return Response.json(
        { message: 'Solicitação inválida.' },
        { status: 400 },
      );

    const name = body.name?.trim();
    const email = body.email?.trim().toLowerCase();
    const deviceId = body.deviceId?.trim().toUpperCase();
    if (body.legalAccepted !== 'yes') {
      return Response.json(
        {
          message:
            'Aceite os Termos de Uso e a Política de Privacidade para continuar.',
        },
        { status: 400 },
      );
    }
    if (
      !name ||
      name.length < 2 ||
      name.length > 80 ||
      !emailPattern.test(email) ||
      email.length > 120 ||
      !devicePattern.test(deviceId)
    ) {
      return Response.json(
        { message: 'Revise o nome, e-mail e Device ID.' },
        { status: 400 },
      );
    }

    const db = getDb();
    const existing = await db
      .select()
      .from(orders)
      .where(and(eq(orders.email, email), eq(orders.deviceId, deviceId)))
      .limit(1);

    if (existing[0]?.status === 'paid' || existing[0]?.status === 'licensed') {
      return Response.json(
        { message: 'Este pedido já foi pago. Verifique seu e-mail.' },
        { status: 409 },
      );
    }
    if (existing[0]?.status === 'pending' && existing[0]?.checkoutUrl) {
      await db
        .update(orders)
        .set({ legalAcceptedAt: new Date(), updatedAt: new Date() })
        .where(eq(orders.id, existing[0].id));
      return Response.json({ checkoutUrl: existing[0].checkoutUrl });
    }

    const now = new Date();
    const orderId = existing[0]?.id ?? crypto.randomUUID();
    if (!existing.length) {
      await db
        .insert(orders)
        .values({
          id: orderId,
          name,
          email,
          deviceId,
          status: 'pending',
          legalAcceptedAt: now,
          createdAt: now,
          updatedAt: now,
        });
    }

    const configuredUrl = env.PUBLIC_SITE_URL?.replace(/\/$/, '');
    const siteUrl = configuredUrl || new URL(request.url).origin;
    const checkout = await createAsaasCheckout({
      orderId,
      name,
      email,
      siteUrl,
    });
    await db
      .update(orders)
      .set({
        asaasCheckoutId: checkout.id,
        checkoutUrl: checkout.link,
        updatedAt: now,
      })
      .where(eq(orders.id, orderId));
    return Response.json({ checkoutUrl: checkout.link }, { status: 201 });
  } catch (error) {
    console.error(
      'checkout_create_failed',
      error instanceof Error ? error.message : 'unknown',
    );
    return Response.json(
      { message: 'Pagamento temporariamente indisponível. Tente novamente.' },
      { status: 503 },
    );
  }
}
