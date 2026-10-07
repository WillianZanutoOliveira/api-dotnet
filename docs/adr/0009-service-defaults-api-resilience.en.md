[🇧🇷 Português](0009-service-defaults-api-resilience.md)

# ADR-0009 — Service Defaults, health model, OpenAPI and edge protection

## Status

Accepted.

## Context

The platform already had OpenTelemetry and a `/health` endpoint, but each service configured those concerns independently. With .NET Aspire in place, duplicated setup reduces consistency and increases the chance that a new workload is created without health, telemetry or HTTP resilience.

The public API also needs explicit HTTP contract discovery and edge protection against traffic bursts.

## Decision

Create `src/BuildingBlocks/ServiceDefaults` as the shared Aspire-aligned application default layer.

Each workload calls:

```csharp
builder.AddPlatformServiceDefaults("service-name");
```

and each web application maps:

```csharp
app.MapPlatformDefaultEndpoints();
```

## Shared defaults

The building block standardizes:

- the platform's existing OpenTelemetry setup;
- readiness at `/health`;
- liveness at `/alive`;
- `HttpClient` service discovery;
- the Standard Resilience Handler for HTTP clients.

This makes resilient HTTP behavior the default for future clients without changing the current event-driven service boundaries.

RabbitMQ remains the business integration mechanism between bounded contexts.

## Health semantics

`/health` represents readiness.

`/alive` runs only checks tagged `live`.

This allows an orchestrator to distinguish a process that is alive but temporarily not ready from a process that should be restarted.

## OpenAPI

Orders exposes ASP.NET Core's native OpenAPI document in Development:

```text
/openapi/v1.json
```

The Aspire topology integration test verifies that the document can be retrieved.

Production exposure is not enabled automatically.

## Gateway rate limiting

The YARP Gateway applies a token-bucket limiter per authenticated identity.

Partition key order:

1. authenticated `sub`;
2. remote IP fallback;
3. `unknown` final fallback.

Initial policy:

- 60-token burst;
- 60 tokens replenished per minute;
- no request queue;
- HTTP 429 rejection.

Operational endpoints do not consume the application user's rate-limit partition.

## Consequences

### Benefits

- less duplicated setup;
- predictable onboarding;
- consistent health semantics;
- resilient HTTP defaults;
- inspectable OpenAPI contract;
- baseline abuse/burst protection;
- better Aspire/Kubernetes alignment.

### Trade-offs

- the limiter is in-memory per Gateway replica;
- a globally shared quota requires a distributed design;
- HTTP retries still require idempotency awareness;
- a Development OpenAPI document does not replace API-versioning governance.

## Validation

CI validates the Service Defaults build, Aspire `/health` and `/alive`, the Orders OpenAPI document and the existing authenticated Gateway smoke test.
