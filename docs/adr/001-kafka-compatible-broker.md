# ADR 001: Kafka-compatible broker

## Status
Accepted

## Context
The project needs real partition, offset, consumer-group, replay, and producer-acknowledgement semantics while remaining inexpensive to run locally.

## Decision
Use Redpanda in Docker Compose as the local broker and use the standard Kafka protocol through Confluent.Kafka in application code.

## Alternatives considered
RabbitMQ, Redis streams, a process-local queue, and a traditional multi-container Kafka distribution.

## Consequences
Kafka concepts remain directly relevant to production systems while local startup stays comparatively lightweight. The project does not claim Redpanda and Apache Kafka are operationally identical products.
