[🇧🇷 Português](0013-customers-pf-pj-brasilapi-cep.md)

# ADR-0013 — Individual/company registry and BrasilAPI postal-code lookup

## Status

Accepted.

## Context

The platform needs a registration bounded context capable of representing Brazilian individuals and companies without coupling Orders to profile data or sharing tables. Registration contains PII and must preserve the authentication, secrets, migration and database-per-service guarantees already used by the platform.

Brazilian addresses benefit from CEP-assisted filling, but coupling a provider directly to the domain would make business code and tests depend on an external API.

## Decision

Create the **Customers** bounded context using Clean Architecture:

```text
Customers.Domain
      ↑
Customers.Application
      ↑
Customers.Infrastructure
      ↑
Customers.Api
```

The `Customer` aggregate distinguishes:

- `Individual`: CPF, name, birth date, e-mail and phone;
- `Company`: CNPJ, trade name, legal name, foundation date, state/municipal registrations, e-mail and phone;
- 1–10 addresses with exactly one primary address.

CPF and CNPJ are normalized and check-digit validated locally. The document has a unique database index. Person type is immutable after creation.

Customers owns PostgreSQL, with `customers_runtime` for DML and `customers_migrator` for DDL. Vault issues independent ephemeral logins for runtime and migration.

Registration routes require JWT authentication and the `admin` role. The Gateway validates the token at the edge and Customers validates it again.

For CEP lookup, the application defines `IPostalCodeLookup`; infrastructure implements it with **BrasilAPI CEP v2**. The adapter uses HTTPS, the resilient platform HTTP client, a timeout, address fields, IBGE city code and optional coordinates.

CPF/CNPJ values are **never sent to external services**.

## API

```text
POST   /api/customers
GET    /api/customers
GET    /api/customers/{id}
PUT    /api/customers/{id}
DELETE /api/customers/{id}
GET    /api/customers/address/cep/{cep}
```

List queries are paginated and support text, document and person-type filters.

## Consequences

### Positive

- PII is isolated in its own bounded context and database;
- deterministic CPF/CNPJ validation has no external dependency;
- the CEP provider is replaceable without changing domain/application code;
- Customers follows the same Vault/migration/least-privilege posture as the platform;
- CRUD is RBAC protected and exercised through the Gateway;
- CEP v2 can enrich addresses with IBGE data and coordinates when available.

### Trade-offs

- BrasilAPI is an external dependency and can be unavailable;
- CEP coordinates do not identify an exact residence and may be missing or inaccurate;
- physical deletion should be revisited if legal/audit requirements require retention or anonymization;
- customer registration is not currently an automatic source of Keycloak identities.

## Validation

- domain tests for CPF/CNPJ and address invariants;
- BrasilAPI adapter tests with fixtures and missing coordinates;
- authenticated PF/PJ CRUD smoke test;
- architecture tests;
- CodeQL, Trivy, secret scanning and authenticated CRUD DAST.
