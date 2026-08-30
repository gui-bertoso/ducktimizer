'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  BarChart3,
  CheckCircle2,
  Clipboard,
  Gauge,
  KeyRound,
  LogOut,
  RefreshCw,
  ShoppingCart,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

type Order = {
  id: string;
  name: string;
  email: string;
  deviceId: string;
  status: string;
  checkoutId?: string;
  createdAt: number;
  paidAt?: number;
  deliveredAt?: number;
  deliveryError?: string;
  downloadCount?: number;
  maxDownloads?: number;
};
type Overview = {
  totals: {
    totalOrders?: number;
    paid?: number;
    licensed?: number;
    pending?: number;
  };
  orders: Order[];
  views: Array<{ day: string; pageViews: number }>;
};
type SubmitEvent = { preventDefault(): void; currentTarget: HTMLFormElement };

const statusLabels: Record<string, string> = {
  pending: 'Pendente',
  paid: 'Pago',
  licensed: 'Licenciado',
  cancelled: 'Cancelado',
};

export default function AdminPage() {
  const [authenticated, setAuthenticated] = useState<boolean | null>(null);
  const [overview, setOverview] = useState<Overview | null>(null);
  const [error, setError] = useState('');
  const [issuing, setIssuing] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<Order | null>(null);
  const [token, setToken] = useState('');

  const load = useCallback(async () => {
    const response = await fetch('/api/admin/overview', { cache: 'no-store' });
    if (response.status === 401) {
      setAuthenticated(false);
      setOverview(null);
      return;
    }
    if (!response.ok) {
      setError('Não foi possível carregar o painel.');
      return;
    }
    setOverview((await response.json()) as Overview);
    setAuthenticated(true);
    setError('');
  }, []);
  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  async function login(event: SubmitEvent) {
    event.preventDefault();
    setError('');
    const form = new FormData(event.currentTarget);
    const response = await fetch('/api/admin/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ password: form.get('password') }),
    });
    if (!response.ok) {
      setError('Senha incorreta.');
      return;
    }
    await load();
  }

  async function logout() {
    await fetch('/api/admin/logout', { method: 'POST' });
    setAuthenticated(false);
    setOverview(null);
  }

  async function generate(event: SubmitEvent) {
    event.preventDefault();
    setIssuing(true);
    setError('');
    setToken('');
    const form = new FormData(event.currentTarget);
    const lifetime = Boolean(selectedOrder) || form.get('lifetime') === 'on';
    const response = await fetch('/api/admin/licenses', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        orderId: selectedOrder?.id,
        customer: form.get('customer'),
        deviceId: form.get('deviceId'),
        days: lifetime ? null : Number(form.get('days')),
      }),
    });
    const result = (await response.json()) as {
      token?: string;
      message?: string;
    };
    if (!response.ok || !result.token)
      setError(result.message ?? 'Falha ao gerar licença.');
    else {
      setToken(result.token);
      setSelectedOrder(null);
      await load();
    }
    setIssuing(false);
  }

  const totalViews = useMemo(
    () =>
      overview?.views.reduce((sum, item) => sum + Number(item.pageViews), 0) ??
      0,
    [overview],
  );
  const maxViews = useMemo(
    () =>
      Math.max(
        1,
        ...(overview?.views.map((item) => Number(item.pageViews)) ?? [1]),
      ),
    [overview],
  );
  const cards: Array<[string, number, LucideIcon]> = [
    ['Pedidos', Number(overview?.totals.totalOrders ?? 0), ShoppingCart],
    ['Pendentes', Number(overview?.totals.pending ?? 0), RefreshCw],
    ['Pagos', Number(overview?.totals.paid ?? 0), CheckCircle2],
    ['Licenciados', Number(overview?.totals.licensed ?? 0), KeyRound],
    ['Acessos (30d)', totalViews, BarChart3],
  ];

  if (authenticated === null)
    return (
      <main className="grid min-h-screen place-items-center bg-background text-slate-400">
        Carregando painel…
      </main>
    );
  if (!authenticated)
    return (
      <main className="grid min-h-screen place-items-center bg-background px-6 text-foreground">
        <form
          onSubmit={login}
          className="w-full max-w-sm rounded-[26px] border border-white/10 bg-[#171b20] p-7 shadow-2xl"
        >
          <Gauge className="size-9 text-sky-100" />
          <h1 className="mt-5 text-2xl font-semibold">Painel Ducktimizer</h1>
          <p className="mt-2 text-sm text-slate-400">
            Área exclusiva do vendedor.
          </p>
          <label htmlFor="admin-password" className="mt-7 block text-sm">
            Senha
          </label>
          <Input
            id="admin-password"
            name="password"
            type="password"
            required
            className="mt-2 h-11 border-white/10 bg-white/[.04]"
          />
          <Button className="mt-4 h-11 w-full rounded-xl bg-sky-100 text-slate-950 hover:bg-white">
            Entrar
          </Button>
          {error && (
            <output className="mt-3 block text-sm text-red-300">{error}</output>
          )}
        </form>
      </main>
    );

  return (
    <main className="min-h-screen bg-background px-5 py-6 text-foreground">
      <div className="mx-auto max-w-7xl">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <Gauge className="size-8 text-sky-100" />
            <div>
              <h1 className="text-xl font-semibold">Ducktimizer Admin</h1>
              <p className="text-xs text-slate-500">
                Vendas, acessos e licenças
              </p>
            </div>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => void load()}>
              <RefreshCw /> Atualizar
            </Button>
            <Button variant="outline" onClick={() => void logout()}>
              <LogOut /> Sair
            </Button>
          </div>
        </header>

        <section className="mt-7 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          {cards.map(([label, value, Icon]) => (
            <article
              key={label}
              className="rounded-2xl border border-white/10 bg-[#171b20] p-5"
            >
              <Icon className="size-5 text-sky-200" />
              <p className="mt-5 text-3xl font-semibold">{value}</p>
              <p className="mt-1 text-xs text-slate-500">{label}</p>
            </article>
          ))}
        </section>

        <section className="mt-5 grid gap-5 lg:grid-cols-[1.35fr_.65fr]">
          <div className="overflow-hidden rounded-[22px] border border-white/10 bg-[#171b20]">
            <div className="border-b border-white/10 px-5 py-4">
              <h2 className="font-semibold">Pedidos recentes</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="text-xs text-slate-500">
                  <tr>
                    <th className="px-5 py-3">Cliente</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3">Device ID</th>
                    <th className="px-4 py-3">Entrega</th>
                    <th className="px-5 py-3">
                      <span className="sr-only">Ações</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {overview?.orders.map((order) => (
                    <tr key={order.id} className="border-t border-white/[.06]">
                      <td className="px-5 py-4">
                        <p className="font-medium">{order.name}</p>
                        <p className="text-xs text-slate-500">{order.email}</p>
                      </td>
                      <td className="px-4 py-4">
                        <span className="rounded-full bg-white/[.06] px-2.5 py-1 text-xs">
                          {statusLabels[order.status] ?? order.status}
                        </span>
                      </td>
                      <td className="px-4 py-4 font-mono text-xs text-slate-400">
                        {order.deviceId}
                      </td>
                      <td className="px-4 py-4 text-xs">
                        {order.deliveredAt ? (
                          <span className="text-emerald-300">
                            E-mail enviado · {order.downloadCount ?? 0}/
                            {order.maxDownloads ?? 5}
                          </span>
                        ) : order.deliveryError ? (
                          <span
                            className="text-red-300"
                            title={order.deliveryError}
                          >
                            Falha no envio
                          </span>
                        ) : order.status === 'paid' ? (
                          <span className="text-amber-300">Processando</span>
                        ) : (
                          <span className="text-slate-600">—</span>
                        )}
                      </td>
                      <td className="px-5 py-4 text-right">
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={order.status !== 'paid'}
                          onClick={() => {
                            setSelectedOrder(order);
                            setToken('');
                          }}
                        >
                          Gerar
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="space-y-5">
            <form
              onSubmit={generate}
              className="rounded-[22px] border border-white/10 bg-[#171b20] p-5"
            >
              <h2 className="font-semibold">Gerar licença</h2>
              <p className="mt-1 text-xs text-slate-500">
                {selectedOrder
                  ? `Pedido de ${selectedOrder.name}`
                  : 'Emissão manual para testes'}
              </p>
              <label
                htmlFor="license-customer"
                className="mt-5 block text-xs text-slate-400"
              >
                Cliente
              </label>
              <Input
                id="license-customer"
                name="customer"
                required={!selectedOrder}
                disabled={Boolean(selectedOrder)}
                defaultValue={selectedOrder?.name ?? ''}
                key={`customer-${selectedOrder?.id ?? 'manual'}`}
                className="mt-2 border-white/10 bg-white/[.04]"
              />
              <label
                htmlFor="license-device"
                className="mt-4 block text-xs text-slate-400"
              >
                Device ID
              </label>
              <Input
                id="license-device"
                name="deviceId"
                required={!selectedOrder}
                disabled={Boolean(selectedOrder)}
                defaultValue={selectedOrder?.deviceId ?? ''}
                key={`device-${selectedOrder?.id ?? 'manual'}`}
                placeholder="XXXX-XXXX-XXXX-XXXX-XXXX-XXXX"
                className="mt-2 border-white/10 bg-white/[.04] font-mono"
              />
              <label
                htmlFor="license-days"
                className="mt-4 block text-xs text-slate-400"
              >
                Validade em dias
              </label>
              <Input
                id="license-days"
                name="days"
                type="number"
                min="1"
                max="3650"
                defaultValue="365"
                className="mt-2 border-white/10 bg-white/[.04]"
              />
              <label className="mt-4 flex items-center gap-2 text-sm">
                <input
                  name="lifetime"
                  type="checkbox"
                  defaultChecked
                  className="accent-sky-300"
                />{' '}
                Licença permanente
              </label>
              <div className="mt-5 flex gap-2">
                {selectedOrder && (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => setSelectedOrder(null)}
                  >
                    Cancelar
                  </Button>
                )}
                <Button
                  disabled={issuing}
                  className="flex-1 bg-sky-100 text-slate-950 hover:bg-white"
                >
                  {issuing ? 'Assinando…' : 'Gerar licença'}
                </Button>
              </div>
            </form>
            <div className="rounded-[22px] border border-white/10 bg-[#171b20] p-5">
              <h2 className="font-semibold">Acessos diários</h2>
              <div className="mt-5 flex h-28 items-end gap-1">
                {overview?.views
                  .slice()
                  .reverse()
                  .map((item) => (
                    <div
                      key={item.day}
                      title={`${item.day}: ${item.pageViews}`}
                      className="min-w-1 flex-1 rounded-t bg-sky-300/70"
                      style={{
                        height: `${Math.max(5, (Number(item.pageViews) / maxViews) * 100)}%`,
                      }}
                    />
                  ))}
              </div>
            </div>
          </div>
        </section>
        {token && (
          <section className="mt-5 rounded-[22px] border border-emerald-300/20 bg-emerald-300/[.04] p-5">
            <div className="flex items-center justify-between gap-4">
              <div>
                <h2 className="font-semibold text-emerald-200">
                  Licença gerada
                </h2>
                <p className="mt-1 text-xs text-slate-500">
                  Copie e envie somente ao cliente correto.
                </p>
              </div>
              <Button onClick={() => void navigator.clipboard.writeText(token)}>
                <Clipboard /> Copiar
              </Button>
            </div>
            <textarea
              aria-label="Token da licença gerada"
              readOnly
              value={token}
              className="mt-4 h-28 w-full resize-none rounded-xl border border-white/10 bg-black/20 p-3 font-mono text-xs text-slate-300 outline-none"
            />
          </section>
        )}
        {error && (
          <output className="mt-4 block rounded-xl border border-red-300/20 bg-red-300/5 p-3 text-sm text-red-300">
            {error}
          </output>
        )}
      </div>
    </main>
  );
}
