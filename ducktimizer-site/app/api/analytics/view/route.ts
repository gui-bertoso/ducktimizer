import { sql } from 'drizzle-orm';
import { getDb } from '@/db';
import { dailyMetrics } from '@/db/schema';
import { rateLimited } from '@/lib/security';

export async function POST(request: Request) {
  if (await rateLimited(request, 'page-view', 30, 3600)) return new Response(null, { status: 204 });
  const day = new Date().toISOString().slice(0, 10);
  await getDb()
    .insert(dailyMetrics)
    .values({ day, pageViews: 1 })
    .onConflictDoUpdate({
      target: dailyMetrics.day,
      set: { pageViews: sql`${dailyMetrics.pageViews} + 1` },
    });
  return new Response(null, { status: 204 });
}
