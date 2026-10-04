CREATE TABLE IF NOT EXISTS processed_events (
    event_id UUID PRIMARY KEY,
    event_type VARCHAR(150) NOT NULL,
    subject_id VARCHAR(200) NOT NULL,
    source VARCHAR(100) NOT NULL,
    schema_version INTEGER NOT NULL,
    correlation_id UUID NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    received_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL,
    status VARCHAR(50) NOT NULL,
    result JSONB NULL
);

CREATE INDEX IF NOT EXISTS idx_processed_events_processed_at
    ON processed_events(processed_at DESC);

CREATE TABLE IF NOT EXISTS retry_schedule (
    id UUID PRIMARY KEY,
    event_id UUID NOT NULL UNIQUE,
    original_topic VARCHAR(255) NOT NULL,
    original_partition INTEGER NOT NULL,
    original_offset BIGINT NOT NULL,
    event_key VARCHAR(255) NOT NULL,
    payload JSONB NOT NULL,
    attempt INTEGER NOT NULL CHECK (attempt > 0),
    next_attempt_at TIMESTAMPTZ NOT NULL,
    failure_category VARCHAR(100) NOT NULL,
    error_code VARCHAR(100) NULL,
    last_error TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_retry_schedule_due
    ON retry_schedule(next_attempt_at);
