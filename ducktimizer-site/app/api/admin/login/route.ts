import { createAdminCookie, passwordIsValid } from '@/lib/admin-auth';

export async function POST(request: Request) {
  const body = await request.json().catch(() => ({})) as { password?: string };
  if (!body.password || !await passwordIsValid(body.password)) {
    return Response.json({ message: 'Senha incorreta.' }, { status: 401 });
  }
  return Response.json({ authenticated: true }, { headers: { 'set-cookie': await createAdminCookie() } });
}
