import type { Metadata } from 'next';
import { PurchaseDelivery } from '@/components/purchase-delivery';

export const metadata: Metadata = { title: 'Sua compra — Ducktimizer', robots: { index: false, follow: false } };

export default function PaymentSuccess() {
  return <PurchaseDelivery />;
}
