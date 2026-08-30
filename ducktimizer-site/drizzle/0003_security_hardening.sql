ALTER TABLE `orders` ADD `legal_accepted_at` integer;
CREATE TABLE `rate_limits` (
  `bucket` text NOT NULL,
  `key_hash` text NOT NULL,
  `window_start` integer NOT NULL,
  `request_count` integer NOT NULL DEFAULT 1,
  PRIMARY KEY (`bucket`, `key_hash`)
);
CREATE INDEX `idx_rate_limits_window` ON `rate_limits` (`window_start`);
