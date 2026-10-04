#!/usr/bin/env bash
set -euo pipefail

echo "This event remains transiently failing beyond the configured retry budget and should reach the DLQ."
FAIL_UNTIL_ATTEMPT=99 ./scripts/send-event.sh
