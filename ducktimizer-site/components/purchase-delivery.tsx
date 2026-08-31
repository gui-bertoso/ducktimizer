'use client';

import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, Clipboard, Download, Gauge, LoaderCircle, RotateCcw } from 'lucide-react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';

type Delivery = {
  status: 'pending' | 'ready' | 'cancelled';
  order?: string;
  customer?: string;
  deviceId?: string;
  paidAt?: string | null;
  price?: string;
  license?: string;
  downloadUrl?: string;
  downloadsUsed?: number;
  downloadsAllowed?: number;
  message?: string;
};

export function PurchaseDelivery() {
  const [delivery, setDelivery] = useState<Delivery>({ status: 'pending' });
  const [failed, setFailed] = useState(false);
  const [copied, setCopied] = useState(false);

  const load = useCallback(async () => {
    try {
      const response = await fetch('/api/order-delivery', { cache: 'no-store' });
      const result = (await response.json()) as Delivery;
      if (response.status === 401) {
        setFailed(true);
        return true;
      }
      if (!response.ok && response.status !== 202) throw new Error();
      setDelivery(result);
      return result.status !== 'pending';
    } catch {
      setFailed(true);
      return true;
    }
  }, []);

  useEffect(() => {
    let stopped = false;
    let attempts = 0;
    async function poll() {
      const done = await load();
      attempts += 1;
      if (!stopped && !done && attempts < 80) window.setTimeout(poll, 3000);
    }
    void poll();
    return () => { stopped = true; };
  }, [load]);

  if (failed) return <Shell icon={<RotateCcw className="size-9 text-amber-200" />} title="Vamos recuperar sua compra">
    <p className="mt-3 text-sm leading-6 text-slate-400">Volte à compra e informe o mesmo e-mail e Device ID. O site reconhecerá o pedido pago e abrirá esta entrega novamente.</p>
    <Link className="mt-6 inline-flex h-10 w-full items-center justify-center rounded-lg bg-sky-100 px-4 text-sm font-medium text-slate-950 hover:bg-white" href="/recuperar">Recuperar compra</Link>
  </Shell>;

  if (delivery.status === 'cancelled') return <Shell icon={<RotateCcw className="size-9 text-amber-200" />} title="Pagamento não concluído">
    <p className="mt-3 text-sm text-slate-400">O Asaas informou que este checkout foi cancelado ou expirou.</p>
    <Link className="mt-6 inline-flex h-10 w-full items-center justify-center rounded-lg bg-sky-100 px-4 text-sm font-medium text-slate-950 hover:bg-white" href="/#comprar">Tentar novamente</Link>
  </Shell>;

  if (delivery.status !== 'ready') return <Shell icon={<LoaderCircle className="size-9 animate-spin text-sky-100" />} title="Confirmando pagamento…">
    <p className="mt-3 text-sm leading-6 text-slate-400">Assim que o Asaas confirmar, a licença e o instalador aparecem aqui automaticamente. Não feche esta página.</p>
    {delivery.order && <p className="mt-5 font-mono text-xs text-slate-600">Pedido {delivery.order}</p>}
  </Shell>;

  const paidAt = delivery.paidAt ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(delivery.paidAt)) : 'Confirmado';
  return <main className="min-h-screen bg-background px-5 py-10 text-foreground">
    <section className="mx-auto max-w-2xl rounded-[28px] border border-white/10 bg-[#171b20] p-6 shadow-2xl sm:p-8">
      <div className="flex items-start gap-4"><CheckCircle2 className="mt-1 size-9 shrink-0 text-emerald-300" /><div><p className="text-xs font-semibold tracking-[.16em] text-emerald-300">PAGAMENTO CONFIRMADO</p><h1 className="mt-2 text-3xl font-semibold tracking-tight">Seu Ducktimizer está pronto.</h1><p className="mt-2 text-sm text-slate-400">Salve a licença e baixe o instalador abaixo.</p></div></div>
      <div className="mt-7 grid gap-3 rounded-2xl border border-white/10 bg-black/15 p-4 text-sm sm:grid-cols-2">
        <Info label="Pedido" value={delivery.order!} mono /><Info label="Valor" value={`R$ ${delivery.price}`} /><Info label="Confirmado" value={paidAt} /><Info label="Device ID" value={delivery.deviceId!} mono />
      </div>
      <div className="mt-6"><div className="flex items-center justify-between"><h2 className="font-semibold">Licença permanente</h2><Button size="sm" variant="outline" onClick={async () => { await navigator.clipboard.writeText(delivery.license!); setCopied(true); }}><Clipboard />{copied ? 'Copiada' : 'Copiar'}</Button></div><textarea aria-label="Licença Ducktimizer" readOnly value={delivery.license} className="mt-3 h-32 w-full resize-none rounded-xl border border-white/10 bg-black/25 p-3 font-mono text-xs leading-5 text-sky-100 outline-none" /></div>
      <a className="mt-5 inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-sky-100 px-4 text-sm font-semibold text-slate-950 hover:bg-white" href={delivery.downloadUrl}><Download className="size-4" />Baixar instalador</a>
      <p className="mt-3 text-center text-xs text-slate-600">Downloads usados: {delivery.downloadsUsed}/{delivery.downloadsAllowed}. Guarde sua licença em local seguro.</p>
    </section>
  </main>;
}

function Shell({ icon, title, children }: { icon: React.ReactNode; title: string; children: React.ReactNode }) {
  return <main className="grid min-h-screen place-items-center bg-background px-5 text-foreground"><section className="w-full max-w-lg rounded-[28px] border border-white/10 bg-[#171b20] p-8 text-center shadow-2xl"><div className="mx-auto grid size-14 place-items-center rounded-2xl bg-white/[.05]">{icon}</div><h1 className="mt-5 text-2xl font-semibold">{title}</h1>{children}</section></main>;
}

function Info({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return <div><p className="text-xs text-slate-600">{label}</p><p className={`mt-1 text-slate-200 ${mono ? 'font-mono text-xs' : ''}`}>{value}</p></div>;
}
