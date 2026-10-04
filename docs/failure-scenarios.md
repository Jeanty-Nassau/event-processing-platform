# Failure scenarios

Start the system first:

```bash
docker compose up -d --build
```

## Duplicate delivery

```bash
./scripts/simulate-duplicate.sh
```

Expected: two accepted broker records may exist, but PostgreSQL contains one logical `processed_events` row for the shared `EventId`. The duplicate metric increments.

## Delayed retry then success

```bash
./scripts/simulate-retry.sh
```

Expected: the original delivery fails, a durable retry is scheduled, the retry dispatcher republishes it, and processing eventually succeeds.

## Retry exhaustion

```bash
./scripts/simulate-dlq.sh
```

Expected: retries progress through the configured budget and the final failed event is published to `event-platform.events.dlq.v1`.

Inspect the DLQ:

```bash
docker compose exec redpanda rpk topic consume event-platform.events.dlq.v1 -n 1 -X brokers=redpanda:29092
```

## PostgreSQL outage

```bash
./scripts/simulate-db-outage.sh
./scripts/send-event.sh
docker compose start postgres
```

Expected: the processor does not commit the record while it cannot durably handle it. After PostgreSQL recovers, the record remains recoverable.

## Graceful processor shutdown

```bash
docker compose stop processor
docker compose start processor
```

The consumer closes cleanly and rejoins the group on restart.

## Abrupt processor crash

```bash
docker compose kill -s SIGKILL processor
docker compose up -d processor
```

Any record whose offset was not committed remains eligible for redelivery.

## Scale processors

```bash
docker compose up -d --scale processor=2
```

Both instances use `event-platform-processors-v1`; Kafka distributes partition ownership between group members.
