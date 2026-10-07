[🇧🇷 Português](0004-identity-keycloak.md)

# ADR-0004 — Identity and authorization with Keycloak

## Status

Accepted.

## Context

The first Orders API was public and accepted `customerId` from the payload. That did not model a real security boundary because callers could attempt to act on behalf of another customer.

After adding the YARP Gateway, identity also could not become trusted merely because a request came from the edge proxy. The internal service must continue validating identity itself.

## Decision

Use Keycloak as the OpenID Connect/OAuth 2.0 Identity Provider and apply **double JWT validation**:

```text
Client
  |
  v
Keycloak
  |
  | JWT
  v
YARP Gateway
  |
  | validated JWT + rate limiting
  v
Orders API
  |
  | JWT validated again
  v
use case
```

The shared `Security` building block enforces signed RS256 tokens, issuer, audience, lifetime, required expiration, username/role mapping and read/write/admin authorization policies. Authentication error details are not exposed to callers.

Outside `Development`, Authority, Issuer and MetadataAddress must use HTTPS and `RequireHttpsMetadata` cannot be disabled.

## Business identity

Customer identity comes from the token `sub` claim, never from request data.

Unknown JSON properties are rejected, so attempts to inject a `customerId` property are invalid instead of silently ignored.

## Object-level authorization

Order reads have two explicit paths:

- `admin` uses a cross-customer `OrderId` lookup;
- `customer` queries PostgreSQL by `OrderId + CustomerId`.

The normal customer path therefore does not load another customer's resource and then decide in memory whether access is allowed.

Out-of-scope resources return `404`, reducing resource-existence disclosure.

## Abuse protection

The Gateway applies a token bucket partitioned by `sub`.

The local realm enables brute-force protection and short access-token lifetime.

CI verifies unauthenticated/tampered token rejection, cross-customer isolation, admin access and HTTP 429 under burst load.

## Local environment

Docker Compose/Aspire run Keycloak in development mode with a versioned importable realm.

Resource Owner Password Credentials on `commerce-cli` exists **only** for disposable smoke/DAST fixtures. Real interactive clients should use Authorization Code + PKCE.

Local HTTP is loopback-only. Application Production mode rejects non-HTTPS Keycloak metadata.

## Production

Keycloak should run in `start` production mode with TLS and explicit hostname configuration.

Where practical, the administration UI/API should use a separate administrative hostname or network from public identity endpoints.

Demo users/passwords and the local realm are not production identity configuration.

## Consequences

Benefits include token-derived identity, defense in depth, verifiable RBAC/BOLA controls, reduced resource-existence disclosure and fail-fast production HTTPS requirements.

Trade-offs include an additional operational dependency and the need to operate IdP/JWKS availability as part of the platform.

See also [security posture and pentest readiness](../security-posture.en.md).
