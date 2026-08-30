import type { Metadata } from 'next';
import { Geist, Geist_Mono } from 'next/font/google';
import './globals.css';

const geistSans = Geist({
  variable: '--font-geist-sans',
  subsets: ['latin'],
});

const geistMono = Geist_Mono({
  variable: '--font-geist-mono',
  subsets: ['latin'],
});

export const metadata: Metadata = {
  metadataBase: new URL('https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev'),
  title: 'Ducktimizer Pro — Windows rápido. Sem firula.',
  description: 'Otimize desempenho, jogos e privacidade do Windows em poucos cliques.',
  applicationName: 'Ducktimizer Pro',
  robots: { index: true, follow: true },
  alternates: { canonical: '/' },
  openGraph: { title: 'Ducktimizer Pro', description: 'Seu Windows. Só o necessário.', type: 'website', locale: 'pt_BR' },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
      </body>
    </html>
  );
}
