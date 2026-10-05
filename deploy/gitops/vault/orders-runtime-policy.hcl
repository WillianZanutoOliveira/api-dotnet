path "secret/data/platform/orders" {
  capabilities = ["read"]
}

path "database/creds/orders-app" {
  capabilities = ["read"]
}

path "sys/leases/renew/database/creds/orders-app/*" {
  capabilities = ["update"]
}
