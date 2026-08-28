ALTER TABLE `orders` ADD `asaas_checkout_id` text;
--> statement-breakpoint
ALTER TABLE `orders` ADD `checkout_url` text;
--> statement-breakpoint
ALTER TABLE `orders` ADD `paid_at` integer;
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_orders_asaas_checkout` ON `orders` (`asaas_checkout_id`);
--> statement-breakpoint
CREATE TABLE `webhook_events` (
  `id` text PRIMARY KEY NOT NULL,
  `event` text NOT NULL,
  `checkout_id` text,
  `received_at` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `idx_webhook_checkout` ON `webhook_events` (`checkout_id`);
