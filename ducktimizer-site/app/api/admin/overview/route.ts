import { env } from 'cloudflare:workers';
import { isAdmin, unauthorized } from '@/lib/admin-auth';

export async function GET(request: Request) {
  if (!await isAdmin(request)) return unauthorized();
  const [totals, orders, views] = await Promise.all([
    env.DB.prepare(`SELECT
      COUNT(*) AS totalOrders,
      SUM(CASE WHEN status = 'paid' THEN 1 ELSE 0 END) AS paid,
      SUM(CASE WHEN status = 'licensed' THEN 1 ELSE 0 END) AS licensed,
      SUM(CASE WHEN status = 'pending' THEN 1 ELSE 0 END) AS pending
      FROM orders`).first(),
    env.DB.prepare(`SELECT id, name, email, device_id AS deviceId, status, asaas_checkout_id AS checkoutId,
      created_at AS createdAt, paid_at AS paidAt FROM orders ORDER BY created_at DESC LIMIT 50`).all(),
    env.DB.prepare(`SELECT day, page_views AS pageViews FROM daily_metrics ORDER BY day DESC LIMIT 30`).all(),
  ]);
  return Response.json({ totals, orders: orders.results, views: views.results }, { headers: { 'cache-control': 'no-store' } });
}
