#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
SECRET="${EVENT_SIGNING_SECRET:-local-development-secret}"
EVENT_ID="${EVENT_ID:-$(uuidgen | tr '[:upper:]' '[:lower:]')}"
CORRELATION_ID="${CORRELATION_ID:-$(uuidgen | tr '[:upper:]' '[:lower:]')}"
SUBJECT_ID="${SUBJECT_ID:-subject-demo}"
VALUE="${VALUE:-42}"
FAIL_UNTIL_ATTEMPT="${FAIL_UNTIL_ATTEMPT:-0}"
OCCURRED_AT="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

PAYLOAD="$(cat <<JSON
{"eventId":"$EVENT_ID","eventType":"demo.work.requested","source":"sample-producer","subjectId":"$SUBJECT_ID","schemaVersion":1,"occurredAt":"$OCCURRED_AT","correlationId":"$CORRELATION_ID","payload":{"operation":"transform","value":$VALUE,"failUntilAttempt":$FAIL_UNTIL_ATTEMPT}}
JSON
)"

DIGEST="$(printf '%s' "$PAYLOAD" | openssl dgst -sha256 -hmac "$SECRET" -hex | awk '{print $NF}')"

curl --fail-with-body -sS \
  -X POST "$BASE_URL/api/v1/events" \
  -H "Content-Type: application/json" \
  -H "X-Signature: sha256=$DIGEST" \
  --data-binary "$PAYLOAD"

echo
echo "eventId=$EVENT_ID"
