-- V002: Add update history tracking
CREATE TABLE IF NOT EXISTS update_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    version TEXT NOT NULL,
    previous_version TEXT,
    update_type TEXT NOT NULL CHECK (update_type IN ('full', 'patch', 'rollback')),
    status TEXT NOT NULL CHECK (status IN ('success', 'failed', 'pending')),
    started_at TEXT NOT NULL DEFAULT (datetime('now')),
    completed_at TEXT,
    error_message TEXT
);

-- Add columns to service_state for update tracking
ALTER TABLE service_state ADD COLUMN category TEXT DEFAULT 'general';

CREATE INDEX IF NOT EXISTS idx_update_history_version ON update_history(version);
