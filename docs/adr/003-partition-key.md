# ADR 003: SubjectId partition key

## Status
Accepted

## Context
The processor should preserve ordering for related events without serializing all work.

## Decision
Use `SubjectId` as the Kafka record key.

## Alternatives considered
Random partitioning and EventId keying.

## Consequences
Events for the same subject share partition ordering while different subjects can be processed in parallel. A very hot subject can create a hot partition.
