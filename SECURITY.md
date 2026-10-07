# Security Policy

## Supported version

This repository is an engineering reference project. Security fixes are applied to the default branch (`main`).

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, tokens, private keys, personal data or other sensitive material.

Use GitHub private vulnerability reporting when it is enabled. If private reporting is unavailable, open only a minimal public issue requesting a private security contact and do not include exploit details.

## Security target

The project does **not** claim that a penetration test can never find a vulnerability.

The merge target is measurable instead:

- no known High/Critical findings in enforced automated gates;
- no broken authentication or authorization regression;
- no default or committed production credentials;
- no runtime database DDL privilege;
- no security exception added only to silence a scanner.

See [Security posture and pentest readiness](docs/security-posture.en.md).

## Security model

The repository demonstrates:

- Keycloak OpenID Connect / OAuth 2.0 identity;
- JWT validation at YARP and Orders;
- issuer, audience, signature, lifetime and RS256 validation;
- production HTTPS enforcement for identity metadata;
- resource-level authorization with customer-scoped database queries;
- subject-partitioned rate limiting;
- strict JSON/input and HTTP request bounds;
- security response headers and implementation-header suppression;
- HashiCorp Vault policies scoped per workload;
- dynamic PostgreSQL credentials with TTL and renewable leases;
- separate Vault/PostgreSQL identities for DML runtime and DDL migrations;
- non-root application containers;
- CodeQL, Gitleaks, Trivy vulnerability/secret/IaC checks;
- authenticated OWASP ZAP API DAST;
- SBOM and OpenSSF Scorecard automation;
- human-reviewed AI-assisted changes.

Local Keycloak password grant, Vault dev mode/root token and local demo credentials are fixtures only. They must never be treated as production configuration.

## Security automation

Relevant changes are expected to pass:

1. static security invariants;
2. build/analyzers/tests;
3. authentication and object-authorization smoke tests;
4. adversarial HTTP/input/rate-limit smoke tests;
5. CodeQL;
6. Gitleaks;
7. Trivy vulnerabilities/secrets;
8. Trivy infrastructure misconfiguration scan;
9. authenticated OWASP ZAP API scan when the DAST workflow applies;
10. SPDX SBOM generation.

A manual penetration test is still recommended before exposing a real production deployment or processing sensitive/financial data.

## Secret handling

Never commit:

- real API keys;
- production passwords;
- Vault tokens;
- private keys;
- cloud credentials;
- customer or personal data.

The `.env` and `.aspire/` local state are excluded from version control.
