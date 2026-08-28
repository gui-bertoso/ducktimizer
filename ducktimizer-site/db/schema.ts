import { integer, sqliteTable, text, uniqueIndex } from 'drizzle-orm/sqlite-core';

export const orders = sqliteTable('orders', {
  id: text('id').primaryKey(), name: text('name').notNull(), email: text('email').notNull(),
  deviceId: text('device_id').notNull(),
  status: text('status', { enum: ['pending', 'paid', 'licensed', 'cancelled'] }).notNull().default('pending'),
  createdAt: integer('created_at', { mode: 'timestamp' }).notNull(),
  updatedAt: integer('updated_at', { mode: 'timestamp' }).notNull(),
}, (table) => [uniqueIndex('idx_orders_device_email').on(table.deviceId, table.email)]);

export const licenses = sqliteTable('licenses', {
  id: text('id').primaryKey(), orderId: text('order_id').notNull().references(() => orders.id),
  deviceId: text('device_id').notNull(), customer: text('customer').notNull(),
  licenseToken: text('license_token').notNull(), expiresAt: integer('expires_at', { mode: 'timestamp' }).notNull(),
  createdAt: integer('created_at', { mode: 'timestamp' }).notNull(),
}, (table) => [uniqueIndex('idx_licenses_order').on(table.orderId), uniqueIndex('idx_licenses_token').on(table.licenseToken)]);
