-- V001: Initial schema for InstallerService local state
CREATE TABLE IF NOT EXISTS service_state (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    key TEXT NOT NULL UNIQUE,
    value TEXT,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS message_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    direction TEXT NOT NULL CHECK (direction IN ('inbound', 'outbound')),
    endpoint TEXT NOT NULL,
    payload TEXT,
    status_code INTEGER,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS file_tracking (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_path TEXT NOT NULL,
    file_hash TEXT,
    file_size INTEGER,
    last_modified TEXT,
    synced_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_service_state_key ON service_state(key);
CREATE INDEX IF NOT EXISTS idx_message_log_created ON message_log(created_at);
CREATE INDEX IF NOT EXISTS idx_file_tracking_path ON file_tracking(file_path);
