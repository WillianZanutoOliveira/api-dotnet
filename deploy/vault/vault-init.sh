#!/bin/sh
set -eu

export VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
export VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:?VAULT_DEV_ROOT_TOKEN_ID is required}"

shared_postgres_user="${POSTGRES_USER:-}"
shared_postgres_password="${POSTGRES_PASSWORD:-}"

orders_postgres_user="${ORDERS_POSTGRES_USER:-$shared_postgres_user}"
orders_postgres_password="${ORDERS_POSTGRES_PASSWORD:-$shared_postgres_password}"
inventory_postgres_user="${INVENTORY_POSTGRES_USER:-$shared_postgres_user}"
inventory_postgres_password="${INVENTORY_POSTGRES_PASSWORD:-$shared_postgres_password}"
payments_postgres_user="${PAYMENTS_POSTGRES_USER:-$shared_postgres_user}"
payments_postgres_password="${PAYMENTS_POSTGRES_PASSWORD:-$shared_postgres_password}"

vault secrets enable -path=secret kv-v2 >/dev/null 2>&1 || true
vault secrets enable database >/dev/null 2>&1 || true

vault kv put secret/platform/orders   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/inventory   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/payments   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/notifications   RabbitMq__Username="${RABBITMQ_DEFAULT_USER}"   RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

configure_database() {
  connection_name="$1"
  host="$2"
  database="$3"
  vault_role="$4"
  runtime_role="$5"
  admin_user="$6"
  admin_password="$7"

  if [ -z "$admin_user" ] || [ -z "$admin_password" ]; then
    echo "Missing PostgreSQL bootstrap credentials for $connection_name." >&2
    exit 1
  fi

  attempt=1
  max_attempts=30

  while ! vault write "database/config/$connection_name"       plugin_name="postgresql-database-plugin"       allowed_roles="$vault_role"       connection_url="postgresql://{{username}}:{{password}}@$host:5432/$database?sslmode=disable"       username="$admin_user"       password="$admin_password"       password_authentication="scram-sha-256" >/dev/null 2>&1; do
    if [ "$attempt" -ge "$max_attempts" ]; then
      echo "Could not configure Vault database connection $connection_name after $max_attempts attempts." >&2
      exit 1
    fi

    echo "Waiting for PostgreSQL resource $connection_name to become reachable ($attempt/$max_attempts)..."
    attempt=$((attempt + 1))
    sleep 2
  done

  vault write "database/roles/$vault_role"     db_name="$connection_name"     creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' INHERIT; GRANT $runtime_role TO \"{{name}}\";"     renew_statements="ALTER ROLE \"{{name}}\" VALID UNTIL '{{expiration}}';"     revocation_statements="REVOKE $runtime_role FROM \"{{name}}\"; DROP ROLE IF EXISTS \"{{name}}\";"     rollback_statements="DROP ROLE IF EXISTS \"{{name}}\";"     default_ttl="5m"     max_ttl="24h" >/dev/null
}

configure_database orders orders-db orders orders-app orders_runtime   "$orders_postgres_user" "$orders_postgres_password"

configure_database inventory inventory-db inventory inventory-app inventory_runtime   "$inventory_postgres_user" "$inventory_postgres_password"

configure_database payments payments-db payments payments-app payments_runtime   "$payments_postgres_user" "$payments_postgres_password"

write_policy_and_token() {
  service="$1"
  secret_path="$2"
  database_role="${3:-}"
  token_dir="/tokens/$service"
  policy_file="/tmp/$service-policy.hcl"

  cat > "$policy_file" <<EOF
path "secret/data/$secret_path" {
  capabilities = ["read"]
}
EOF

  if [ -n "$database_role" ]; then
    cat >> "$policy_file" <<EOF

path "database/creds/$database_role" {
  capabilities = ["read"]
}

path "sys/leases/renew/database/creds/$database_role/*" {
  capabilities = ["update"]
}
EOF
  fi

  vault policy write "$service-service" "$policy_file" >/dev/null
  mkdir -p "$token_dir"
  vault token create     -field=token     -policy="$service-service"     -ttl=24h     -renewable=true > "$token_dir/token"
  chmod 0400 "$token_dir/token"
}

write_policy_and_token orders platform/orders orders-app
write_policy_and_token inventory platform/inventory inventory-app
write_policy_and_token payments platform/payments payments-app
write_policy_and_token notifications platform/notifications

echo "Vault KV secrets, dynamic database roles and scoped service tokens are ready."
