[🇧🇷 Português](0007-dynamic-postgresql-credentials.md)

# ADR-0007 — Dynamic PostgreSQL credentials with Vault Database Secrets Engine

## Status

Accepted.

## Context

ADR-0006 centralized application secrets in HashiCorp Vault and removed effective PostgreSQL/RabbitMQ credentials from application-container environments.

One conceptual issue remained: a static database password is still long-lived even when stored safely. If leaked, its impact continues until an explicit rotation happens.

This evolution removes permanent PostgreSQL credentials from the Orders, Inventory and Payments workloads.

## Decision

Use the **Vault Database Secrets Engine** with the PostgreSQL plugin to issue a unique username and password for each workload execution.

The flow is:

```text
Orders / Inventory / Payments
          |
          | workload-scoped Vault token
          v
Vault database/creds/<service>-app
          |
          | creates temporary PostgreSQL user
          | returns username + password + lease_id + TTL
          v
Connection string built only in memory
          |
          v
PostgreSQL
```

Current Vault roles:

- `database/creds/orders-app`
- `database/creds/inventory-app`
- `database/creds/payments-app`

Each response contains a `lease_id`, TTL and `renewable` flag.

## PostgreSQL identity and authorization separation

The generated login does not receive business permissions directly.

Each database owns a stable NOLOGIN PostgreSQL role:

- `orders_runtime`
- `inventory_runtime`
- `payments_runtime`

Vault creates a temporary login and grants membership only in the matching runtime role.

The connection includes `Options=-c role=<runtime_role>`, so sessions switch to the stable runtime role while the ephemeral login remains only the temporary identity issued by Vault.

The runtime role has no DDL permission. Schema ownership and object creation belong to a separate stable migration role, as documented in [ADR-0011](0011-ef-migrations-vault-deployment-identity.en.md).

## TTL and renewal

The local secure profile uses:

- `default_ttl = 5m`
- `max_ttl = 24h`

`DistributedCommerce.Secrets` loads `lease_id`, `lease_duration` and `renewable` together with the credentials.

A `BackgroundService` renews the lease before expiration through `sys/leases/renew`.

Each workload token can only:

- read its KV v2 path;
- generate credentials from its own database role;
- renew leases under its own database-role prefix.

Orders example:

```text
secret/data/platform/orders                      read
database/creds/orders-app                        read
sys/leases/renew/database/creds/orders-app/*    update
```

## Fail closed

If Vault is configured and:

- the token is missing;
- dynamic credential issuance fails;
- the lease prefix is unexpected;
- or lease renewal fails;

the service does not silently continue with a fallback database credential.

A renewal failure terminates the host. In an orchestrated environment, the workload is expected to restart, authenticate again and receive a new database identity.

## PostgreSQL statements

Every dynamic role explicitly defines:

- `creation_statements`;
- `renew_statements`;
- `revocation_statements`;
- `rollback_statements`.

Vault creates a temporary LOGIN role, sets `VALID UNTIL`, grants only the matching runtime role and updates `VALID UNTIL` on renewal.

Revocation removes membership and drops the ephemeral login.

## Plugin administrative credential

The local environment uses the PostgreSQL bootstrap administrator to configure the Vault plugin. This is a **development fixture** because Vault runs in ephemeral `server -dev` mode.

Production should:

1. create a dedicated Vault database-administration identity instead of using `postgres`;
2. grant only the permissions needed to create, renew and revoke managed identities;
3. configure the Database Secrets Engine connection;
4. rotate the plugin administrative credential through Vault where appropriate;
5. use TLS between Vault and PostgreSQL;
6. enable audit devices and monitor issuance, renewal and revocation.

## Schema migrations

Migration/runtime separation is now implemented rather than merely recommended.

Each database has two stable roles:

- `<service>_runtime` — workload DML only, without schema `CREATE`;
- `<service>_migrator` — DDL required for EF Core migrations.

Vault issues separate dynamic credentials for `<service>-app` and `<service>-migration`. A one-shot `DatabaseMigrator` runs `MigrateAsync()` before the workload starts. Services do not run `EnsureCreatedAsync` or schema migrations during application startup.

Ownership, default privileges and GitOps/PreSync details are documented in [ADR-0011](0011-ef-migrations-vault-deployment-identity.en.md).

## CI validation

The secure pipeline verifies that:

1. the stack starts with `docker-compose.vault.yml`;
2. Orders can create/read orders through Keycloak + YARP;
3. PostgreSQL contains a Vault-generated dynamic role;
4. `ConnectionStrings__orders-db` remains empty in the application-container environment;
5. the effective connection string exists only in the in-memory Vault-loaded configuration;
6. logs confirm at least one runtime database lease renewal;
7. `__EFMigrationsHistory` proves EF Migrations created the schema;
8. `orders_runtime` has no schema `CREATE`;
9. `orders_migrator` has DDL permission;
10. a Vault-generated migration login exists.

## Consequences

### Benefits

- no long-lived PostgreSQL password is delivered to a workload;
- credentials are unique per issuance;
- every database identity has bounded lifetime;
- revocation is centralized;
- leaked-credential blast radius is reduced;
- auditing can correlate issuance with a lease;
- the project demonstrates secret lifecycle rather than only secret storage.

### Trade-offs

- Vault participates in service bootstrap;
- lease renewal must be monitored;
- a new credential is required after `max_ttl`;
- migration and runtime identities, policies and lifecycles must remain separate;
- Vault HA, unseal, backup and disaster recovery become platform concerns.

## Alternatives considered

### Static password in GitHub/Kubernetes Secret

Simpler, but still long-lived and externally rotated.

### Static password in Vault KV

Improves storage/governance but does not remove credential permanence.

### Vault dynamic credential

Chosen because it combines TTL, on-demand issuance, centralized revocation, leasing and least privilege.
