# ADR 004: PostgreSQL-backed delayed retries

## Status
Accepted

## Context
Long sleeps inside a Kafka consumer block useful work and Kafka is not a general-purpose delayed scheduler.

## Decision
Persist retry intent in PostgreSQL with `next_attempt_at`. A separate worker claims due rows using `FOR UPDATE SKIP LOCKED` and republishes them.

## Alternatives considered
Sleeping in the consumer, immediate retry loops, and introducing a second queue technology.

## Consequences
Retries survive restarts, are inspectable, and can be claimed safely by multiple dispatcher instances. PostgreSQL becomes an explicit dependency of delayed retries.
