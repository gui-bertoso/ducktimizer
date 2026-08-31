ALTER TABLE `licenses` ADD `activation_key_hash` text;
ALTER TABLE `licenses` ADD `activated_token` text;
ALTER TABLE `licenses` ADD `activated_at` integer;
CREATE UNIQUE INDEX `idx_licenses_activation_key` ON `licenses` (`activation_key_hash`);
