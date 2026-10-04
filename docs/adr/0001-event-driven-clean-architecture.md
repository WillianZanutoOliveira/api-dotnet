# ADR-0001: Event-driven services with Clean Architecture

- **Status:** Accepted
- **Date:** 2026-10-03

## Context

The project needs to demonstrate service boundaries and distributed-system concerns without coupling business rules to transport or persistence technology.

## Decision

Use independently deployable services connected through RabbitMQ integration events.

The Orders bounded context uses explicit Clean Architecture layers:

- Domain;
- Application;
- Infrastructure;
- API.

Smaller consumers use a lighter internal organization to avoid ceremony that does not improve their current complexity.

## Consequences

### Positive

- domain code is framework-independent;
- infrastructure can change without changing domain rules;
- service boundaries are explicit;
- asynchronous consumers evolve independently;
- the project demonstrates architectural judgment rather than mechanically applying layers everywhere.

### Trade-offs

- distributed deployments are operationally more complex than a modular monolith;
- business flows become eventually consistent;
- debugging requires correlation and observability.
