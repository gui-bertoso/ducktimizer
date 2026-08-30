import { PaymentResult } from '@/components/payment-result';

export default function PaymentSuccess() {
  return (
    <PaymentResult
      tone="success"
      title="Pagamento enviado"
      description="Assim que o Asaas confirmar, você receberá no e-mail o instalador, a licença permanente e o comprovante da compra. Confira também a caixa de spam."
    />
  );
}
