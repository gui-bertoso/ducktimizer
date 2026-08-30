import { createAdminCookie, passwordIsValid } from '@/lib/admin-auth';
import {
  hasSameOrigin,
  rateLimited,
  rateLimitResponse,
  requestTooLarge,
} from '@/lib/security';

export async function POST(request: Request) {
  if (!hasSameOrigin(request))
    return Response.json({ message: 'Origem inválida.' }, { status: 403 });
  if (requestTooLarge(request, 2_048))
    return Response.json(
      { message: 'Solicitação muito grande.' },
      { status: 413 },
    );
  if (await rateLimited(request, 'admin-login', 5, 300))
    return rateLimitResponse();
  const body = (await request.json().catch(() => ({}))) as {
    password?: string;
  };
  if (!body.password || !(await passwordIsValid(body.password))) {
    return Response.json(
      { message: 'Credenciais inválidas.' },
      { status: 401, headers: { 'cache-control': 'no-store' } },
    );
  }
  return Response.json(
    { authenticated: true },
    {
      headers: {
        'set-cookie': await createAdminCookie(),
        'cache-control': 'no-store',
      },
    },
  );
}
