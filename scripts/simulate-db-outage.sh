#!/usr/bin/env bash
set -euo pipefail

echo "Stopping PostgreSQL. The processor must not commit messages it cannot durably handle."
docker compose stop postgres
echo "Publish an event now with ./scripts/send-event.sh, then restore PostgreSQL:"
echo "  docker compose start postgres"
echo "The uncommitted Kafka record should remain recoverable."
