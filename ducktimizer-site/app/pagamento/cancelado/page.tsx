import { PaymentResult } from '@/components/payment-result';

export default function PaymentCancelled() {
  return (
    <PaymentResult
      tone="warning"
      title="Compra cancelada"
      description="Nenhuma licença foi emitida. Você pode voltar ao site e iniciar um novo pagamento quando quiser."
    />
  );
}
