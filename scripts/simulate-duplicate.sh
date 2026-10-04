#!/usr/bin/env bash
set -euo pipefail

EVENT_ID="$(uuidgen | tr '[:upper:]' '[:lower:]')"
echo "Sending the same EventId twice: $EVENT_ID"
EVENT_ID="$EVENT_ID" ./scripts/send-event.sh
EVENT_ID="$EVENT_ID" ./scripts/send-event.sh
echo "Inspect processed_events and the duplicate metric; one logical row should exist."
