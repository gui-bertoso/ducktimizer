'use client';

import { useState } from 'react';
import { Gauge, RotateCcw } from 'lucide-react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export default function RecoverPage() {
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  async function recover(formData: FormData) {
    setLoading(true);
    setMessage('');
    const response = await fetch('/api/recover', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify(Object.fromEntries(formData)),
    });
    const result = await response.json() as { deliveryUrl?: string; message?: string };
    if (response.ok && result.deliveryUrl) { window.location.assign(result.deliveryUrl); return; }
    setMessage(result.message ?? 'Não foi possível recuperar a compra.');
    setLoading(false);
  }
  return <main className="grid min-h-screen place-items-center bg-background px-5 py-10 text-foreground">
    <section className="w-full max-w-lg rounded-[28px] border border-white/10 bg-[#171b20] p-7 shadow-2xl sm:p-8">
      <div className="flex items-center gap-3"><div className="grid size-12 place-items-center rounded-2xl bg-white/[.05]"><RotateCcw className="size-6 text-sky-100" /></div><div><p className="text-xs font-semibold tracking-[.14em] text-sky-200">RECUPERAÇÃO</p><h1 className="mt-1 text-2xl font-semibold">Recupere sua licença</h1></div></div>
      <p className="mt-5 text-sm leading-6 text-slate-400">Use o mesmo e-mail da compra e os 8 caracteres do pedido exibidos na entrega e no comprovante. Nenhum novo pagamento será criado.</p>
      <form action={recover} className="mt-6">
        <label htmlFor="recover-email" className="text-sm">E-mail da compra</label><Input id="recover-email" name="email" type="email" required autoComplete="email" className="mt-2 h-11 border-white/10 bg-white/[.04]" />
        <label htmlFor="recover-order" className="mt-4 block text-sm">Código do pedido</label><Input id="recover-order" name="orderCode" required placeholder="A1B2C3D4" pattern="[A-Fa-f0-9]{8}" maxLength={8} className="mt-2 h-11 border-white/10 bg-white/[.04] font-mono uppercase" />
        <Button disabled={loading} className="mt-6 h-11 w-full bg-sky-100 text-slate-950 hover:bg-white">{loading ? 'Procurando…' : 'Recuperar compra'}</Button>
        {message && <output className="mt-4 block text-sm text-red-300">{message}</output>}
      </form>
      <Link href="/" className="mt-6 flex items-center justify-center gap-2 text-xs text-slate-500 hover:text-slate-300"><Gauge className="size-4" />Voltar ao Ducktimizer</Link>
    </section>
  </main>;
}
