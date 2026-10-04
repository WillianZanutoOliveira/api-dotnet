# ADR-0003: Idempotent consumers and eventual consistency

- **Status:** Accepted
- **Date:** 2026-10-03

## Context

RabbitMQ-based distributed workflows should assume messages may be delivered more than once.

A consumer must not create duplicate reservations, payments or invalid order transitions when a message is retried or replayed.

## Decision

Use both infrastructure and business-level safeguards:

- MassTransit EF Core inbox support;
- unique order decision records in Inventory and Payments;
- terminal-state checks in the Order aggregate;
- retry policies at receive endpoints.

The checkout journey is modeled as eventually consistent rather than attempting a global ACID transaction.

## Consequences

### Positive

- safe retry/replay behavior;
- no global database transaction;
- services remain independently deployable;
- failure behavior is explicit.

### Trade-offs

- clients must tolerate a `Pending` state;
- observability is required to understand asynchronous progress;
- compensation/failure paths must be modeled explicitly.
