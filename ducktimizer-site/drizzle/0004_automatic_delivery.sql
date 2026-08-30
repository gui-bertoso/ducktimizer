CREATE TABLE `deliveries` (
  `id` text PRIMARY KEY NOT NULL,
  `order_id` text NOT NULL,
  `token_hash` text NOT NULL,
  `expires_at` integer NOT NULL,
  `download_count` integer DEFAULT 0 NOT NULL,
  `max_downloads` integer DEFAULT 5 NOT NULL,
  `email_message_id` text,
  `delivered_at` integer,
  `last_downloaded_at` integer,
  `last_error` text,
  `created_at` integer NOT NULL,
  FOREIGN KEY (`order_id`) REFERENCES `orders`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_deliveries_order` ON `deliveries` (`order_id`);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_deliveries_token_hash` ON `deliveries` (`token_hash`);
