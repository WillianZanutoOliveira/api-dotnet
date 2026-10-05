# AI Engineering Instructions

This repository is a production-minded distributed-systems portfolio project. AI-assisted changes must make engineering quality more visible, not merely add technology.

## Read first

Before changing code, read:
1. .ai/engineering-constitution.md
2. docs/architecture.md
3. the ADRs under docs/adr
4. the code in the bounded context you are changing

## Architecture invariants

- Preserve service ownership: no service may read another service's database.
- Preserve the Orders Clean Architecture dependency direction.
- Preserve transactional outbox/inbox semantics and idempotent consumers.
- Prefer asynchronous integration events across service boundaries.
- Do not introduce distributed transactions.
- Keep observability vendor-neutral through OpenTelemetry.
- New architecture decisions require an ADR.

## Security invariants

- Never commit credentials, tokens, private keys or real customer data.
- Never trust a customer/user identifier supplied by the request body when it can be derived from authenticated identity.
- Do not disable issuer, audience, lifetime or signature validation to make tests pass.
- Do not weaken authorization policies.
- Local demo credentials must be unmistakably non-production.

## Change discipline

- Keep each task narrowly scoped.
- Add or update tests for behavior changes.
- Run restore, build and tests before considering work complete.
- Update Portuguese and English documentation together when public behavior or architecture changes.
- Do not perform unrelated refactors.
- Do not auto-merge or push directly to main.

## Protected governance files

AI automation must not modify these files unless a human explicitly performs a separate governance change:

- AGENTS.md
- .ai/engineering-constitution.md
- .ai/prompts/engineer.md
- .github/workflows/ai-evolution.yml

## Validation commands

- dotnet restore DistributedCommerce.slnx
- dotnet build DistributedCommerce.slnx --configuration Release --no-restore
- dotnet test DistributedCommerce.slnx --configuration Release --no-build
- docker compose config
