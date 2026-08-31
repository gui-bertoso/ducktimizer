import type { Metadata } from 'next';
import { LegalPage } from '@/components/legal-page';
import { getSettings } from '@/lib/settings';

export const metadata: Metadata = {
  title: 'Termos de Uso — Ducktimizer',
  description: 'Termos de uso, licença e ciência de risco do Ducktimizer Pro.',
};

export default async function TermsPage() {
  const seller = await getSettings();
  return (
    <LegalPage
      eyebrow="Documento legal"
      title="Termos de Uso"
      updated="31 de agosto de 2026"
      intro="Ao comprar, instalar, ativar ou usar o Ducktimizer Pro, você declara que leu e concorda com estes termos. O software altera configurações administrativas do Windows e deve ser usado somente em computadores próprios ou sob sua autorização."
      sections={[
        {
          title: 'Fornecedor e atendimento',
          paragraphs: [
            `${seller.businessName || 'Fornecedor ainda não cadastrado'} — CPF/CNPJ: ${seller.taxId || 'não informado'}. Endereço: ${seller.businessAddress || 'não informado'}. Atendimento eletrônico: ${seller.supportEmail || 'não informado'}.`,
            'Esse canal recebe dúvidas, reclamações, pedidos de suporte e solicitações de arrependimento. O atendimento não elimina o direito de procurar órgãos de defesa do consumidor.',
          ],
        },
        {
          title: 'Produto e finalidade',
          paragraphs: [
            'O Ducktimizer é uma ferramenta de configuração para Windows 10 e 11. Ele reúne ajustes visuais, de energia, jogos, privacidade, pesquisa, aplicativos e execução em segundo plano. Resultados variam conforme hardware, drivers, programas, versão do Windows e condições de uso. Não prometemos ganho específico de FPS, latência ou consumo.',
          ],
        },
        {
          title: 'Privilégios e alterações',
          paragraphs: [
            'O aplicativo solicita privilégios de administrador. Ao autorizar o UAC e confirmar uma operação, você permite alterações no Registro, políticas locais, plano de energia e, quando escolhido, pacotes de aplicativos. Algumas mudanças exigem reinicialização e podem ser reaplicadas ou alteradas por atualizações do Windows.',
          ],
        },
        {
          title: 'Presets, modo seco e Debloat',
          paragraphs: [
            'Presets são seleções prontas, mas o usuário deve revisar as chaves antes de aplicar. O modo Windows seco pode remover transparência, animações, suavização de fontes e outros efeitos visuais. O Debloat pode desinstalar aplicativos selecionados e remover seu provisionamento. Essa remoção pode ser irreversível: o backup registra configurações, mas não garante a reinstalação de componentes e aplicativos removidos.',
            'Edge, Microsoft Store, Windows Update, Microsoft Defender, Firewall e serviços considerados críticos não são removidos pelo padrão do produto. A reinstalação de aplicativos removidos pode depender da Microsoft Store ou de arquivos ainda presentes no sistema.',
          ],
        },
        {
          title: 'Backup e restauração',
          paragraphs: [
            'Antes de alterar configurações, o Ducktimizer tenta criar um ponto de restauração e mantém um backup granular local. Se o ponto não estiver disponível, o programa pede confirmação antes de continuar. A função Restaurar tenta voltar aos valores registrados, mas não substitui um backup completo do computador e não garante a recuperação de componentes removidos por fatores externos.',
          ],
        },
        {
          title: 'Licença vinculada ao dispositivo',
          paragraphs: [
            'A compra concede uma licença pessoal, limitada, não exclusiva e intransferível para um computador. Na primeira ativação, a chave é vinculada automaticamente ao Device ID daquele computador e não poderá ser ativada em outro. Troca relevante de hardware, formatação ou substituição do equipamento pode alterar o identificador e exigir análise de reativação pelo suporte.',
            'O aplicativo realiza validação periódica da situação da licença e mantém uma autorização assinada com validade de até 7 dias para tolerar indisponibilidade temporária de internet. Licenças suspensas por fraude, estorno confirmado ou violação contratual deixam de renovar essa autorização, preservados os direitos do consumidor e o atendimento de suporte.',
          ],
          items: [
            'É proibido compartilhar, revender ou publicar a licença.',
            'É proibido contornar a ativação, adulterar a assinatura ou redistribuir o executável.',
            'A licença não transfere propriedade do código-fonte, marca ou identidade visual.',
          ],
        },
        {
          title: 'Entrega digital',
          paragraphs: [
            'Após a confirmação do pagamento, o site libera o instalador, a chave de ativação permanente e a confirmação da compra no mesmo navegador usado para iniciar o checkout. O link do instalador é individual, expira em 7 dias e permite até 5 downloads. Se fechar a página, o comprador pode informar novamente o mesmo e-mail e o código do pedido para recuperar a compra. A confirmação de entrega digital não substitui documento fiscal quando ele for legalmente exigível.',
          ],
        },
        {
          title: 'Responsabilidade do usuário',
          paragraphs: [
            'Você deve fechar trabalhos importantes, revisar os grupos selecionados e manter cópias de arquivos relevantes. O uso em equipamento corporativo depende da autorização do responsável. O usuário responde por escolhas agressivas e por incompatibilidades decorrentes de configurações, drivers ou software de terceiros.',
          ],
        },
        {
          title: 'Disponibilidade, atualizações e suporte',
          paragraphs: [
            'Podemos corrigir, substituir ou descontinuar recursos para acompanhar mudanças do Windows. A oferta comercial indicará quais versões e atualizações estão incluídas. O suporte de ativação não inclui manutenção geral do computador, recuperação de dados ou garantia de desempenho em jogos.',
          ],
        },
        {
          title: 'Diagnóstico técnico',
          paragraphs: [
            'Para melhorar estabilidade e corrigir falhas, o aplicativo pode enviar relatórios técnicos resumidos de erros e encerramentos inesperados. Esses relatórios podem conter o tipo do erro, contexto, mensagem técnica e versão do aplicativo, mas não incluem arquivos pessoais ou o conteúdo dos seus documentos. O tratamento segue a Política de Privacidade e é limitado a segurança, suporte e evolução do produto.',
          ],
        },
        {
          title: 'Cancelamento e legislação',
          paragraphs: [
            `O consumidor pode exercer o direito de arrependimento em até 7 dias contados da contratação ou do recebimento do produto digital, conforme aplicável, pelo e-mail ${seller.supportEmail || 'de suporte informado no site'}. Os valores pagos serão devolvidos na forma e nos prazos legalmente exigidos. A ativação ou o download não eliminam direitos assegurados por lei.`,
            'Estes termos não excluem nem limitam responsabilidade que não possa ser afastada pelo Código de Defesa do Consumidor. Eventuais limitações técnicas, variação de desempenho e riscos informados não autorizam produto defeituoso, publicidade enganosa ou descumprimento da oferta.',
          ],
        },
        {
          title: 'Propriedade intelectual e uso legítimo',
          paragraphs: ['O software é licenciado, não vendido. A proteção da licença não impede direitos legalmente assegurados, interoperabilidade permitida ou análise autorizada. É vedada a distribuição não autorizada, adulteração para fraude, revenda de chaves ou contorno do controle de ativação.'],
        },
        {
          title: 'Lei aplicável e solução de conflitos',
          paragraphs: ['Aplica-se a legislação brasileira. Fica preservado o foro legalmente competente do consumidor e o acesso a Procon, consumidor.gov.br e Poder Judiciário. Nenhuma cláusula destes termos impõe renúncia antecipada a direitos do consumidor.'],
        },
      ]}
    />
  );
}
