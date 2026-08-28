import { and, eq } from 'drizzle-orm';
import { getDb } from '@/db';
import { orders } from '@/db/schema';

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const devicePattern = /^(?:[A-F0-9]{4}-){5}[A-F0-9]{4}$/;

export async function POST(request: Request) {
  try {
    const body = await request.json() as Record<string, string>;
    if (body.website) return Response.json({ message: 'Pedido recebido.' });
    const name = body.name?.trim(); const email = body.email?.trim().toLowerCase(); const deviceId = body.deviceId?.trim().toUpperCase();
    if (!name || name.length < 2 || name.length > 80 || !emailPattern.test(email) || email.length > 120 || !devicePattern.test(deviceId)) return Response.json({ message: 'Revise o nome, e-mail e Device ID.' }, { status: 400 });
    const db = getDb();
    const existing = await db.select({ id: orders.id }).from(orders).where(and(eq(orders.email, email), eq(orders.deviceId, deviceId))).limit(1);
    if (existing.length) return Response.json({ message: 'Já existe um pedido para este e-mail e dispositivo.' });
    const now = new Date();
    await db.insert(orders).values({ id: crypto.randomUUID(), name, email, deviceId, status: 'pending', createdAt: now, updatedAt: now });
    return Response.json({ message: 'Pedido recebido! Entraremos em contato com as instruções de pagamento.' }, { status: 201 });
  } catch {
    return Response.json({ message: 'Serviço temporariamente indisponível. Tente novamente.' }, { status: 500 });
  }
}
