[🇧🇷 Português](0004-identity-keycloak.md)

# ADR-0004 — Identity and authorization with Keycloak

## Status

Accepted.

## Context

The Orders API was public and accepted `customerId` directly from the payload. That was useful for the first architecture demo, but it did not model a real security boundary: a caller could attempt to create or read resources on behalf of another user.

## Decision

Use Keycloak as the OpenID Connect Identity Provider and validate JWT access tokens at the Orders API HTTP boundary.

The shared `Security` building block centralizes:

- signature, issuer, audience and lifetime validation;
- `preferred_username` mapping;
- realm roles flattened into a `roles` claim;
- authorization policies for read, write and administration.

The business customer identifier now comes from the token `sub` claim instead of the request body.

Order reads apply object-level authorization:

- `customer` can only read orders whose `CustomerId` matches their own `sub`;
- `admin` can read orders across customers.

## Local environment

Docker Compose starts Keycloak in development mode and imports a versioned realm so the demo remains reproducible.

Direct password grant is enabled **only** for local smoke-test users. Real interactive clients should use Authorization Code + PKCE.

## Consequences

Benefits:

- authentication and authorization become explicit architectural concerns;
- the API no longer trusts identity supplied in a payload;
- RBAC and object-level authorization are demonstrable;
- identity configuration becomes reproducible in CI.

Trade-offs:

- Keycloak adds an operational dependency;
- `start-dev` and demo users are not production configuration;
- production requires TLS, secret management, rotation policies and a hardened IdP setup.
