import { env } from 'cloudflare:workers';
import { installerObjectKey } from '@/lib/delivery';
import { rateLimited, rateLimitResponse } from '@/lib/security';

export async function GET(request: Request) {
  if (await rateLimited(request, 'public-update', 5, 3600)) return rateLimitResponse();
  const object = await env.INSTALLERS.get(installerObjectKey());
  if (!object) return new Response('Atualização indisponível.', { status: 503 });
  return new Response(object.body, { headers: {
    'content-type': 'application/octet-stream',
    'content-disposition': `attachment; filename="${installerObjectKey()}"`,
    'content-length': String(object.size),
    'cache-control': 'private, no-store',
    'x-content-type-options': 'nosniff',
    ...(env.INSTALLER_SHA256 ? { 'x-installer-sha256': env.INSTALLER_SHA256 } : {}),
  } });
}
