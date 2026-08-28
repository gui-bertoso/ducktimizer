'use client';

import { useState } from 'react';
import { ArrowRight, Check, Cpu, Gauge, LockKeyhole, ShieldCheck, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export default function Home() {
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle');
  const [message, setMessage] = useState('');

  async function submit(formData: FormData) {
    setState('sending');
    setMessage('');
    const response = await fetch('/api/checkout', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify(Object.fromEntries(formData)),
    });
    const result = (await response.json()) as { message?: string; checkoutUrl?: string };
    if (response.ok && result.checkoutUrl) {
      setState('done');
      window.location.assign(result.checkoutUrl);
      return;
    }
    setMessage(result.message ?? 'Não foi possível iniciar o pagamento.');
    setState('error');
  }

  return (
    <main className="min-h-screen overflow-hidden bg-background text-foreground">
      <nav className="mx-auto flex max-w-6xl items-center justify-between px-6 py-5">
        <a href="#top" className="flex items-center gap-3" aria-label="Ducktimizer, início"><Gauge className="size-7 text-sky-200" /><span className="text-lg font-semibold tracking-tight">Ducktimizer</span><span className="rounded-full border border-sky-200/20 bg-sky-200/10 px-2 py-0.5 text-[10px] font-bold tracking-widest text-sky-100">PRO</span></a>
        <a href="#comprar" className="text-sm text-slate-300 transition hover:text-white">Comprar licença</a>
      </nav>
      <section id="top" className="relative mx-auto grid max-w-6xl gap-14 px-6 pb-24 pt-16 lg:grid-cols-[1.1fr_.9fr] lg:items-center lg:pt-24">
        <div className="pointer-events-none absolute -left-52 -top-44 size-[500px] rounded-full bg-sky-300/10 blur-3xl" />
        <div className="relative">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-sky-200/15 bg-sky-200/5 px-3 py-1.5 text-xs text-sky-100"><Sparkles className="size-3.5" /> Windows rápido. Sem firula.</div>
          <h1 className="max-w-3xl text-balance text-5xl font-semibold leading-[1.02] tracking-[-0.045em] sm:text-7xl">Menos peso no Windows. <span className="text-sky-200">Mais PC para você.</span></h1>
          <p className="mt-7 max-w-xl text-lg leading-8 text-slate-300">Otimização em poucos cliques para jogos, privacidade e desempenho. Você escolhe o nível; o Ducktimizer cuida do resto.</p>
          <div className="mt-9 flex flex-wrap gap-3"><Button className="h-12 rounded-xl bg-sky-100 px-6 text-slate-950 hover:bg-white" onClick={() => document.querySelector('#comprar')?.scrollIntoView({ behavior: 'smooth' })}>Quero otimizar meu PC <ArrowRight /></Button><div className="flex items-center gap-2 px-3 text-sm text-slate-400"><ShieldCheck className="size-4 text-emerald-300" /> Licença individual</div></div>
        </div>
        <div className="relative rounded-[28px] border border-white/10 bg-[#171b20] p-5 shadow-2xl shadow-sky-950/30">
          <div className="mb-5 flex items-center justify-between"><div><p className="font-semibold">Ducktimizer Pro</p><p className="text-xs text-slate-500">Poucas coisas. Muita diferença.</p></div><Gauge className="size-8 text-sky-100" /></div>
          <div className="grid gap-3 sm:grid-cols-2">
            {['Visual limpo', 'Modo gamer', 'Alto desempenho', 'Privacidade', 'Debloat extremo', 'Apps em segundo plano'].map((item, index) => <div key={item} className="flex items-center justify-between rounded-2xl border border-white/8 bg-white/[.035] px-4 py-4"><span className="text-sm font-medium">{item}</span><span className={`h-5 w-9 rounded-full p-0.5 ${index < 4 ? 'bg-sky-400' : 'bg-slate-700'}`}><span className={`block size-4 rounded-full bg-white ${index < 4 ? 'translate-x-4' : ''}`} /></span></div>)}
          </div>
          <div className="mt-4 flex items-center gap-3 rounded-2xl border border-sky-200/20 bg-sky-200/[.04] p-4"><span className="size-2 rounded-full bg-emerald-300" /><div><p className="text-sm font-semibold">Pronto para acelerar</p><p className="text-xs text-slate-500">Backup local antes das alterações</p></div></div>
        </div>
      </section>
      <section className="border-y border-white/8 bg-white/[.02]"><div className="mx-auto grid max-w-6xl gap-8 px-6 py-16 md:grid-cols-3">
        <article><Cpu className="mb-5 size-7 text-sky-200" /><h2 className="text-lg font-semibold">Performance prática</h2><p className="mt-2 leading-7 text-slate-400">Presets diretos para reduzir efeitos, processos e recursos que você não usa.</p></article>
        <article><LockKeyhole className="mb-5 size-7 text-sky-200" /><h2 className="text-lg font-semibold">Privacidade reforçada</h2><p className="mt-2 leading-7 text-slate-400">Reduz telemetria, anúncios, sugestões, Copilot, Recall e recursos de IA.</p></article>
        <article><ShieldCheck className="mb-5 size-7 text-sky-200" /><h2 className="text-lg font-semibold">Controle para voltar</h2><p className="mt-2 leading-7 text-slate-400">Histórico local, backup de configurações e opção para desativar as otimizações.</p></article>
      </div></section>
      <section id="comprar" className="mx-auto grid max-w-6xl gap-12 px-6 py-24 lg:grid-cols-[.8fr_1.2fr] lg:items-start">
        <div><p className="text-sm font-semibold uppercase tracking-[.2em] text-sky-200">Licença Pro</p><h2 className="mt-4 text-4xl font-semibold tracking-tight">Uma licença.<br />Um computador.</h2><p className="mt-5 text-slate-400">Oferta de lançamento: <span className="text-3xl font-semibold text-white">R$ 39,90</span> em pagamento único.</p><ul className="mt-8 space-y-3 text-sm text-slate-300">{['Ativação vinculada ao seu PC', 'Presets Low até Debloat Extremo', 'Atualizações da linha 3.x', 'Suporte para ativação'].map((item) => <li key={item} className="flex items-center gap-3"><Check className="size-4 text-emerald-300" />{item}</li>)}</ul></div>
        <form action={submit} className="rounded-[26px] border border-white/10 bg-[#171b20] p-6 sm:p-8">
          <h3 className="text-xl font-semibold">Comprar licença</h3><p className="mt-2 text-sm leading-6 text-slate-400">Informe seus dados e siga para o checkout seguro do Asaas. Após a confirmação, seu pedido fica pronto para emissão.</p>
          <div className="mt-7 grid gap-4 sm:grid-cols-2">
            <label htmlFor="customer-name" className="space-y-2 text-sm"><span>Seu nome</span><Input id="customer-name" name="name" required minLength={2} maxLength={80} placeholder="Como devemos chamar você?" className="h-11 border-white/10 bg-white/[.04]" /></label>
            <label htmlFor="customer-email" className="space-y-2 text-sm"><span>E-mail</span><Input id="customer-email" name="email" type="email" required maxLength={120} placeholder="voce@email.com" className="h-11 border-white/10 bg-white/[.04]" /></label>
            <label htmlFor="device-id" className="space-y-2 text-sm sm:col-span-2"><span>Device ID do Ducktimizer</span><Input id="device-id" name="deviceId" required pattern="[A-Fa-f0-9-]{24,40}" maxLength={40} placeholder="XXXX-XXXX-XXXX-XXXX-XXXX-XXXX" className="h-11 border-white/10 bg-white/[.04] font-mono" /></label>
            <input name="website" tabIndex={-1} autoComplete="off" className="hidden" aria-hidden="true" />
          </div>
          <Button type="submit" disabled={state === 'sending' || state === 'done'} className="mt-6 h-12 w-full rounded-xl bg-sky-100 text-slate-950 hover:bg-white">{state === 'sending' ? 'Abrindo pagamento…' : state === 'done' ? 'Redirecionando…' : 'Pagar com Pix ou cartão'} <ArrowRight /></Button>
          {message && <output className={`mt-4 block text-sm ${state === 'error' ? 'text-red-300' : 'text-emerald-300'}`}>{message}</output>}
          <p className="mt-4 text-xs leading-5 text-slate-500">Pagamento processado pelo Asaas. O Ducktimizer não recebe nem armazena os dados do seu cartão.</p>
        </form>
      </section>
      <footer className="border-t border-white/8 px-6 py-8 text-center text-xs text-slate-500">© 2026 patoLizo · Ducktimizer Pro · Windows 10 e 11</footer>
    </main>
  );
}
