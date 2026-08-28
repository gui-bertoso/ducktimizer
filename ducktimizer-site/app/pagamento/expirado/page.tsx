import { PaymentResult } from '@/components/payment-result';

export default function PaymentExpired() {
  return <PaymentResult tone="warning" title="Checkout expirado" description="O link de pagamento perdeu a validade. Volte ao site para iniciar uma nova compra com segurança." />;
}
