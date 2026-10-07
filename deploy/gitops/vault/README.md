[🇺🇸 English](README.en.md)

# Vault Kubernetes Auth para Orders

Os manifests GitOps usam Vault Agent Injector para autenticar pods pelo ServiceAccount do Kubernetes e compartilhar o token temporário em:

```text
/vault/secrets/token
```

A aplicação continua falando diretamente com Vault para obter credenciais PostgreSQL dinâmicas e renovar o lease de banco.

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

A configuração exata de `auth/kubernetes/config` depende do cluster e deve ser feita pela plataforma.

Depois de habilitar/configurar o auth method, crie roles vinculadas a ServiceAccounts diferentes:

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

A migration identity não recebe KV/RabbitMQ nem a role runtime. Ela pode somente pedir `database/creds/orders-migration`.

A runtime identity não recebe a role `orders_migrator`.

## Agent Injector

No Rollout:

- `agent-inject-token=true`;
- `agent-run-as-same-user=true`;
- sidecar/cache mantém o token Vault autenticado pelo Kubernetes.

No Job:

- `agent-pre-populate-only=true`;
- o token é preparado pelo init container;
- não há sidecar para impedir término do Job.

Não use `tls-skip-verify` em produção.
