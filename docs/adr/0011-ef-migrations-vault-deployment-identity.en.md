[🇧🇷 Português](0011-ef-migrations-vault-deployment-identity.md)

# ADR-0011 — EF Core Migrations with a separate Vault deployment identity

## Status

Accepted.

## Context

Services originally created schema during startup with `EnsureCreatedAsync`. This simplified the demo but mixed application execution with database schema administration, forced runtime identities to hold schema `CREATE`, and bypassed EF Core migration history.

The goal is to apply least privilege to the schema lifecycle.

## Decision

Commit EF Core Migrations to source control and apply them through a one-shot `DatabaseMigrator` before the workload starts.

Business services:

- do not call `EnsureCreatedAsync`;
- do not call `MigrateAsync`;
- receive no DDL permission;
- start only after the matching migrator succeeds.

## PostgreSQL roles

Every database has two stable `NOLOGIN` roles.

Runtime roles:

- `orders_runtime`
- `inventory_runtime`
- `payments_runtime`

They receive CONNECT, schema USAGE and the DML required by the workload, but no schema CREATE.

Migration roles:

- `orders_migrator`
- `inventory_migrator`
- `payments_migrator`

They receive CONNECT plus schema USAGE/CREATE and own migration-created objects.

`ALTER DEFAULT PRIVILEGES` automatically grants runtime DML on tables and sequences created by the migration role.

## Dynamic Vault identities

Runtime example:

```text
database/creds/orders-app
        |
        +--> temporary LOGIN
        +--> orders_runtime membership
        +--> 5m default TTL / 24h max TTL
        +--> renewable
```

Migration example:

```text
database/creds/orders-migration
        |
        +--> temporary LOGIN
        +--> orders_migrator membership
        +--> 15m default TTL / 1h max TTL
```

The migrator Vault token has its own policy, cannot read RabbitMQ/KV application secrets and is not reused by the workload.

## DatabaseMigrator

`src/Platform/DatabaseMigrator` is a one-shot console process.

It obtains a dynamic database credential from Vault, constructs the connection string only in memory, chooses the requested DbContext, runs `Database.MigrateAsync()`, then exits.

## Startup ordering

Secure Compose:

```text
PostgreSQL -> Vault bootstrap -> DatabaseMigrator -> workload
```

Aspire uses the equivalent `WaitForCompletion(migrator)`.

GitOps/Kubernetes executes the migrator as an Argo CD `PreSync` Job with a separate ServiceAccount and Vault Kubernetes Auth role.

## Versioned migrations

Orders, Inventory and Payments have committed initial migrations and `__EFMigrationsHistory`.

`IDesignTimeDbContextFactory<T>` factories let engineers create new migrations without starting Keycloak, RabbitMQ or Vault.

Generated migrations must be reviewed before merge.

## CI

The pipeline proves that:

- application services contain no `EnsureCreatedAsync`;
- migrations were applied;
- `__EFMigrationsHistory` exists;
- `orders_runtime` has no schema CREATE;
- `orders_migrator` has schema CREATE;
- a temporary Vault migration login exists;
- the normal Keycloak → YARP → Orders path still works.

## Production

EF Core guidance recommends a separate deployment identity with schema privileges while the application runtime identity normally holds only the read/write permissions it needs.

A higher-governance evolution can publish EF migration bundles or reviewed SQL scripts from CI and execute that deployment artifact in the PreSync Job. The identity model from this ADR remains unchanged.

## Consequences

Benefits include real database least privilege, versioned schema, auditable deployment, stable object ownership and rollout blocking on migration failure.

Trade-offs include an additional deployment stage and the need for expand/contract migrations when old and new application versions coexist during canary delivery.
