declare namespace Cloudflare {
  interface Env {
    DB: D1Database;
    ASAAS_API_KEY: string;
    ASAAS_WEBHOOK_TOKEN: string;
    ASAAS_ENVIRONMENT?: 'sandbox' | 'production';
    PUBLIC_SITE_URL?: string;
    DUCKTIMIZER_PRICE?: string;
  }
}
