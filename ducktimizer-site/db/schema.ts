import {
  index,
  integer,
  sqliteTable,
  text,
  uniqueIndex,
} from 'drizzle-orm/sqlite-core';

export const orders = sqliteTable(
  'orders',
  {
    id: text('id').primaryKey(),
    name: text('name').notNull(),
    email: text('email').notNull(),
    deviceId: text('device_id').notNull(),
    status: text('status', {
      enum: ['pending', 'paid', 'licensed', 'cancelled'],
    })
      .notNull()
      .default('pending'),
    asaasCheckoutId: text('asaas_checkout_id'),
    checkoutUrl: text('checkout_url'),
    legalAcceptedAt: integer('legal_accepted_at', { mode: 'timestamp' }),
    paidAt: integer('paid_at', { mode: 'timestamp' }),
    createdAt: integer('created_at', { mode: 'timestamp' }).notNull(),
    updatedAt: integer('updated_at', { mode: 'timestamp' }).notNull(),
  },
  (table) => [
    uniqueIndex('idx_orders_device_email').on(table.deviceId, table.email),
    uniqueIndex('idx_orders_asaas_checkout').on(table.asaasCheckoutId),
  ],
);

export const webhookEvents = sqliteTable(
  'webhook_events',
  {
    id: text('id').primaryKey(),
    event: text('event').notNull(),
    checkoutId: text('checkout_id'),
    receivedAt: integer('received_at', { mode: 'timestamp' }).notNull(),
  },
  (table) => [index('idx_webhook_checkout').on(table.checkoutId)],
);

export const dailyMetrics = sqliteTable('daily_metrics', {
  day: text('day').primaryKey(),
  pageViews: integer('page_views').notNull().default(0),
});

export const licenses = sqliteTable(
  'licenses',
  {
    id: text('id').primaryKey(),
    orderId: text('order_id')
      .notNull()
      .references(() => orders.id),
    deviceId: text('device_id').notNull(),
    customer: text('customer').notNull(),
    licenseToken: text('license_token').notNull(),
    expiresAt: integer('expires_at', { mode: 'timestamp' }).notNull(),
    createdAt: integer('created_at', { mode: 'timestamp' }).notNull(),
  },
  (table) => [
    uniqueIndex('idx_licenses_order').on(table.orderId),
    uniqueIndex('idx_licenses_token').on(table.licenseToken),
  ],
);

export const deliveries = sqliteTable(
  'deliveries',
  {
    id: text('id').primaryKey(),
    orderId: text('order_id')
      .notNull()
      .references(() => orders.id),
    tokenHash: text('token_hash').notNull(),
    expiresAt: integer('expires_at', { mode: 'timestamp' }).notNull(),
    downloadCount: integer('download_count').notNull().default(0),
    maxDownloads: integer('max_downloads').notNull().default(5),
    emailMessageId: text('email_message_id'),
    deliveredAt: integer('delivered_at', { mode: 'timestamp' }),
    lastDownloadedAt: integer('last_downloaded_at', { mode: 'timestamp' }),
    lastError: text('last_error'),
    createdAt: integer('created_at', { mode: 'timestamp' }).notNull(),
  },
  (table) => [
    uniqueIndex('idx_deliveries_order').on(table.orderId),
    uniqueIndex('idx_deliveries_token_hash').on(table.tokenHash),
  ],
);

export const rateLimits = sqliteTable(
  'rate_limits',
  {
    bucket: text('bucket').notNull(),
    keyHash: text('key_hash').notNull(),
    windowStart: integer('window_start').notNull(),
    requestCount: integer('request_count').notNull().default(1),
  },
  (table) => [
    uniqueIndex('idx_rate_limits_bucket_key').on(table.bucket, table.keyHash),
    index('idx_rate_limits_window').on(table.windowStart),
  ],
);
