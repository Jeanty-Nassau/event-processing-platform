# ADR 002: At-least-once delivery

## Status
Accepted

## Context
A consumer can fail after committing a side effect but before committing its broker offset.

## Decision
Assume duplicate delivery and make processing idempotent using the event identifier.

## Alternatives considered
Claiming exactly-once processing or deduplicating in process memory.

## Consequences
The design is explicit about redelivery and failure windows. Database uniqueness protects the demonstrated side effect. Exactly-once semantics across arbitrary external systems are not claimed.
