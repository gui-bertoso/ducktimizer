import { clearAdminCookie } from '@/lib/admin-auth';
import { hasSameOrigin } from '@/lib/security';

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  return Response.json({ authenticated: false }, { headers: { 'set-cookie': clearAdminCookie() } });
}
