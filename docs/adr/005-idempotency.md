# ADR 005: Database-enforced idempotency

## Status
Accepted

## Context
A check-then-insert sequence races under concurrent duplicate delivery.

## Decision
Use `processed_events.event_id` as a primary key and `INSERT ... ON CONFLICT DO NOTHING RETURNING event_id`.

## Alternatives considered
In-memory caches and SELECT-before-INSERT.

## Consequences
Correctness is enforced at the durable side-effect boundary rather than in one process instance.
