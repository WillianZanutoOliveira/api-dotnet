[🇧🇷 Português](security-posture.md)

# Security posture and pentest readiness

## Goal

The platform does not claim to be "invulnerable". No serious distributed system can guarantee that a future penetration test will find zero vulnerabilities.

The repository uses a stronger, auditable target:

> no change may introduce known High/Critical findings in automated gates, weaken authorization boundaries, reintroduce default credentials or remove least-privilege controls.

The baseline follows **OWASP ASVS 5.0.0**, **OWASP API Security Top 10 2023**, and current ASP.NET Core, Keycloak, Vault, container and Kubernetes practices.

## Trust boundaries

```text
Internet / client
      |
      | untrusted
      v
TLS ingress
      |
      v
Keycloak -------- JWT --------+
      |                        |
      |                        v
      +--------------------> YARP Gateway
                                |
                                | authenticated / rate limited
                                v
                            Orders API
                                |
                 +--------------+--------------+
                 |                             |
                 v                             v
             PostgreSQL                    RabbitMQ
                 ^                             |
                 |                             v
              Vault                  Inventory / Payments /
                 |                      Notifications
                 |
          migration identity
                 |
                 v
          DatabaseMigrator
```

Crossing a boundary requires explicit authentication, authorization, policy or configuration. Internal network placement is not treated as trust by itself.

## HTTP/API controls

Shared Service Defaults enforce:

- no Kestrel `Server` header;
- 1 MiB request-body limit;
- request-line/header limits;
- request-header timeout;
- HSTS outside Development;
- `nosniff`;
- frame denial;
- no-referrer;
- restrictive CSP;
- restrictive Permissions Policy;
- explicit TRACE/CONNECT rejection;
- `no-store` on authenticated requests.

Orders rejects unknown JSON properties, limits JSON depth and bounds collection, SKU, quantity and price inputs. `CustomerId` is never accepted as request authority.

## Authentication

Gateway and Orders both validate JWTs.

Validation requires signed RS256 tokens, issuer, audience, lifetime and expiration. Authentication error details are disabled and tokens are not saved by middleware.

Outside Development, Keycloak metadata/issuer/authority must use HTTPS.

The local realm uses brute-force protection and short access tokens. Its password grant exists only for disposable smoke/DAST fixtures. Interactive production clients should use Authorization Code + PKCE.

Production Keycloak should run in production mode with TLS and explicit hostname configuration, with administration interfaces isolated from the public frontend when practical.

## Authorization / BOLA

Orders creation derives customer identity from `sub`.

Customer reads query PostgreSQL by both `OrderId` and `CustomerId`; admin has an explicit cross-customer path. Requests for another customer's order return 404 to avoid resource-existence disclosure.

## Resource consumption

YARP uses an authenticated-subject token bucket: 60 tokens, 60/minute refill, no queue, HTTP 429 plus `Retry-After`.

CI verifies the limiter with adversarial traffic. DAST temporarily raises the limit only inside a disposable security-test stack so active scanning is not blocked.

## Secrets and data

Application settings contain no default database/broker credentials; missing secrets fail startup.

The secure profile uses Vault KV v2 and Database Secrets Engine, per-workload tokens, in-memory connection strings, DML-only runtime database roles, a separate DDL migration identity, and fail-closed lease renewal.

## Containers and Kubernetes

.NET workloads run non-root. Secure Compose drops capabilities, enables no-new-privileges and read-only root filesystems with an ephemeral writable `/tmp`.

Published development ports bind to `127.0.0.1`.

Kubernetes examples use non-root execution, RuntimeDefault seccomp, no privilege escalation and dropped capabilities. GitOps uses separate Vault Kubernetes Auth identities for workload and migrator.

## Security automation

Every relevant PR executes code-quality/security invariants, authentication/authorization and adversarial HTTP smoke tests, architecture/contract/chaos tests, CodeQL, Gitleaks, Trivy vulnerability/secret/misconfiguration scans and SPDX SBOM generation.

`.github/workflows/dast.yml` runs an authenticated **OWASP ZAP active API scan** against a disposable Keycloak → YARP → Orders stack and stores HTML/JSON/Markdown evidence.

OpenSSF Scorecard, Dependabot, k6 and OCI provenance cover recurring/supply-chain concerns.

## OWASP API Security Top 10 mapping

| Risk | Project controls |
| --- | --- |
| API1 BOLA | customer-scoped query, explicit admin path, cross-customer regression test |
| API2 Broken Authentication | Keycloak, double JWT validation, RS256, issuer/audience/lifetime, production HTTPS |
| API3 Object Property Authorization | dedicated DTO, token-derived CustomerId, unknown JSON members rejected |
| API4 Resource Consumption | rate limits, request/header/body bounds, bounded business inputs, performance baseline |
| API5 Function Authorization | role policies and explicit endpoint authorization |
| API6 Sensitive Business Flows | subject rate limiting and business constraints |
| API7 SSRF | public API accepts no user-controlled remote URL/host |
| API8 Security Misconfiguration | headers, non-root, Trivy config scan, fail-closed config, no default credentials |
| API9 Inventory Management | tested OpenAPI, ADRs and environment-scoped development endpoints |
| API10 Unsafe API Consumption | resilient HTTP defaults and no user-selected upstream endpoints |

## Green security gate

A head is only described as security-green when functional CI, Security and relevant ZAP DAST are green, no known High/Critical finding is silently accepted, and security exceptions are not added merely to make automation pass.

This is not a substitute for independent manual penetration testing.

The sustainable security promise is not "zero vulnerabilities forever"; it is **known classes of defects become explicit controls plus regression tests**.
