CREATE TABLE IF NOT EXISTS telemetry_errors (
  id TEXT PRIMARY KEY,
  kind TEXT NOT NULL,
  context TEXT NOT NULL,
  message TEXT NOT NULL,
  app_version TEXT,
  created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_telemetry_errors_created ON telemetry_errors(created_at);
