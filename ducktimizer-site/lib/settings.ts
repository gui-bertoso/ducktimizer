import { env } from 'cloudflare:workers';

export const settingDefaults = {
  price: env.DUCKTIMIZER_PRICE || '17.50',
  promoPrice: '',
  promoEndsAt: '',
  latestVersion: '3.4.1',
  releaseNotes: 'Ativação automática por dispositivo e melhorias de estabilidade.',
  businessName: '',
  taxId: '',
  businessAddress: '',
  supportEmail: '',
};

export type PublicSettings = typeof settingDefaults;

export async function getSettings(): Promise<PublicSettings> {
  const rows = await env.DB.prepare('SELECT key, value FROM settings').all<{ key: string; value: string }>();
  const values = { ...settingDefaults } as Record<string, string>;
  for (const row of rows.results) if (row.key in values) values[row.key] = row.value;
  return values as PublicSettings;
}

export function activePrice(settings: PublicSettings) {
  const promoActive = settings.promoPrice && settings.promoEndsAt && new Date(settings.promoEndsAt).getTime() > Date.now();
  const value = Number(promoActive ? settings.promoPrice : settings.price);
  return Number.isFinite(value) && value >= 1 && value <= 10_000 ? value : 17.5;
}

export function formatPrice(value: number) {
  return value.toFixed(2).replace('.', ',');
}
