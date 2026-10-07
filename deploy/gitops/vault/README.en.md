[🇧🇷 Português](README.md)

# Vault Kubernetes Auth for Orders

The GitOps manifests use Vault Agent Injector to authenticate pods from their Kubernetes ServiceAccount and share the short-lived Vault token at:

```text
/vault/secrets/token
```

The application still talks directly to Vault for dynamic PostgreSQL credentials and database-lease renewal.

## Policies

Runtime:

```bash
vault policy write orders-api orders-runtime-policy.hcl
```

Migration:

```bash
vault policy write orders-migrator orders-migration-policy.hcl
```

## Kubernetes Auth

The exact `auth/kubernetes/config` values are cluster-specific and belong to platform configuration.

After the auth method is enabled/configured, bind separate roles to separate ServiceAccounts:

```bash
vault write auth/kubernetes/role/orders-api \
  bound_service_account_names=orders-api \
  bound_service_account_namespaces=distributed-commerce \
  policies=orders-api \
  ttl=1h

vault write auth/kubernetes/role/orders-migrator \
  bound_service_account_names=orders-migrator \
  bound_service_account_namespaces=distributed-commerce \
  policies=orders-migrator \
  ttl=30m
```

The migration identity cannot read RabbitMQ/KV secrets and cannot request the runtime database role.

The runtime identity cannot assume `orders_migrator`.

## Agent Injector

For the Rollout:

- `agent-inject-token=true`;
- `agent-run-as-same-user=true`;
- the sidecar/cache keeps the Kubernetes-authenticated Vault token alive.

For the Job:

- `agent-pre-populate-only=true`;
- the init container prepares the token;
- no persistent sidecar blocks Job completion.

Do not use `tls-skip-verify` in production.
