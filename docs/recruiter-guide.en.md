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

The code exposes custom spans and metrics and can export through OTLP.

## 6. Testing and delivery

Useful evidence:

- [Order domain tests](../tests/Orders.Domain.Tests/OrderTests.cs)
- [GitHub Actions CI](../.github/workflows/ci.yml)
- [Docker Compose](../docker-compose.yml)
- [Kubernetes examples](../deploy/k8s/README.en.md)

The CI pipeline validates:

- dependency restore;
- Release build;
- automated tests;
- code coverage;
- four Docker image builds.

## 7. Architecture decisions

The ADRs document trade-offs instead of only implementation details:

- [ADR-0001 — Event-driven services and Clean Architecture](./adr/0001-event-driven-clean-architecture.en.md)
- [ADR-0002 — Transactional Outbox](./adr/0002-transactional-outbox.en.md)
- [ADR-0003 — Idempotency and eventual consistency](./adr/0003-idempotency-eventual-consistency.en.md)

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
