[🇧🇷 Português](secrets-management.md)

# Secrets management

## Goal

The secure profile uses HashiCorp Vault for two different secret categories:

1. **static infrastructure secrets that still have to exist**, such as local RabbitMQ credentials;
2. **dynamic PostgreSQL credentials**, issued on demand by the Vault Database Secrets Engine.

This distinction matters: moving a fixed password into Vault improves storage and governance; issuing a short-lived identity also reduces exposure time and blast radius.

## Running the secure profile

If an older project version already created persistent PostgreSQL volumes, recreate them once so the runtime roles are initialized:

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml down -v
```

Then:

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

The `.env` file contains only **local infrastructure bootstrap values**.

Orders, Inventory and Payments receive `ConnectionStrings__*-db=""`. The effective connection string is built in memory after the service obtains a temporary PostgreSQL identity from Vault.

## Flow architecture

```text
                     local bootstrap
.env ------------------------------------------------+
                                                     |
                                                     v
                                            +-----------------+
                                            | Vault server-dev |
                                            +-----------------+
                                                |          |
                         KV v2 (RabbitMQ) -------+          +---- Database Secrets Engine
                                                |                        |
                                                v                        v
                                     workload token           temporary PostgreSQL user
                                                |              + password + lease_id + TTL
                                                +------------------------+
                                                                         |
                                                                         v
                                                          in-memory service config
                                                                         |
                                                                         v
                                                                    PostgreSQL
```

## Vault paths

### KV v2

- `secret/data/platform/orders`
- `secret/data/platform/inventory`
- `secret/data/platform/payments`
- `secret/data/platform/notifications`

These paths currently carry RabbitMQ credentials.

### Database Secrets Engine

- `database/creds/orders-app`
- `database/creds/inventory-app`
- `database/creds/payments-app`

These endpoints do not return a permanent application password. Each read generates a new PostgreSQL login with bounded lifetime.

## PostgreSQL authorization model

Stable permissions live in `NOLOGIN` roles:

| Service | Vault dynamic role | Stable PostgreSQL role |
| --- | --- | --- |
| Orders | `orders-app` | `orders_runtime` |
| Inventory | `inventory-app` | `inventory_runtime` |
| Payments | `payments-app` | `payments_runtime` |

The generated login only receives membership in the matching runtime role.

Example:

```text
v-token-orders-...  (LOGIN, temporary)
        |
        +--> member of orders_runtime (NOLOGIN)
                         |
                         +--> CONNECT orders
                         +--> USAGE public
                         +--> CREATE public only in local demo
```

The connection string includes:

```text
Options=-c role=orders_runtime
```

so the session operates as the stable authorization identity.

## Dynamic credential lifecycle

### 1. Issuance

At startup, `DistributedCommerce.Secrets` calls:

```text
GET /v1/database/creds/orders-app
```

The response contains:

```text
username
password
lease_id
lease_duration
renewable
```

The password is not written to disk or exported as an application-container environment variable.

### 2. In-memory connection string

The service builds the connection string from:

- host;
- port;
- database;
- temporary username;
- temporary password;
- stable runtime role.

### 3. Renewal

The project registers `VaultLeaseRenewalService`.

By default it renews around half of the lease lifetime, with a 30-second minimum. CI uses a shorter interval so renewal is observable during smoke testing.

It calls:

```text
POST /v1/sys/leases/renew/<lease_id>
```

Renewal extends the same temporary identity; it does not make the credential permanent.

### 4. Max TTL

The demo uses:

- initial TTL: **5 minutes**;
- maximum TTL: **24 hours**.

After `max_ttl`, a workload should reauthenticate and obtain a new identity. In orchestration this normally means controlled restart/re-creation or a rotation mechanism owned by the platform.

### 5. Revocation

Vault roles define explicit statements to revoke membership and drop the ephemeral PostgreSQL login.

## Least privilege inside Vault

Orders does not receive a generic database policy.

Its token can only:

```text
read   secret/data/platform/orders
read   database/creds/orders-app
update sys/leases/renew/database/creds/orders-app/*
```

Inventory and Payments have equivalent boundaries. Notifications has no database role because it does not use PostgreSQL.

## Fail closed

When Vault is configured, secret failures do not fall back silently.

The service fails when:

- the token file is absent/empty;
- Vault cannot be reached;
- dynamic credential issuance fails;
- the returned lease does not match the expected role prefix;
- lease renewal fails.

A renewal failure terminates the host so the orchestrator can restart the workload and force a new authentication/credential cycle.

## What CI proves

The secure pipeline verifies more than syntax:

- build + Sonar + tests pass;
- Testcontainers uses a real PostgreSQL instance;
- Vault configures the Database Secrets Engine;
- Orders starts without an effective connection string in its environment;
- PostgreSQL contains a Vault-generated dynamic login;
- the API creates/reads orders through Keycloak + YARP;
- logs show lease renewal;
- CodeQL passes;
- Trivy finds no unpatched HIGH/CRITICAL vulnerability or detectable secret;
- an SPDX SBOM is generated.

## Production differences

The local profile is intentionally self-contained. It is **not a ready-to-deploy production template**.

Production requires:

### Vault

- HA;
- TLS;
- appropriate auto-unseal;
- audit devices;
- health/latency/error monitoring;
- backup/disaster recovery;
- platform workload authentication.

### Workload authentication

Preferred:

```text
Kubernetes ServiceAccount
        |
        v
Vault Kubernetes Auth / Vault Secrets Operator
        |
        v
short-lived token + workload policy
```

AppRole is a fallback when native platform identity is unavailable.

### Vault database administration

Do not permanently use PostgreSQL `postgres`.

Create a dedicated Vault database-management identity with only the privileges required to manage dynamic users, then use Database Secrets Engine root-credential rotation where appropriate.

### Migrations

Separate DDL from runtime:

```text
CI/CD migration identity ------> migrations / DDL
runtime dynamic identity ------> DML
```

The portfolio keeps `EnsureCreatedAsync` for a frictionless local demo, so local runtime roles retain schema `CREATE`.

## Key files

- [Vault loader](../src/BuildingBlocks/Secrets/VaultConfigurationExtensions.cs)
- [Lease renewer](../src/BuildingBlocks/Secrets/VaultLeaseRenewalService.cs)
- [Vault bootstrap](../deploy/vault/vault-init.sh)
- [Orders DB role](../deploy/postgres/orders-init.sql)
- [Inventory DB role](../deploy/postgres/inventory-init.sql)
- [Payments DB role](../deploy/postgres/payments-init.sql)
- [Secure Compose overlay](../docker-compose.vault.yml)
- [ADR-0006](adr/0006-secrets-hashicorp-vault.en.md)
- [ADR-0007](adr/0007-dynamic-postgresql-credentials.en.md)

## Interview explanation

A concise explanation:

> "I did not just move a password from appsettings into Vault. PostgreSQL uses the Database Secrets Engine: every workload receives a temporary identity with TTL, a renewable lease and its own Vault policy. Permissions live in a stable NOLOGIN role, the service builds the connection string only in memory, and it fails closed if lease renewal fails. CI proves a real dynamic PostgreSQL login exists while the effective password is absent from the container environment."

That demonstrates the difference between **secret storage** and **secret lifecycle management**.
