#!/bin/sh
set -eu

export VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
export VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:?VAULT_DEV_ROOT_TOKEN_ID is required}"

vault secrets enable -path=secret kv-v2 >/dev/null 2>&1 || true

vault kv put secret/platform/orders   ConnectionStrings__orders-db="Host=orders-db;Port=5432;Database=orders;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/inventory   ConnectionStrings__inventory-db="Host=inventory-db;Port=5432;Database=inventory;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/payments   ConnectionStrings__payments-db="Host=payments-db;Port=5432;Database=payments;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/notifications   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

write_policy_and_token() {
  service="$1"
  secret_path="$2"
  token_dir="/tokens/$service"
  policy_file="/tmp/$service-policy.hcl"

  cat > "$policy_file" <<EOF
path "secret/data/$secret_path" {
  capabilities = ["read"]
}
EOF

  vault policy write "$service-service" "$policy_file" >/dev/null
  mkdir -p "$token_dir"
  vault token create     -field=token     -policy="$service-service"     -ttl=24h     -renewable=true > "$token_dir/token"
  chmod 0400 "$token_dir/token"
}

write_policy_and_token orders platform/orders
write_policy_and_token inventory platform/inventory
write_policy_and_token payments platform/payments
write_policy_and_token notifications platform/notifications

echo "Vault development secrets and scoped service tokens are ready."
