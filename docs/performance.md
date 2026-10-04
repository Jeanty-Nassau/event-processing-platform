# Performance testing

The repository includes a k6 ingestion workload at `benchmarks/k6/ingestion.js`.

## Run

```bash
docker compose up -d --build
k6 run benchmarks/k6/ingestion.js
```

Configuration is supplied through environment variables:

- `BASE_URL`
- `EVENT_SIGNING_SECRET`
- `VUS`
- `DURATION`
- `SUBJECT_COUNT`

Example:

```bash
VUS=50 DURATION=60s SUBJECT_COUNT=5000 k6 run benchmarks/k6/ingestion.js
```

## Metrics to record

For a published benchmark, capture at minimum:

- accepted requests per second,
- HTTP error rate,
- p50, p95, and p99 HTTP latency,
- processor throughput,
- retry/DLQ counts,
- consumer lag if measured,
- end-to-end drain time after load stops.

## Required environment metadata

Every committed benchmark result must record:

- date,
- Git commit SHA,
- operating system,
- CPU model,
- logical CPU count,
- RAM,
- Docker version,
- .NET version,
- Redpanda version,
- PostgreSQL version,
- partition count,
- processor instance count,
- payload size,
- test duration,
- k6 configuration.

## Published results

No verified benchmark result is currently published.

This is intentional. The project avoids throughput claims until the workload is run in a documented environment. Results belong under `benchmarks/results/` and should not be generalized beyond the recorded hardware and configuration.
