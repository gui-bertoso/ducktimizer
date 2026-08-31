import { activePrice, formatPrice, getSettings } from '@/lib/settings';

export async function GET() {
  const settings = await getSettings();
  const price = activePrice(settings);
  const salesReady = Boolean(settings.businessName && settings.taxId && settings.businessAddress && settings.supportEmail);
  return Response.json({
    price: formatPrice(price),
    regularPrice: formatPrice(Number(settings.price) || price),
    promotion: price !== Number(settings.price),
    promoEndsAt: settings.promoEndsAt || null,
    latestVersion: settings.latestVersion,
    supportEmail: settings.supportEmail || null,
    salesReady,
  }, { headers: { 'cache-control': 'public, max-age=60' } });
}
