[🇧🇷 Português](README.md)

# GitOps and progressive delivery

This directory models a production strategy with **Argo CD + Argo Rollouts + Vault Kubernetes Auth**.

CI does not imperatively run `kubectl apply`. CI publishes artifacts; Git remains the deployment source of truth, and Argo CD reconciles cluster state from the reviewed repository state.

## Flow

```text
v1.2.3 tag
    |
    v
Release OCI Images
    |
    +--> Orders:v1.2.3
    +--> DatabaseMigrator:v1.2.3
    +--> provenance attestations
    |
    v
GitOps Promotion workflow
    |
    +--> update Kustomization newTag
    +--> create PR
    |
    v
human review + merge
    |
    v
Argo CD detects main change
    |
    v
PreSync Database Migration Job
    |
    v
Argo Rollouts
    |
    +--> 20%
    +--> pause
    +--> canary health analysis
    +--> 50%
    +--> pause
    +--> canary health analysis
    +--> 100%
```

No promotion workflow auto-merges its pull request.

## Structure

- `argocd/orders-production.yaml` — declarative Argo CD Application.
- `orders-canary/kustomization.yaml` — desired image versions.
- `orders-canary/migration-job.yaml` — `PreSync` migration hook.
- `orders-canary/rollout.yaml` — canary strategy.
- `orders-canary/analysis-template.yaml` — automated canary analysis.
- `vault/` — Kubernetes Auth policies/setup expectations.

## Migration before workload

The migration Job uses the `database-migrator` image and the Vault `orders-migration` identity.

The regular application uses `orders-app`.

This preserves:

```text
orders-migrator ServiceAccount
        |
        v
Vault Kubernetes Auth
        |
        v
database/creds/orders-migration
        |
        v
orders_migrator PostgreSQL role
        |
        +--> DDL / EF migrations


orders-api ServiceAccount
        |
        v
Vault Kubernetes Auth
        |
        v
database/creds/orders-app
        |
        v
orders_runtime PostgreSQL role
        |
        +--> DML only
```

The Job uses `vault.hashicorp.com/agent-pre-populate-only: "true"` so a persistent Agent sidecar does not block Job completion.

## Canary

Orders uses five replicas so a basic canary without an external traffic router has useful weight granularity.

Sequence:

1. 20%;
2. 60-second pause;
3. three checks against `orders-api-canary`;
4. 50%;
5. 120-second pause;
6. another analysis;
7. 100%.

Analysis calls:

```text
http://orders-api-canary:8080/health/deployment
```

The endpoint returns JSON derived from ASP.NET Core `HealthCheckService`.

A failed analysis stops automatic progression.

For exact traffic shaping at higher scale, integrate Argo Rollouts with a supported ingress or service mesh rather than introducing one only for portfolio breadth.

## Argo CD

The Application tracks:

```text
main:deploy/gitops/orders-canary
```

with automated sync, self-heal, prune, PruneLast and ApplyOutOfSyncOnly.

## Promotion workflow

After a `v*` tag publishes images to GHCR, run:

```text
GitHub Actions
→ GitOps Promotion
→ version = v1.2.3
```

The workflow validates the version/tag, updates both image references and opens a pull request. It never merges automatically.

This separates **build**, **promotion** and **deployment**.

## Cluster prerequisites

The cluster needs:

- Argo CD;
- Argo Rollouts controller/CRDs;
- HashiCorp Vault Agent Injector;
- Vault Kubernetes Auth;
- reachable PostgreSQL, RabbitMQ, Keycloak and OTel services.

See the [Vault Kubernetes Auth guide](vault/README.en.md).
