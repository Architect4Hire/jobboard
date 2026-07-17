#!/usr/bin/env bash
# PreToolUse guard: deny writes containing secret-shaped strings. exit 2 = deny.
# Everything here should come from Aspire-injected config, never a literal in source.
set -euo pipefail
payload="$(cat)"
patterns='(sk-[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|-----BEGIN [A-Z ]*PRIVATE KEY-----|password[[:space:]]*=[[:space:]]*["'\''][^"'\'' ]{6,}|(postgres|redis)://[^:@/]+:[^@/]+@|Endpoint=sb://[^;]+;SharedAccessKey[A-Za-z]*=[^;]+)'
if printf '%s' "$payload" | grep -Eiq "$patterns"; then
 echo "secret-guard: blocked a write with a secret-shaped string (DB/Redis/Service Bus credential or key). Use Aspire-injected config, not literals." >&2
 exit 2
fi
exit 0
