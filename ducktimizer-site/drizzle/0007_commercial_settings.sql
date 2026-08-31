CREATE TABLE `settings` (
  `key` text PRIMARY KEY NOT NULL,
  `value` text NOT NULL,
  `updated_at` integer NOT NULL
);
CREATE TABLE `admin_audit` (
  `id` text PRIMARY KEY NOT NULL,
  `action` text NOT NULL,
  `detail` text,
  `created_at` integer NOT NULL
);
CREATE INDEX `idx_admin_audit_created` ON `admin_audit` (`created_at`);
ALTER TABLE `orders` ADD `amount_cents` integer NOT NULL DEFAULT 1750;
ALTER TABLE `licenses` ADD `enabled` integer NOT NULL DEFAULT 1;
