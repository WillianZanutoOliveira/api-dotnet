[🇧🇷 Português](0002-transactional-outbox.md)

# ADR-0002: Transactional outbox instead of distributed transactions

- **Status:** Accepted
- **Date:** 2026-10-03

## Context

Creating an order requires both:

1. persisting the order in PostgreSQL;
2. publishing `OrderSubmitted` to RabbitMQ.

Performing those operations independently creates a dual-write problem.

## Decision

Use MassTransit EF Core outbox support.

The application publishes normally through an abstraction, while the infrastructure captures the outgoing message in the same EF Core persistence boundary.

The broker delivery occurs asynchronously after the database commit.

Consumers also use EF Core inbox/outbox integration where they both persist state and publish follow-up events.

## Consequences

### Positive

- no two-phase distributed transaction;
- durable message intent;
- service state and outgoing message remain consistent;
- application code remains transport-agnostic.

### Trade-offs

- downstream services observe changes asynchronously;
- outbox tables require operational monitoring and cleanup behavior;
- delivery is at-least-once, therefore consumers must be idempotent.
