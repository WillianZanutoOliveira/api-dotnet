# Security Policy

## Supported version

This repository is an engineering reference project. Security fixes are applied to the default branch (`main`).

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, tokens, private keys, personal data or other sensitive material.

Use GitHub's private vulnerability reporting feature when it is enabled for this repository. If private reporting is unavailable, open a minimal public issue requesting a private security contact without including exploit details.

## Security model

The repository intentionally demonstrates:

- OpenID Connect and JWT validation with Keycloak;
- resource-level authorization;
- HashiCorp Vault policies scoped per workload;
- dynamic PostgreSQL credentials with TTL and renewable leases;
- no effective database connection string in application-container environment variables;
- CodeQL, Trivy, SBOM and OpenSSF Scorecard automation;
- human-reviewed AI-assisted changes.

Local credentials and local Vault development mode are fixtures only and must never be treated as production configuration.

## Secret handling

Never commit:

- real API keys;
- production passwords;
- Vault tokens;
- private keys;
- cloud credentials;
- customer or personal data.

The `.env` and `.aspire/` local state are excluded from version control.
