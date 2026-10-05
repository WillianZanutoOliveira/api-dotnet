[🇧🇷 Português](0008-dotnet-aspire-local-orchestration.md)

# ADR-0008 — .NET Aspire as the local development orchestrator

## Status

Accepted.

## Context

The project already has a complete secure Docker Compose environment, but local development requires contributors to understand container startup order, inspect multiple log streams, discover endpoints manually and rebuild images for ordinary code changes.

The local experience must improve without removing important architecture characteristics:

- Keycloak;
- HashiCorp Vault;
- dynamic PostgreSQL credentials;
- RabbitMQ;
- workload-scoped policies;
- lease renewal;
- fail-closed behavior;
- OpenTelemetry.

## Decision

Adopt **Aspire 13.6 / .NET 10 AppHost** as the preferred local-development entry point.

The AppHost lives at:

```text
src/Platform/DistributedCommerce.AppHost/
```

Primary command:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

The AppHost:

1. starts PostgreSQL for Orders, Inventory and Payments;
2. starts RabbitMQ;
3. starts Keycloak and imports the local realm;
4. starts Vault in development mode;
5. runs Vault bootstrap;
6. writes workload-scoped tokens under `.aspire/vault-tokens`;
7. runs the four .NET services as local project processes;
8. runs the YARP API Gateway;
9. routes resource telemetry into the Aspire Dashboard.

## Parity principle

Aspire **does not replace the security architecture**.

The local profile still exercises:

```text
Keycloak
   |
   v
YARP Gateway
   |
   v
Orders API
   |
   +--> Vault KV v2 -> RabbitMQ credentials
   |
   +--> Vault Database Secrets Engine
                 |
                 +--> temporary PostgreSQL login
                 +--> TTL + lease
                 +--> lease renewal
```

Secure Docker Compose remains available and is still exercised by CI. Aspire is a second model of the same local topology optimized for the inner loop.

## Projects versus containers

.NET workloads are modeled with `AddProject` to enable incremental builds, breakpoints, hot reload where supported, per-resource logs and integrated telemetry.

External infrastructure remains containerized.

## Local credentials

No effective password is hard-coded in the AppHost.

Aspire generates secret parameters for:

- the local Vault root token;
- each PostgreSQL administrator password;
- RabbitMQ password;
- Keycloak administrator password.

Values are managed by the AppHost secret store.

Vault bootstrap accepts separate PostgreSQL administrator credentials while preserving compatibility with the shared `POSTGRES_USER/POSTGRES_PASSWORD` variables used by Docker Compose.

## Vault tokens

Scoped tokens are written to:

```text
.aspire/vault-tokens/
├── orders/token
├── inventory/token
├── payments/token
└── notifications/token
```

The complete `.aspire` directory is gitignored.

Each project receives only its own token path.

## Local ports

| Resource | Port |
| --- | ---: |
| API Gateway | 8080 |
| Orders API | 8081 |
| Inventory | 8082 |
| Payments | 8083 |
| Notifications | 8084 |
| Keycloak | 8180 |
| Vault | 8200 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Orders PostgreSQL | 5432 |
| Inventory PostgreSQL | 5433 |
| Payments PostgreSQL | 5434 |

The Aspire Dashboard uses its own HTTPS endpoint configured by the AppHost.

## Observability

Projects receive the AppHost OTLP settings through `WithOtlpExporter()`.

The Aspire Dashboard provides a single development surface for:

- logs;
- traces;
- metrics;
- resource state;
- endpoints;
- dependencies.

Grafana/Tempo/Prometheus remain available in Docker Compose to demonstrate vendor-neutral observability outside Aspire.

## Startup ordering

`vault-init` is a one-shot resource.

Vault-consuming services start only after it completes.

The bootstrap script also retries Database Secrets Engine configuration while PostgreSQL finishes initialization, eliminating a common local startup race.

## Docker Compose remains important

Aspire is the recommended development experience.

Secure Docker Compose remains for CI, smoke testing, parity checks, running the platform without AppHost tooling and the full Grafana/Tempo/Prometheus stack.

### Aspire CLI dependency

The AppHost explicitly sets `AspireUseCliBundle=false`. With the current Aspire release, this keeps `dotnet run` functional through SDK-restored orchestration dependencies without making a separately installed Aspire CLI another prerequisite for a new contributor.

The current SDK recommends moving to the CLI bundle in the future. This decision should be revisited when the ecosystem makes the bundle mandatory; until then, the repository optimizes for low-friction onboarding.

## Consequences

### Benefits

- simpler onboarding;
- one local entry point;
- central resource dashboard;
- .NET debugging without image rebuilds;
- predictable endpoints;
- generated local secrets;
- dynamic Vault credentials preserved;
- less Docker Compose knowledge required before contributing.

### Trade-offs

- two local orchestration models must remain coherent;
- fixed development ports must be available;
- AppHost is a development tool, not the production deployment architecture;
- infrastructure changes may need to be reflected in both models.

## Maintenance rule

Any topology change affecting local development should validate:

1. secure Docker Compose;
2. Aspire AppHost;
3. Portuguese and English documentation.

The engineering AI must not remove Vault, Keycloak or dynamic database credentials merely to simplify the AppHost.
