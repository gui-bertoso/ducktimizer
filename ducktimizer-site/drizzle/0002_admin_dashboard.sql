CREATE TABLE `daily_metrics` (
  `day` text PRIMARY KEY NOT NULL,
  `page_views` integer DEFAULT 0 NOT NULL
);
