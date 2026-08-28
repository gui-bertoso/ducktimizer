import { PaymentResult } from '@/components/payment-result';

export default function PaymentSuccess() {
  return <PaymentResult tone="success" title="Pagamento enviado" description="Estamos confirmando o pagamento pelo Asaas. Assim que o webhook confirmar, seu pedido será marcado como pago para emissão da licença." />;
}
