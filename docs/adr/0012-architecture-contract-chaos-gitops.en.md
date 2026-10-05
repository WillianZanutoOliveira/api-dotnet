[🇧🇷 Português](0012-architecture-contract-chaos-gitops.md)

# ADR-0012 — Architecture guardrails, contracts, fault injection and progressive delivery

## Status

Accepted.

## Context

A green build and unit tests alone do not prove important distributed-platform properties such as dependency direction, event-contract stability, network-failure behavior or safe gradual promotion.

These properties should be executable rather than documentation-only.

## Decision

Add four validation layers.

## Architecture tests

`tests/Architecture.Tests` uses reflection to block forbidden assembly dependencies: Domain cannot reference infrastructure/framework layers, Application cannot reference Infrastructure, Contracts cannot reference service implementations, and business services cannot directly reference each other.

## Contract compatibility

`tests/Contracts.Compatibility.Tests` records the expected public property shape of published integration events.

Removing, renaming or changing a property type breaks the test and requires an explicit compatibility decision.

For cross-repository ecosystems, the natural next step is consumer-driven contracts or a schema registry/compatibility service.

## Fault injection

`tests/Chaos.Tests` uses Testcontainers plus Toxiproxy.

The test starts a real PostgreSQL database, applies migrations, routes traffic through Toxiproxy, cuts connectivity, requires failure, restores the proxy and requires recovery.

This gives deterministic disposable infrastructure fault testing without a shared environment.

## GitOps

`deploy/gitops` models Argo CD as the reconciler.

CI publishes artifacts. Promotion selects an existing released tag, updates Kustomize through a pull request, requires review/merge, and lets Argo CD reconcile `main`.

CI does not imperatively deploy production.

## Canary

Orders uses Argo Rollouts with stable/canary Services and a 20% → analysis → 50% → analysis → 100% progression.

The AnalysisTemplate calls only the canary Service at `/health/deployment`. A failed analysis stops automatic progression.

No service mesh is added only for portfolio breadth. Without a traffic router, weights are replica-based; exact traffic splitting can be added later through a supported ingress/service mesh.

## Migration plus progressive delivery

The database migration is an Argo CD `PreSync` Job.

Canary deployment therefore requires backward-compatible migrations. Destructive changes should use expand/contract across releases.

## Vault Kubernetes Auth

Runtime and migration use different ServiceAccounts.

Vault Agent Injector shares a short-lived token at `/vault/secrets/token`. Migration Jobs use `agent-pre-populate-only` so no persistent sidecar prevents Job completion.

## Consequences

Benefits include executable architecture, guarded event contracts, real fault/recovery testing, declarative audited deployments, migration-before-rollout and automatic canary gates.

Trade-offs include the limits of reflection-based architecture rules, local chaos versus production game days, approximate canary weights without a traffic router and required cluster components such as Argo CD, Argo Rollouts and Vault Injector.
