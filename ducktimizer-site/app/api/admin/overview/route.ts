import { env } from 'cloudflare:workers';
import { isAdmin, unauthorized } from '@/lib/admin-auth';

export async function GET(request: Request) {
  if (!(await isAdmin(request))) return unauthorized();
  const [totals, orders, views] = await Promise.all([
    env.DB.prepare(`SELECT
      COUNT(*) AS totalOrders,
      SUM(CASE WHEN status = 'paid' THEN 1 ELSE 0 END) AS paid,
      SUM(CASE WHEN status = 'licensed' THEN 1 ELSE 0 END) AS licensed,
      SUM(CASE WHEN status = 'pending' THEN 1 ELSE 0 END) AS pending
      ,SUM(CASE WHEN status IN ('paid','licensed') THEN amount_cents ELSE 0 END) AS revenueCents
      FROM orders`).first(),
    env.DB.prepare(`SELECT o.id, o.name, o.email, o.device_id AS deviceId, o.status,
      o.asaas_checkout_id AS checkoutId, o.created_at AS createdAt, o.paid_at AS paidAt,
      d.delivered_at AS deliveredAt, d.last_error AS deliveryError,
      d.download_count AS downloadCount, d.max_downloads AS maxDownloads,
      l.enabled AS licenseEnabled, l.activated_at AS activatedAt
      FROM orders o LEFT JOIN deliveries d ON d.order_id = o.id LEFT JOIN licenses l ON l.order_id = o.id
      ORDER BY o.created_at DESC LIMIT 50`).all(),
    env.DB.prepare(
      `SELECT day, page_views AS pageViews FROM daily_metrics ORDER BY day DESC LIMIT 30`,
    ).all(),
  ]);
  const operations = await env.DB.prepare(`SELECT
    SUM(CASE WHEN activated_at IS NOT NULL THEN 1 ELSE 0 END) AS activated,
    SUM(CASE WHEN enabled = 0 THEN 1 ELSE 0 END) AS suspended FROM licenses`).first();
  return Response.json(
    { totals: { ...totals, ...operations }, orders: orders.results, views: views.results },
    { headers: { 'cache-control': 'no-store' } },
  );
}
