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

  curl -fsS     -X POST "$KEYCLOAK_BASE/realms/$REALM/protocol/openid-connect/token"     -H "Content-Type: application/x-www-form-urlencoded"     --data-urlencode "grant_type=password"     --data-urlencode "client_id=$CLIENT_ID"     --data-urlencode "username=$username"     --data-urlencode "password=$DEMO_PASSWORD" |
    jq -er '.access_token'
}

customer_token="$(token_for demo-customer)"
admin_token="$(token_for demo-admin)"

customer_access_status="$(
  curl -sS -o /dev/null -w '%{http_code}'     "$API_BASE/customers?page=1&pageSize=5"     -H "Authorization: Bearer $customer_token"
)"
test "$customer_access_status" = "403"

individual_payload='{
  "personType":"Individual",
  "document":"111.444.777-35",
  "displayName":"Cliente Smoke PF",
  "birthDate":"1990-01-01",
  "email":"pf-smoke@example.invalid",
  "phone":"(44) 99999-0000",
  "addresses":[{
    "type":"Primary",
    "isPrimary":true,
    "postalCode":"87000-000",
    "street":"Rua Fixture",
    "number":"100",
    "neighborhood":"Centro",
    "city":"Maringa",
    "state":"PR",
    "ibgeCityCode":"4115200"
  }]
}'

individual_response="$(
  curl -fsS     -X POST "$API_BASE/customers"     -H "Authorization: Bearer $admin_token"     -H "Content-Type: application/json"     -d "$individual_payload"
)"
individual_id="$(printf '%s' "$individual_response" | jq -er '.id')"
test "$(printf '%s' "$individual_response" | jq -r '.document')" = "11144477735"

curl -fsS   "$API_BASE/customers/$individual_id"   -H "Authorization: Bearer $admin_token" |
  jq -e '.displayName == "Cliente Smoke PF"' >/dev/null

curl -fsS   "$API_BASE/customers?document=11144477735&page=1&pageSize=10"   -H "Authorization: Bearer $admin_token" |
  jq -e --arg id "$individual_id" '.totalCount == 1 and any(.items[]; .id == $id)' >/dev/null

update_payload='{
  "personType":"Individual",
  "document":"11144477735",
  "displayName":"Cliente Smoke PF Atualizado",
  "birthDate":"1990-01-01",
  "email":"pf-smoke-updated@example.invalid",
  "phone":"+55 (44) 99999-0000",
  "isActive":true,
  "addresses":[{
    "type":"Primary",
    "isPrimary":true,
    "postalCode":"87000-000",
    "street":"Rua Fixture Atualizada",
    "number":"101",
    "neighborhood":"Centro",
    "city":"Maringa",
    "state":"PR"
  }]
}'

curl -fsS   -X PUT "$API_BASE/customers/$individual_id"   -H "Authorization: Bearer $admin_token"   -H "Content-Type: application/json"   -d "$update_payload" |
  jq -e '.displayName == "Cliente Smoke PF Atualizado"' >/dev/null

company_payload='{
  "personType":"Company",
  "document":"11.222.333/0001-81",
  "displayName":"Empresa Smoke",
  "legalName":"Empresa Smoke LTDA",
  "foundationDate":"2020-01-01",
  "stateRegistration":"ISENTO",
  "email":"pj-smoke@example.invalid",
  "phone":"44999990000",
  "addresses":[{
    "type":"Primary",
    "isPrimary":true,
    "postalCode":"87000-000",
    "street":"Avenida Fixture",
    "number":"200",
    "neighborhood":"Centro",
    "city":"Maringa",
    "state":"PR"
  }]
}'

company_response="$(
  curl -fsS     -X POST "$API_BASE/customers"     -H "Authorization: Bearer $admin_token"     -H "Content-Type: application/json"     -d "$company_payload"
)"
company_id="$(printf '%s' "$company_response" | jq -er '.id')"
test "$(printf '%s' "$company_response" | jq -r '.document')" = "11222333000181"

duplicate_status="$(
  curl -sS -o /dev/null -w '%{http_code}'     -X POST "$API_BASE/customers"     -H "Authorization: Bearer $admin_token"     -H "Content-Type: application/json"     -d "$company_payload"
)"
test "$duplicate_status" = "409"

curl -fsS -o /dev/null -w '%{http_code}'   -X DELETE "$API_BASE/customers/$company_id"   -H "Authorization: Bearer $admin_token" |
  grep -qx '204'

curl -fsS -o /dev/null -w '%{http_code}'   -X DELETE "$API_BASE/customers/$individual_id"   -H "Authorization: Bearer $admin_token" |
  grep -qx '204'

deleted_status="$(
  curl -sS -o /dev/null -w '%{http_code}'     "$API_BASE/customers/$individual_id"     -H "Authorization: Bearer $admin_token"
)"
test "$deleted_status" = "404"

echo "Customers PF/PJ CRUD, RBAC, pagination and uniqueness smoke test passed."
