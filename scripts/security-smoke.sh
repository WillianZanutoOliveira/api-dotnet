#!/usr/bin/env bash
set -euo pipefail

KEYCLOAK_BASE="${KEYCLOAK_BASE:-http://localhost:8180}"
GATEWAY_BASE="${GATEWAY_BASE:-http://localhost:8080}"
API_BASE="${API_BASE:-$GATEWAY_BASE/api}"
REALM="distributed-commerce"
CLIENT_ID="commerce-cli"
DEMO_PASSWORD="local-demo-only"

token_for() {
  local username="$1"

  curl -fsS \
    -X POST "$KEYCLOAK_BASE/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=$CLIENT_ID" \
    --data-urlencode "username=$username" \
    --data-urlencode "password=$DEMO_PASSWORD" |
    jq -er '.access_token'
}

token="$(token_for demo-customer)"

tampered_token="${token%?}x"
if [ "$tampered_token" = "$token" ]; then
  tampered_token="${token%?}y"
fi

tampered_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    "$API_BASE/me" \
    -H "Authorization: Bearer $tampered_token"
)"

test "$tampered_status" = "401"

trace_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    -X TRACE "$GATEWAY_BASE/api/orders"
)"

test "$trace_status" = "405"

headers_file="$(mktemp)"
trap 'rm -f "$headers_file" /tmp/distributed-commerce-oversized.json' EXIT

curl -fsS -D "$headers_file" -o /dev/null \
  "$API_BASE/me" \
  -H "Authorization: Bearer $token"

grep -qi '^X-Content-Type-Options: nosniff' "$headers_file"
grep -qi '^X-Frame-Options: DENY' "$headers_file"
grep -qi '^Referrer-Policy: no-referrer' "$headers_file"
grep -qi '^Cross-Origin-Resource-Policy: same-origin' "$headers_file"
grep -qi '^Content-Security-Policy:' "$headers_file"
grep -qi '^Cache-Control: no-store' "$headers_file"

if grep -qi '^Server:' "$headers_file"; then
  echo "Server header leaked implementation details." >&2
  exit 1
fi

mass_assignment_payload='{"customerId":"attacker-controlled","items":[{"sku":"SECURITY","quantity":1,"unitPrice":10.00}]}'
mass_assignment_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "$API_BASE/orders" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$mass_assignment_payload"
)"

test "$mass_assignment_status" = "400"

out_of_range_numeric_payload='{"items":[{"sku":"SECURITY","quantity":2147483648,"unitPrice":10.00}]}'
out_of_range_numeric_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "$API_BASE/orders" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$out_of_range_numeric_payload"
)"

test "$out_of_range_numeric_status" = "400"

too_many_items="$(
  jq -nc '{
    items: [range(0;101) | {sku:("SKU-" + tostring), quantity:1, unitPrice:1.0}]
  }'
)"

bounded_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "$API_BASE/orders" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$too_many_items"
)"

test "$bounded_status" = "400"

python3 - <<'PY'
from pathlib import Path
payload = '{"items":[{"sku":"' + ('A' * 1_100_000) + '","quantity":1,"unitPrice":1.0}]}'
Path('/tmp/distributed-commerce-oversized.json').write_text(payload, encoding='utf-8')
PY

oversized_status="$(
  curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "$API_BASE/orders" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    --data-binary @/tmp/distributed-commerce-oversized.json
)"

test "$oversized_status" = "413"

rate_limited="false"
for _ in $(seq 1 80); do
  status="$(
    curl -sS -o /dev/null -w '%{http_code}' \
      "$API_BASE/me" \
      -H "Authorization: Bearer $token"
  )"

  if [ "$status" = "429" ]; then
    rate_limited="true"
    break
  fi

  test "$status" = "200"
done

test "$rate_limited" = "true"

echo "HTTP hardening, strict input handling, JWT rejection and rate limiting passed."
