import { Gauge } from 'lucide-react';
import Link from 'next/link';

export function PaymentResult({ title, description, tone }: { title: string; description: string; tone: 'success' | 'warning' }) {
  return <main className="grid min-h-screen place-items-center bg-background px-6 text-foreground">
    <section className="w-full max-w-lg rounded-[28px] border border-white/10 bg-[#171b20] p-8 text-center shadow-2xl">
      <Gauge className="mx-auto size-10 text-sky-100" />
      <span className={`mx-auto mt-7 block size-3 rounded-full ${tone === 'success' ? 'bg-emerald-300' : 'bg-amber-300'}`} />
      <h1 className="mt-4 text-3xl font-semibold tracking-tight">{title}</h1>
      <p className="mt-4 leading-7 text-slate-400">{description}</p>
      <Link href="/" className="mt-8 inline-flex rounded-xl bg-sky-100 px-6 py-3 text-sm font-semibold text-slate-950 transition hover:bg-white">Voltar ao Ducktimizer</Link>
    </section>
  </main>;
}
