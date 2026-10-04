# ADR 006: Explicit schema versioning

## Status
Accepted

## Context
The project currently has one controlled public event envelope.

## Decision
Carry `schemaVersion` in the envelope and accept version 1 explicitly. Unsupported versions are permanent failures.

## Alternatives considered
Adding Schema Registry immediately.

## Consequences
Version behavior is visible in code and tests without introducing registry infrastructure before multiple schemas justify it.
