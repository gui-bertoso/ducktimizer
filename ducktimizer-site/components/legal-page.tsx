import { Gauge } from 'lucide-react';
import Link from 'next/link';

export type LegalSection = {
  title: string;
  paragraphs: string[];
  items?: string[];
};

export function LegalPage({
  eyebrow,
  title,
  updated,
  intro,
  sections,
}: {
  eyebrow: string;
  title: string;
  updated: string;
  intro: string;
  sections: LegalSection[];
}) {
  return (
    <main className="min-h-screen bg-background text-foreground">
      <header className="border-b border-white/10">
        <div className="mx-auto flex h-16 max-w-4xl items-center justify-between px-5 sm:px-8">
          <Link href="/" className="flex items-center gap-2.5">
            <Gauge className="size-6 text-sky-100" />
            <span className="font-semibold">Ducktimizer</span>
          </Link>
          <Link href="/" className="text-xs text-slate-400 hover:text-white">
            Voltar ao site
          </Link>
        </div>
      </header>
      <article className="mx-auto max-w-4xl px-5 py-14 sm:px-8 sm:py-20">
        <p className="font-mono text-xs uppercase tracking-[.18em] text-sky-200">
          {eyebrow}
        </p>
        <h1 className="mt-5 text-4xl font-semibold tracking-tight sm:text-5xl">
          {title}
        </h1>
        <p className="mt-4 text-sm text-slate-500">
          Última atualização: {updated}
        </p>
        <p className="mt-8 max-w-3xl text-base leading-8 text-slate-300">
          {intro}
        </p>
        <div className="mt-12 border-t border-white/10">
          {sections.map((section, index) => (
            <section
              key={section.title}
              className="grid gap-4 border-b border-white/10 py-8 sm:grid-cols-[48px_1fr]"
            >
              <span className="font-mono text-xs text-slate-600">
                {String(index + 1).padStart(2, '0')}
              </span>
              <div>
                <h2 className="text-lg font-semibold">{section.title}</h2>
                {section.paragraphs.map((p) => (
                  <p key={p} className="mt-4 leading-7 text-slate-400">
                    {p}
                  </p>
                ))}
                {section.items && (
                  <ul className="mt-4 list-disc space-y-2 pl-5 leading-7 text-slate-400">
                    {section.items.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                )}
              </div>
            </section>
          ))}
        </div>
        <p className="mt-8 text-xs leading-6 text-slate-600">
          Em caso de dúvida, use o canal de suporte informado no momento da
          compra. Estes documentos devem ser revisados por profissional jurídico
          antes do início da comercialização em escala.
        </p>
      </article>
    </main>
  );
}
