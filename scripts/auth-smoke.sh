#!/usr/bin/env bash
set -euo pipefail

KEYCLOAK_BASE="${KEYCLOAK_BASE:-http://localhost:8180}"
GATEWAY_BASE="${GATEWAY_BASE:-http://localhost:8080}"
API_BASE="${API_BASE:-$GATEWAY_BASE/api}"
REALM="distributed-commerce"
CLIENT_ID="commerce-cli"
DEMO_PASSWORD="local-demo-only"

wait_for() {
  local name="$1"
  local url="$2"

  for _ in $(seq 1 60); do
    if curl -fsS "$url" >/dev/null 2>&1; then
      echo "$name is ready"
      return 0
    fi
    sleep 2
  done

  echo "$name did not become ready: $url" >&2
  return 1
}

token_for() {
  local username="$1"

  curl -fsS     -X POST "$KEYCLOAK_BASE/realms/$REALM/protocol/openid-connect/token"     -H "Content-Type: application/x-www-form-urlencoded"     --data-urlencode "grant_type=password"     --data-urlencode "client_id=$CLIENT_ID"     --data-urlencode "username=$username"     --data-urlencode "password=$DEMO_PASSWORD" |
    jq -er '.access_token'
}

wait_for "Keycloak" "$KEYCLOAK_BASE/realms/$REALM/.well-known/openid-configuration"
wait_for "API Gateway" "$GATEWAY_BASE/health"

payload='{"items":[{"sku":"AUTH-SMOKE","quantity":1,"unitPrice":99.90}]}'

unauthenticated_status="$(
  curl -sS -o /dev/null -w '%{http_code}'     -X POST "$API_BASE/orders"     -H "Content-Type: application/json"     -d "$payload"
)"

test "$unauthenticated_status" = "401"

customer_token="$(token_for demo-customer)"
other_customer_token="$(token_for demo-customer-2)"
admin_token="$(token_for demo-admin)"

create_response="$(
  curl -fsS     -X POST "$API_BASE/orders"     -H "Authorization: Bearer $customer_token"     -H "Content-Type: application/json"     -d "$payload"
)"

order_id="$(printf '%s' "$create_response" | jq -er '.id')"

curl -fsS   "$API_BASE/orders/$order_id"   -H "Authorization: Bearer $customer_token" >/dev/null

other_customer_status="$(
  curl -sS -o /dev/null -w '%{http_code}'     "$API_BASE/orders/$order_id"     -H "Authorization: Bearer $other_customer_token"
)"

test "$other_customer_status" = "403"

curl -fsS   "$API_BASE/orders/$order_id"   -H "Authorization: Bearer $admin_token" >/dev/null

echo "Gateway authentication/authorization smoke test passed."
