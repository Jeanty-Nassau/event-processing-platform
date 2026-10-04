# Testing strategy

The test strategy focuses on behavior and system boundaries rather than maximizing coverage percentage.

## Unit tests

Unit tests cover:

- event-envelope validation,
- HMAC validation,
- payload boundary behavior,
- deterministic retry backoff and jitter bounds.

## Integration tests

Integration tests use real disposable infrastructure through Testcontainers:

- Kafka round-trip verifies a keyed message survives a real broker path.
- PostgreSQL idempotency verifies the same `EventId` creates one logical row using the same `ON CONFLICT DO NOTHING` mechanism used by the processor.

Run:

```bash
dotnet test tests/EventPlatform.IntegrationTests/EventPlatform.IntegrationTests.csproj
```

Docker must be available.

## End-to-end smoke test

The smoke test boots the Compose stack, posts a signed HTTP event, and polls PostgreSQL until the processed row appears:

```bash
docker compose up -d --build
./scripts/smoke-test.sh
```

## Failure demonstrations

The scripts directory contains repeatable duplicate, retry, DLQ, and database-outage scenarios. These are intended to make the reliability claims inspectable rather than purely documentary.

## What is deliberately not mocked

Kafka and PostgreSQL correctness are not inferred from mocks in integration tests. Mocks may still be appropriate for narrow pure application behavior, but external-system semantics are tested against real containerized services.
