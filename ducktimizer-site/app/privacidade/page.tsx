import type { Metadata } from 'next';
import { LegalPage } from '@/components/legal-page';
import { getSettings } from '@/lib/settings';

export const metadata: Metadata = {
  title: 'Política de Privacidade — Ducktimizer',
  description:
    'Como o Ducktimizer trata dados pessoais, pagamento e identificação do dispositivo.',
};

export default async function PrivacyPage() {
  const seller = await getSettings();
  return (
    <LegalPage
      eyebrow="LGPD e dados"
      title="Política de Privacidade"
      updated="31 de agosto de 2026"
      intro="Esta política explica quais dados são usados no site, na compra e na ativação. Aplicamos os princípios da necessidade, finalidade, segurança e transparência previstos na Lei Geral de Proteção de Dados (LGPD)."
      sections={[
        {
          title: 'Controlador e contato',
          paragraphs: [`O controlador dos dados é ${seller.businessName || 'o fornecedor identificado na oferta'}, CPF/CNPJ ${seller.taxId || 'não informado'}, com endereço em ${seller.businessAddress || 'não informado'}. Solicitações de privacidade e direitos dos titulares devem ser enviadas para ${seller.supportEmail || 'o canal de suporte informado no site'}.`],
        },
        {
          title: 'Dados tratados',
          paragraphs: [
            'No checkout podemos tratar nome, e-mail, identificadores técnicos da requisição, data, status e referência do pedido. O Device ID é recebido somente na primeira ativação. O painel administrativo registra informações necessárias para emissão, validação e suporte da licença. O aplicativo guarda localmente licença, logs operacionais, aceite dos termos e backups das configurações.',
            'Ao iniciar, o aplicativo pode consultar versão disponível e situação da licença. A validação envia identificador da licença e Device ID, sem arquivos pessoais, lista de programas ou conteúdo de documentos. Uma autorização assinada de curta duração permite tolerância temporária sem conexão.',
          ],
          items: [
            'Não armazenamos número completo de cartão ou código de segurança.',
            'O aplicativo não envia arquivos pessoais nem o conteúdo dos seus documentos.',
            'O Device ID é um identificador derivado do computador e usado para impedir ativação em outro dispositivo.',
          ],
        },
        {
          title: 'Finalidades e bases legais',
          paragraphs: [
            'Usamos os dados para processar a compra, entregar e validar a licença, prevenir fraude, prestar suporte, cumprir obrigações legais e entender acessos básicos ao site. O tratamento se apoia principalmente na execução do contrato, cumprimento de obrigação legal, exercício regular de direitos e legítimo interesse, conforme aplicável.',
          ],
        },
        {
          title: 'Pagamento, entrega e operadores',
          paragraphs: [
            'O pagamento é processado pelo Asaas, que recebe os dados necessários ao checkout sob seus próprios termos e política de privacidade. O site, banco de dados e download protegido utilizam infraestrutura Cloudflare. Esses fornecedores recebem apenas os dados necessários às respectivas funções e atuam como operadores ou controladores independentes conforme a atividade realizada.',
          ],
        },
        {
          title: 'Compartilhamento',
          paragraphs: [
            'Não vendemos dados pessoais. Podemos compartilhar o mínimo necessário com processadores de pagamento, hospedagem, prevenção a fraude, contabilidade, suporte técnico e autoridades quando houver obrigação legal ou ordem válida.',
          ],
        },
        {
          title: 'Retenção e segurança',
          paragraphs: [
            'Dados de pedido, licença e situação da entrega são conservados durante a relação contratual e, depois dela, pelos prazos necessários ao cumprimento de obrigações fiscais, defesa de direitos e prevenção a fraude. Dados que deixarem de ser necessários serão eliminados ou anonimizados, salvo conservação permitida ou exigida por lei. O link individual do instalador expira e tem limite de downloads. Logs e backups do aplicativo permanecem no próprio computador. Adotamos controle de acesso, limitação de tentativas, segregação de segredos, assinatura digital e registro administrativo, mas nenhum sistema é totalmente imune a incidentes.',
          ],
        },
        {
          title: 'Direitos do titular',
          paragraphs: [
            `Você pode solicitar confirmação do tratamento, acesso, correção, anonimização, bloqueio ou eliminação quando cabível, portabilidade nos termos da regulamentação, informação sobre compartilhamentos, oposição, revisão de decisões automatizadas e revogação de consentimento quando essa for a base utilizada. Envie o pedido para ${seller.supportEmail || 'o canal de suporte informado no site'}. Para evitar fraude, poderemos confirmar sua identidade e a titularidade da compra.`,
          ],
        },
        {
          title: 'Transferência internacional e incidentes',
          paragraphs: ['Cloudflare, Asaas e fornecedores técnicos podem processar dados em infraestrutura localizada fora do Brasil, observadas as regras aplicáveis de transferência internacional e medidas contratuais e técnicas adequadas. Incidentes que possam causar risco ou dano relevante serão avaliados e comunicados à ANPD e aos titulares quando exigido.'],
        },
        {
          title: 'Cookies e métricas',
          paragraphs: [
            'O site registra uma visualização técnica para métricas administrativas e usa um cookie seguro e temporário para devolver o comprador à sua licença após o pagamento. Também pode usar cookies necessários à sessão do painel. Não usamos o aplicativo para criar perfil publicitário comportamental.',
          ],
        },
        {
          title: 'Alterações desta política',
          paragraphs: [
            'Podemos atualizar esta política quando o produto, fornecedores ou requisitos legais mudarem. A data no topo identifica a versão vigente. Mudanças materiais serão comunicadas por meio razoável quando necessário.',
          ],
        },
      ]}
    />
  );
}
