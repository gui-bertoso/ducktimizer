'use client';

import { useEffect, useState } from 'react';
import { ArrowDown, ArrowRight, Check, Gauge, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import Link from 'next/link';

const adjustments = [
  [
    'Visual limpo',
    'Transparência, animações e efeitos que pesam na interface.',
  ],
  [
    'Modo gamer',
    'Game Mode, capturas em segundo plano e ajustes voltados a jogos.',
  ],
  ['Alto desempenho', 'Plano de energia preparado para priorizar resposta.'],
  ['Privacidade', 'Telemetria, histórico, anúncios e sugestões do Windows.'],
  [
    'Windows seco',
    'Suavização de fontes, sombras e efeitos visuais reduzidos ao mínimo.',
  ],
  [
    'Pesquisa enxuta',
    'Bing, destaques, histórico e sugestões online desativados.',
  ],
  ['Debloat', 'Copilot, Widgets e aplicativos pré-instalados selecionados.'],
  [
    'Sistema leve',
    'Apps em segundo plano, P2P de updates e atrasos desnecessários.',
  ],
];

export default function Home() {
  const [commerce, setCommerce] = useState({ price: '17,50', regularPrice: '17,50', promotion: false, promoEndsAt: null as string | null, salesReady: false });
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>(
    'idle',
  );
  const [message, setMessage] = useState('');

  useEffect(() => {
    void fetch('/api/analytics/view', { method: 'POST', keepalive: true });
    void fetch('/api/public-config').then(response => response.json()).then(value => setCommerce(value as typeof commerce)).catch(() => {});
  }, []);

  async function submit(formData: FormData) {
    setState('sending');
    setMessage('');
    const response = await fetch('/api/checkout', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(Object.fromEntries(formData)),
    });
    const result = (await response.json()) as {
      message?: string;
      checkoutUrl?: string;
      deliveryUrl?: string;
    };
    if (response.ok && result.checkoutUrl) {
      setState('done');
      window.location.assign(result.checkoutUrl);
      return;
    }
    if (response.ok && result.deliveryUrl) {
      window.location.assign(result.deliveryUrl);
      return;
    }
    setMessage(result.message ?? 'Não foi possível iniciar o pagamento.');
    setState('error');
  }

  return (
    <main className="min-h-screen bg-background text-foreground">
      <header className="border-b border-white/10">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-5 sm:px-8">
          <a
            href="#inicio"
            className="flex items-center gap-2.5"
            aria-label="Ducktimizer, início"
          >
            <Gauge className="size-6 text-sky-100" />
            <span className="font-semibold tracking-tight">Ducktimizer</span>
          </a>
          <div className="flex items-center gap-5 text-xs text-slate-400">
            <span className="hidden sm:inline">v3.4.1 · Windows 10/11</span>
            <a
              href="#comprar"
              className="border-b border-sky-100/60 pb-0.5 text-sky-100"
            >
              Comprar
            </a>
          </div>
        </div>
      </header>

      <section
        id="inicio"
        className="mx-auto grid max-w-6xl gap-12 px-5 py-16 sm:px-8 sm:py-24 lg:grid-cols-[.92fr_1.08fr] lg:items-center"
      >
        <div>
          <p className="font-mono text-xs uppercase tracking-[.18em] text-sky-200">
            Otimizador para Windows
          </p>
          <h1 className="mt-5 max-w-xl text-4xl font-semibold leading-[1.08] tracking-[-.035em] sm:text-6xl">
            Seu Windows.
            <br />
            Só o necessário.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-slate-400 sm:text-lg">
            O Ducktimizer reúne ajustes de desempenho, jogos e privacidade em
            uma tela pequena. Você escolhe o que muda e pode restaurar depois.
          </p>
          <div className="mt-8 flex flex-wrap items-center gap-4">
            <Button
              disabled={!commerce.salesReady}
              className="h-11 rounded-md bg-sky-100 px-5 text-slate-950 hover:bg-white"
              onClick={() =>
                document
                  .querySelector('#comprar')
                  ?.scrollIntoView({ behavior: 'smooth' })
              }
            >
              {commerce.salesReady ? `Comprar por R$ ${commerce.price}` : 'Vendas em configuração'} <ArrowRight />
            </Button>
            <a
              href="#ajustes"
              className="flex items-center gap-2 text-sm text-slate-400 hover:text-white"
            >
              Ver o que muda <ArrowDown className="size-4" />
            </a>
          </div>
          <p className="mt-7 text-xs leading-5 text-slate-600">
            Licença permanente individual vinculada ao computador. Pagamento
            único.
          </p>
        </div>

        <div className="border border-white/15 bg-[#15191c] p-4 shadow-[0_20px_70px_rgba(0,0,0,.35)] sm:p-5">
          <div className="mb-5 flex items-start justify-between border-b border-white/10 pb-4">
            <div>
              <p className="font-semibold">Ducktimizer Pro</p>
              <p className="mt-1 text-xs text-slate-500">
                Poucas coisas. Muita diferença.
              </p>
            </div>
            <Gauge className="size-7 text-sky-100" />
          </div>
          <div className="grid gap-px overflow-hidden border border-white/10 bg-white/10 sm:grid-cols-2">
            {[
              'Visual limpo',
              'Modo gamer',
              'Alto desempenho',
              'Privacidade',
              'Windows seco',
              'Pesquisa enxuta',
              'Debloat extremo',
              'Sistema leve',
            ].map((item, index) => (
              <div
                key={item}
                className="flex items-center justify-between bg-[#1a1f23] px-4 py-4"
              >
                <span className="text-sm font-medium">{item}</span>
                <span
                  className={`h-5 w-9 rounded-full p-0.5 ${index < 6 ? 'bg-sky-400' : 'bg-slate-700'}`}
                >
                  <span
                    className={`block size-4 rounded-full bg-white transition ${index < 6 ? 'translate-x-4' : ''}`}
                  />
                </span>
              </div>
            ))}
          </div>
          <div className="mt-4 flex items-center gap-3 border border-white/10 bg-[#1a1f23] px-4 py-3">
            <span className="size-2 rounded-full bg-emerald-300" />
            <div>
              <p className="text-sm font-medium">Pronto para acelerar</p>
              <p className="mt-0.5 text-xs text-slate-500">
                Backup local antes das alterações
              </p>
            </div>
          </div>
        </div>
      </section>

      <section id="ajustes" className="border-y border-white/10 bg-[#13171a]">
        <div className="mx-auto max-w-6xl px-5 py-16 sm:px-8 sm:py-20">
          <div className="grid gap-8 lg:grid-cols-[.65fr_1.35fr]">
            <div>
              <p className="font-mono text-xs uppercase tracking-[.18em] text-sky-200">
                O que ele faz
              </p>
              <h2 className="mt-4 text-3xl font-semibold tracking-tight">
                Oito grupos.
                <br />
                Sem menu infinito.
              </h2>
              <p className="mt-4 max-w-sm leading-7 text-slate-500">
                Os presets marcam os grupos por você. Ainda assim, cada chave
                continua sob seu controle.
              </p>
            </div>
            <div className="border-t border-white/10">
              {adjustments.map(([title, description], index) => (
                <div
                  key={title}
                  className="grid gap-2 border-b border-white/10 py-5 sm:grid-cols-[36px_190px_1fr]"
                >
                  <span className="font-mono text-xs text-slate-600">
                    0{index + 1}
                  </span>
                  <h3 className="text-sm font-semibold">{title}</h3>
                  <p className="text-sm leading-6 text-slate-400">
                    {description}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto grid max-w-6xl gap-10 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-2">
        <div className="border-l-2 border-sky-200 pl-5">
          <RotateCcw className="size-6 text-sky-200" />
          <h2 className="mt-5 text-2xl font-semibold">
            Não gostou do Windows “no seco”?
          </h2>
          <p className="mt-3 max-w-lg leading-7 text-slate-400">
            Use Restaurar para desfazer as configurações registradas pelo
            Ducktimizer. O app salva o estado local antes de aplicar os grupos
            selecionados.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-px border border-white/10 bg-white/10 text-sm">
          <div className="bg-[#15191c] p-5">
            <p className="font-mono text-xs text-slate-600">01</p>
            <p className="mt-7 font-medium">Escolha um preset</p>
          </div>
          <div className="bg-[#15191c] p-5">
            <p className="font-mono text-xs text-slate-600">02</p>
            <p className="mt-7 font-medium">Revise os switches</p>
          </div>
          <div className="bg-[#15191c] p-5">
            <p className="font-mono text-xs text-slate-600">03</p>
            <p className="mt-7 font-medium">Confirme o backup</p>
          </div>
          <div className="bg-[#15191c] p-5">
            <p className="font-mono text-xs text-slate-600">04</p>
            <p className="mt-7 font-medium">Aplique e reinicie</p>
          </div>
        </div>
      </section>

      <section id="comprar" className="border-t border-white/10 bg-[#13171a]">
        <div className="mx-auto grid max-w-6xl gap-12 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[.8fr_1.2fr]">
          <div>
            <p className="font-mono text-xs uppercase tracking-[.18em] text-sky-200">
              Ducktimizer Pro
            </p>
            <p className="mt-5 text-5xl font-semibold tracking-tight">
              {commerce.promotion && <span className="mr-3 text-lg text-slate-600 line-through">R$ {commerce.regularPrice}</span>}
              R$ {commerce.price}
            </p>
            <p className="mt-2 text-sm text-slate-500">
              pagamento único · licença permanente
            </p>
            <ul className="mt-8 space-y-3 text-sm text-slate-300">
              {[
                'Uma licença permanente para um computador',
                'Oito grupos e cinco presets',
                'Instalador e atualizações da linha 3.x',
                'Suporte de ativação',
              ].map((item) => (
                <li key={item} className="flex items-center gap-3">
                  <Check className="size-4 text-sky-200" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <form
            action={submit}
            className="border border-white/15 bg-[#171b1f] p-6 sm:p-8"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-xl font-semibold">Comprar licença</h2>
                <p className="mt-2 max-w-lg text-sm leading-6 text-slate-400">
                  Preencha seus dados. O pagamento abre no checkout seguro do
                  Asaas e o dispositivo será vinculado apenas na ativação.
                </p>
              </div>
              <span className="font-mono text-[10px] uppercase tracking-wider text-slate-600">
                Etapa 1/2
              </span>
            </div>
            <div className="mt-7 grid gap-5 sm:grid-cols-2">
              <label htmlFor="customer-name" className="space-y-2 text-sm">
                <span>Nome</span>
                <Input
                  id="customer-name"
                  name="name"
                  required
                  minLength={2}
                  maxLength={80}
                  autoComplete="name"
                  className="h-11 rounded-md border-white/15 bg-transparent"
                />
              </label>
              <label htmlFor="customer-email" className="space-y-2 text-sm">
                <span>E-mail</span>
                <Input
                  id="customer-email"
                  name="email"
                  type="email"
                  required
                  maxLength={120}
                  autoComplete="email"
                  className="h-11 rounded-md border-white/15 bg-transparent"
                />
              </label>
              <input
                name="website"
                tabIndex={-1}
                autoComplete="off"
                className="hidden"
                aria-hidden="true"
              />
              <label className="flex items-start gap-3 text-xs leading-5 text-slate-400 sm:col-span-2">
                <input
                  name="legalAccepted"
                  value="yes"
                  type="checkbox"
                  required
                  className="mt-1 size-4 accent-sky-200"
                />
                <span>
                  Li e aceito os{' '}
                  <Link
                    className="text-sky-200 underline underline-offset-2"
                    href="/termos"
                    target="_blank"
                  >
                    Termos de Uso
                  </Link>{' '}
                  e a{' '}
                  <Link
                    className="text-sky-200 underline underline-offset-2"
                    href="/privacidade"
                    target="_blank"
                  >
                    Política de Privacidade
                  </Link>
                  . Entendo que a licença ficará vinculada automaticamente ao
                  primeiro computador em que eu ativar o aplicativo.
                </span>
              </label>
            </div>
            <Button
              type="submit"
              disabled={!commerce.salesReady || state === 'sending' || state === 'done'}
              className="mt-6 h-12 w-full rounded-md bg-sky-100 text-slate-950 hover:bg-white"
            >
              {state === 'sending'
                ? 'Abrindo pagamento…'
                : state === 'done'
                  ? 'Redirecionando…'
                  : 'Ir para o pagamento'}{' '}
              <ArrowRight />
            </Button>
            {!commerce.salesReady && <p className="mt-3 text-center text-xs text-amber-300">A identificação legal do fornecedor está sendo concluída. As vendas serão liberadas em seguida.</p>}
            {message && (
              <output
                className={`mt-4 block text-sm ${state === 'error' ? 'text-red-300' : 'text-emerald-300'}`}
              >
                {message}
              </output>
            )}
            <p className="mt-4 text-xs leading-5 text-slate-600">
              Pix e cartão são processados pelo Asaas. Não armazenamos dados do
              seu cartão.
            </p>
          </form>
        </div>
      </section>

      <footer className="border-t border-white/10">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-5 py-7 text-xs text-slate-600 sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <span>© 2026 patoLizo</span>
          <nav className="flex flex-wrap gap-4">
            <Link className="hover:text-white" href="/termos">
              Termos de Uso
            </Link>
            <Link className="hover:text-white" href="/privacidade">
              Privacidade
            </Link>
            <Link className="hover:text-white" href="/recuperar">
              Recuperar compra
            </Link>
            <span>Ducktimizer Pro · Windows 10 e 11</span>
          </nav>
        </div>
      </footer>
    </main>
  );
}
