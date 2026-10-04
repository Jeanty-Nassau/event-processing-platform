#!/usr/bin/env bash
set -euo pipefail

echo "This event fails on the original delivery and retry 1, then succeeds on retry 2."
FAIL_UNTIL_ATTEMPT=2 ./scripts/send-event.sh
