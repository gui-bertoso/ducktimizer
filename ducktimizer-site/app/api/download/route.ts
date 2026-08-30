import { and, eq, gt, lt } from 'drizzle-orm';
import { getDb } from '@/db';
import { deliveries } from '@/db/schema';
import { hashDeliveryToken, installerObjectKey } from '@/lib/delivery';
import { rateLimited, rateLimitResponse } from '@/lib/security';
import { env } from 'cloudflare:workers';

export async function GET(request: Request) {
  if (await rateLimited(request, 'installer-download', 20, 300))
    return rateLimitResponse();
  const token = new URL(request.url).searchParams.get('token') || '';
  if (!/^[A-Za-z0-9_-]{40,60}$/.test(token))
    return new Response('Link inválido.', { status: 400 });

  const tokenHash = await hashDeliveryToken(token);
  const db = getDb();
  const rows = await db
    .select()
    .from(deliveries)
    .where(
      and(
        eq(deliveries.tokenHash, tokenHash),
        gt(deliveries.expiresAt, new Date()),
        lt(deliveries.downloadCount, deliveries.maxDownloads),
      ),
    )
    .limit(1);
  if (!rows.length)
    return new Response('Este link expirou ou atingiu o limite de downloads.', {
      status: 410,
    });

  const object = await env.INSTALLERS.get(installerObjectKey());
  if (!object)
    return new Response('Instalador temporariamente indisponível.', {
      status: 503,
    });
  const updated = await env.DB.prepare(
    `UPDATE deliveries SET download_count = download_count + 1, last_downloaded_at = ? WHERE id = ? AND download_count < max_downloads`,
  )
    .bind(Math.floor(Date.now() / 1000), rows[0].id)
    .run();
  if (!updated.meta.changes)
    return new Response('Limite de downloads atingido.', { status: 410 });

  return new Response(object.body, {
    headers: {
      'content-type':
        object.httpMetadata?.contentType || 'application/octet-stream',
      'content-disposition': `attachment; filename="${installerObjectKey()}"`,
      'content-length': String(object.size),
      etag: object.httpEtag,
      'cache-control': 'private, no-store',
      'x-content-type-options': 'nosniff',
      ...(env.INSTALLER_SHA256
        ? { 'x-installer-sha256': env.INSTALLER_SHA256 }
        : {}),
    },
  });
}
