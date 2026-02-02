-- LogForDev ClickHouse Schema

CREATE DATABASE IF NOT EXISTS logfordev;

USE logfordev;

-- Main logs table with MergeTree engine
CREATE TABLE IF NOT EXISTS logs (
    id UUID DEFAULT generateUUIDv4(),
    timestamp DateTime64(3) DEFAULT now64(3),
    level Enum8('Trace'=0, 'Debug'=1, 'Info'=2, 'Warning'=3, 'Error'=4, 'Fatal'=5),
    app_name LowCardinality(String),
    message String,
    metadata String DEFAULT '{}',
    trace_id String DEFAULT '',
    span_id String DEFAULT '',
    host LowCardinality(String) DEFAULT '',
    environment LowCardinality(String) DEFAULT 'production',
    created_at DateTime DEFAULT now()
)
ENGINE = MergeTree()
PARTITION BY toYYYYMM(timestamp)
ORDER BY (app_name, level, timestamp)
TTL timestamp + INTERVAL 30 DAY
SETTINGS index_granularity = 8192;

-- Index for faster searches
ALTER TABLE logs ADD INDEX idx_message message TYPE tokenbf_v1(32768, 3, 0) GRANULARITY 4;

-- API Keys table
CREATE TABLE IF NOT EXISTS api_keys (
    id UUID DEFAULT generateUUIDv4(),
    key_hash String,
    name String,
    app_name LowCardinality(String) DEFAULT '',
    is_active UInt8 DEFAULT 1,
    created_at DateTime DEFAULT now(),
    last_used_at DateTime DEFAULT now()
)
ENGINE = MergeTree()
ORDER BY (key_hash)
SETTINGS index_granularity = 8192;

-- Apps table for tracking registered applications
CREATE TABLE IF NOT EXISTS apps (
    id UUID DEFAULT generateUUIDv4(),
    name LowCardinality(String),
    description String DEFAULT '',
    created_at DateTime DEFAULT now(),
    last_log_at DateTime DEFAULT now(),
    log_count UInt64 DEFAULT 0
)
ENGINE = MergeTree()
ORDER BY (name)
SETTINGS index_granularity = 8192;

-- Materialized view for log statistics (per hour)
CREATE MATERIALIZED VIEW IF NOT EXISTS logs_hourly_stats
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMM(hour)
ORDER BY (app_name, level, hour)
AS SELECT
    toStartOfHour(timestamp) AS hour,
    app_name,
    level,
    count() AS log_count
FROM logs
GROUP BY hour, app_name, level;

-- Materialized view for daily statistics
CREATE MATERIALIZED VIEW IF NOT EXISTS logs_daily_stats
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMM(day)
ORDER BY (app_name, day)
AS SELECT
    toStartOfDay(timestamp) AS day,
    app_name,
    count() AS total_logs,
    countIf(level = 'Error') AS error_count,
    countIf(level = 'Warning') AS warning_count,
    countIf(level = 'Fatal') AS fatal_count
FROM logs
GROUP BY day, app_name;
