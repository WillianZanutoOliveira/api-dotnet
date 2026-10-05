[🇧🇷 Português](recruiter-guide.md)

# Recruiter / Engineering Walkthrough

This page is a **5-minute inspection guide** for recruiters, senior engineers and hiring managers evaluating the repository.

The goal is to make the engineering evidence easy to verify without reading the entire codebase.

## 1. Service boundaries

Start with the high-level architecture:

- [Architecture overview](./architecture.en.md)
- [Integration event contracts](../src/BuildingBlocks/Contracts/IntegrationEvents.cs)

The platform is split into four independently deployable services:

- Orders
- Inventory
- Payments
- Notifications

Stateful services own their own PostgreSQL database. Services do not read each other's tables.

## 2. Clean Architecture

The Orders bounded context is intentionally structured into:

```text
Orders.Domain
      ↑
Orders.Application
      ↑
Orders.Infrastructure
      ↑
Orders.Api
```

Useful files:

- [Order aggregate](../src/Services/Orders/Orders.Domain/Order.cs)
- [Application ports](../src/Services/Orders/Orders.Application/Abstractions.cs)
- [Application use case](../src/Services/Orders/Orders.Application/OrderService.cs)
- [Infrastructure composition](../src/Services/Orders/Orders.Infrastructure/DependencyInjection.cs)

The domain layer has no dependency on ASP.NET Core, EF Core, RabbitMQ or MassTransit.

## 3. RabbitMQ and event-driven collaboration

The main asynchronous flow is:

```text
POST /orders
   ↓
OrderSubmitted
   ↓
Inventory
   ↓
InventoryReserved / InventoryRejected
   ↓
Payments
   ↓
PaymentAuthorized / PaymentFailed
   ↓
Orders + Notifications
```

Useful files:

- [Orders API and MassTransit setup](../src/Services/Orders/Orders.Api/Program.cs)
- [Inventory consumer](../src/Services/Inventory/Inventory.Service/OrderSubmittedConsumer.cs)
- [Payment consumer](../src/Services/Payments/Payments.Service/InventoryReservedConsumer.cs)
- [Notification consumers](../src/Services/Notifications/Notifications.Service/PaymentConsumers.cs)

## 4. Reliability: Outbox, Inbox and idempotency

The repository intentionally addresses the distributed dual-write problem.

Orders uses MassTransit EF Core Bus Outbox so order persistence and outgoing message intent share the same persistence boundary.

Stateful consumers use inbox/outbox support and business-level duplicate protection.

Useful evidence:

- [Orders DbContext / outbox entities](../src/Services/Orders/Orders.Infrastructure/OrdersDbContext.cs)
- [Inventory DbContext / inbox-outbox](../src/Services/Inventory/Inventory.Service/InventoryDbContext.cs)
- [Payments DbContext / inbox-outbox](../src/Services/Payments/Payments.Service/PaymentsDbContext.cs)
- [ADR: Transactional Outbox](./adr/0002-transactional-outbox.en.md)
- [ADR: Idempotency and eventual consistency](./adr/0003-idempotency-eventual-consistency.en.md)

## 5. Observability

The platform uses a vendor-neutral OpenTelemetry building block.

Useful evidence:

- [Shared telemetry building block](../src/BuildingBlocks/Observability/PlatformTelemetry.cs)
- [Architecture notes on observability](./architecture.en.md#observability)

The code exposes custom spans and metrics over OTLP. The local environment includes OpenTelemetry Collector, Tempo, Prometheus and Grafana.

## 6. Testing and delivery

Useful evidence:

- [Order domain tests](../tests/Orders.Domain.Tests/OrderTests.cs)
- [Real PostgreSQL integration through Testcontainers](../tests/Orders.Persistence.IntegrationTests/OrderRepositoryTests.cs)
- [GitHub Actions CI](../.github/workflows/ci.yml)
- [Docker Compose](../docker-compose.yml)
- [Kubernetes examples](../deploy/k8s/README.en.md)

The CI pipeline validates:

- dependency restore;
- Release build;
- automated tests;
- code coverage;
- Sonar/.editorconfig/dotnet format quality gate;
- unit and Testcontainers tests;
- Gateway + four service image builds;
- Keycloak → YARP → Orders smoke test using the Vault profile;
- a real ephemeral PostgreSQL user created by Vault;
- no effective connection string in the application environment;
- actual lease renewal during CI.

## 7. Security and identity

Useful evidence:

- [Security building block](../src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs)
- [Versioned local realm](../deploy/keycloak/distributed-commerce-realm.json)
- [Authentication/authorization smoke test](../scripts/auth-smoke.sh)
- [Identity ADR](./adr/0004-identity-keycloak.en.md)

The project demonstrates JWT validation, audience/issuer checks, RBAC and object-level authorization. CustomerId is not trusted from the payload: it comes from the authenticated sub.

Secrets use workload-scoped Vault tokens and policies. RabbitMQ remains in KV v2, while PostgreSQL uses dynamic credentials. Orders, Inventory and Payments receive a temporary login with TTL + renewable lease, while permissions live in stable PostgreSQL `NOLOGIN` roles.

Evidence:

- [Dynamic secret loader](../src/BuildingBlocks/Secrets/VaultConfigurationExtensions.cs)
- [Lease renewal service](../src/BuildingBlocks/Secrets/VaultLeaseRenewalService.cs)
- [Database Secrets Engine bootstrap](../deploy/vault/vault-init.sh)
- [Vault overlay](../docker-compose.vault.yml)
- [ADR-0006 — secrets vault](./adr/0006-secrets-hashicorp-vault.en.md)
- [ADR-0007 — dynamic PostgreSQL credentials](./adr/0007-dynamic-postgresql-credentials.en.md)

## 8. Developer experience with .NET Aspire

The repository also makes **developer experience** and technical onboarding inspectable.

Evidence:

- [Aspire AppHost](../src/Platform/DistributedCommerce.AppHost/AppHost.cs)
- [AppHost project](../src/Platform/DistributedCommerce.AppHost/DistributedCommerce.AppHost.csproj)
- [Local-development guide](./local-development.en.md)
- [ADR-0008 — local Aspire orchestration](./adr/0008-dotnet-aspire-local-orchestration.en.md)

A contributor can start the local topology with:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

The AppHost keeps Keycloak, Vault, RabbitMQ and the three PostgreSQL databases containerized while the Gateway and four .NET services run as local projects. This preserves dynamic PostgreSQL credentials and lease renewal while enabling breakpoints, per-resource logs and centralized Aspire Dashboard telemetry.

Docker Compose remains the CI-tested parity path.

## 9. AI engineering automation

Useful evidence:

- [AGENTS.md](../AGENTS.md)
- [Engineering constitution](../.ai/engineering-constitution.md)
- [AI Evolution Harness workflow](../.github/workflows/ai-evolution.yml)
- [Harness ADR](./adr/0005-ai-engineering-harness.en.md)

The agent can implement changes in an isolated workspace, but it must pass quality gates and can only deliver through a branch + pull request. There is no auto-merge.

## 10. Clean Code and DevSecOps

Useful evidence:

- [Shared rules](../.editorconfig)
- [SonarAnalyzer build integration](../Directory.Build.props)
- [Security pipeline](../.github/workflows/security.yml)
- [Code-quality guide](./code-quality.en.md)

The build treats warnings as errors and CI verifies formatting/analyzers. A separate pipeline runs CodeQL, Trivy and generates an SPDX SBOM.

## 11. Architecture decisions

The ADRs document trade-offs instead of only implementation details:

- [ADR-0001 — Event-driven services and Clean Architecture](./adr/0001-event-driven-clean-architecture.en.md)
- [ADR-0002 — Transactional Outbox](./adr/0002-transactional-outbox.en.md)
- [ADR-0003 — Idempotency and eventual consistency](./adr/0003-idempotency-eventual-consistency.en.md)
- [ADR-0004 — Identity and authorization with Keycloak](./adr/0004-identity-keycloak.en.md)
- [ADR-0005 — AI-assisted engineering harness](./adr/0005-ai-engineering-harness.en.md)
- [ADR-0006 — Centralized secrets management with HashiCorp Vault](./adr/0006-secrets-hashicorp-vault.en.md)
- [ADR-0007 — Dynamic PostgreSQL credentials with Vault Database Secrets Engine](./adr/0007-dynamic-postgresql-credentials.en.md)
- [ADR-0008 — .NET Aspire as the local development orchestrator](./adr/0008-dotnet-aspire-local-orchestration.en.md)

## What this repository is intended to demonstrate

This repository is not presented as a production system or as proof that every product should use microservices.

It is a public engineering reference designed to make the following concerns inspectable:

- architecture boundaries;
- asynchronous messaging;
- reliable event delivery;
- idempotency;
- eventual consistency;
- observability;
- containerized delivery;
- CI quality gates;
- explicit technical trade-offs.
