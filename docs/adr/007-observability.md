# ADR 007: OpenTelemetry observability

## Status
Accepted

## Context
A distributed event system is difficult to reason about from logs alone.

## Decision
Instrument application activities and metrics with OpenTelemetry and export through an OTLP collector to Prometheus, Tempo, and Grafana locally.

## Alternatives considered
Vendor-specific SDKs and logs-only observability.

## Consequences
Instrumentation remains vendor-neutral. Local observability requires several optional containers, but application services are not coupled to those backends.
