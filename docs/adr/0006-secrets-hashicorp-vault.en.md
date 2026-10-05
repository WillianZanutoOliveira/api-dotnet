[🇧🇷 Português](0006-secrets-hashicorp-vault.md)

# ADR-0006 — Centralized secrets management with HashiCorp Vault

## Status

Accepted.

## Context

The first local environment passed PostgreSQL and RabbitMQ credentials directly through application-container environment variables. This is convenient for learning, but it does not model the security posture expected from enterprise systems.

Application credentials need:

- centralized storage;
- least-privilege policies;
- rotation capability;
- workload identity;
- auditing;
- reduced exposure in environment variables and configuration files.

## Decision

Adopt HashiCorp Vault as a vendor-neutral secrets manager.

The repository provides a secure overlay:

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

In this mode:

1. Vault runs in development mode exclusively for the local demo.
2. A bootstrap writes only secrets that remain static in the demo, such as RabbitMQ credentials, into KV v2.
3. Each service receives a separate Vault token whose policy can read only its own path.
4. The token is delivered as a mounted file rather than an application environment variable.
5. The `DistributedCommerce.Secrets` building block loads Vault data before dependency composition.
6. PostgreSQL no longer uses static application passwords in KV: Orders, Inventory and Payments receive temporary credentials from the Database Secrets Engine.
7. Values loaded from Vault override the intentionally empty credential environment variables.

Current paths:

- `secret/data/platform/orders`
- `secret/data/platform/inventory`
- `secret/data/platform/payments`
- `secret/data/platform/notifications`

## Production

`server -dev`, the bootstrap root token and the local 24-hour tokens are **not production configuration**.

On Kubernetes, workloads should preferably authenticate using platform identity through Kubernetes Auth / Vault Secrets Operator or an equivalent mechanism. AppRole remains a fallback when a trusted platform authenticator is not available.

Policies should be service-specific and tokens should be short-lived and narrowly scoped.

PostgreSQL credentials now use the Database Secrets Engine. TTL, lease renewal, revocation and runtime roles are documented in [ADR-0007](0007-dynamic-postgresql-credentials.en.md).

## Consequences

Benefits:

- reduces direct credential exposure in application containers;
- demonstrates service-level least privilege;
- centralizes governance and rotation;
- makes Platform Engineering / DevSecOps practices inspectable.

Trade-offs:

- introduces a critical operational dependency;
- Vault availability becomes part of platform architecture;
- production bootstrap, unseal, HA, audit devices and disaster recovery need dedicated design.
