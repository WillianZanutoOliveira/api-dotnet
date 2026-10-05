# Engineering Constitution for AI-Assisted Evolution

## Purpose

The AI harness exists to accelerate small, reviewable improvements while keeping human control over architecture, security and merge decisions.

## Non-negotiable rules

1. Human-reviewed delivery — AI may create a branch and pull request. It never merges its own work.
2. Green quality gates — build and automated tests must pass before a pull request is created.
3. Least privilege — automation receives only the repository permissions required to create its branch and PR.
4. Architecture preservation — service ownership, Clean Architecture boundaries, outbox/inbox and idempotency must not be weakened.
5. Security preservation — authentication and authorization validation cannot be disabled for convenience.
6. No secrets in source — credentials belong in secret stores or explicitly local demo fixtures.
7. Small diffs — one task should result in one coherent change, not broad opportunistic refactoring.
8. Explain important decisions — material architectural choices require an ADR.
9. Bilingual public docs — public Portuguese and English documentation evolve together.
10. Governance cannot rewrite itself — the agent cannot edit the harness policy or workflow that governs it.

## Pull-request expectation

An AI-generated PR should explain:
- requested outcome;
- files changed;
- architecture/security impact;
- validation performed;
- remaining trade-offs or follow-up work.

The reviewer remains responsible for correctness and merge approval.
