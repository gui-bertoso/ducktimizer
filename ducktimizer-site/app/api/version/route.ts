import { getSettings } from '@/lib/settings';
import { env } from 'cloudflare:workers';
import { signMessage } from '@/lib/license-signing';

export async function GET(request: Request) {
  const settings = await getSettings();
  const origin = new URL(request.url).origin;
  const downloadUrl = `${origin}/api/update/download`;
  const sha256 = env.INSTALLER_SHA256?.trim().toUpperCase() || '';
  const signature = await signMessage(`${settings.latestVersion}|${sha256}|${downloadUrl}`);
  return Response.json({
    version: settings.latestVersion,
    notes: settings.releaseNotes,
    downloadUrl,
    sha256,
    signature,
  }, { headers: { 'cache-control': 'public, max-age=300' } });
}
