# Reliability model

## Delivery guarantee

The platform deliberately implements **at-least-once processing with idempotent side effects**.

Kafka may redeliver a record after a crash or rebalance. The processor therefore never assumes a record is unique. A PostgreSQL primary key on `processed_events.event_id` is the business idempotency boundary.

## Critical failure window

1. The processor consumes event E123.
2. PostgreSQL commits the processed result.
3. The process crashes before the Kafka offset commit.
4. Kafka later redelivers E123.
5. `INSERT ... ON CONFLICT DO NOTHING` returns no inserted row.
6. The processor treats E123 as an already-completed duplicate and safely commits the offset.

This avoids a misleading "exactly once" claim while still protecting the side effect.

## Failure windows

| Failure point | Expected behavior |
|---|---|
| API fails before Kafka acknowledgement | no 202 response is returned |
| API fails after Kafka acknowledgement | Kafka retains the accepted event |
| Processor fails before PostgreSQL commit | source offset is not committed; event remains recoverable |
| Processor fails after PostgreSQL commit but before offset commit | event may be redelivered; database idempotency suppresses duplicate side effects |
| PostgreSQL is unavailable | processor seeks back and does not commit the Kafka offset |
| Retryable application failure | retry row is made durable before the source offset is committed |
| Retry dispatcher cannot republish | retry row remains in PostgreSQL |
| Permanent invalid event | DLQ publish must succeed before the source offset is committed |
| Retry budget exhausted | event is sent to the DLQ |
| DLQ publish fails | source offset is not committed |

## Why database outage is different from an application retry

The retry scheduler itself lives in PostgreSQL. If PostgreSQL is unavailable, the processor cannot truthfully claim that a retry has been durably scheduled. It therefore leaves the Kafka record uncommitted instead of losing it.

## Ordering

Kafka ordering is guaranteed only within a partition. `SubjectId` is the message key, so events for one subject are consistently routed together while different subjects can execute in parallel.

A hot subject can create a hot partition; this is an explicit trade-off.

## Graceful shutdown

The consumer stops polling when cancellation is requested and calls `Close()` so it leaves the group cleanly. Work is only committed after a safe terminal state: processed, duplicate, durably scheduled retry, or successfully dead-lettered.
