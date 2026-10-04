#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
EVENT_ID="$(uuidgen | tr '[:upper:]' '[:lower:]')"

echo "Waiting for API readiness..."
for _ in $(seq 1 60); do
  if curl -fsS "$BASE_URL/health/ready" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

curl -fsS "$BASE_URL/health/ready" >/dev/null

echo "Publishing smoke event $EVENT_ID"
EVENT_ID="$EVENT_ID" ./scripts/send-event.sh >/dev/null

echo "Waiting for PostgreSQL processing result..."
for _ in $(seq 1 30); do
  COUNT="$(docker compose exec -T postgres psql -U eventplatform -d eventplatform -tAc "SELECT COUNT(*) FROM processed_events WHERE event_id = '$EVENT_ID';")"
  if [ "$COUNT" = "1" ]; then
    echo "Smoke test passed: event processed exactly once."
    exit 0
  fi
  sleep 1
done

echo "Smoke test failed: event was not observed in processed_events." >&2
exit 1
