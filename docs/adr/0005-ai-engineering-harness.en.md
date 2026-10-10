[🇧🇷 Português](0005-ai-engineering-harness.md)

# ADR-0005 — AI-assisted engineering harness

## Status

Accepted.

## Context

Coding agents can modify files and execute the build/test loop, but unbounded automation can introduce regressions, weaken architecture decisions or modify the governance mechanism itself.

The project should demonstrate engineering automation without turning the AI agent into an unrestricted actor on the main branch.

## Decision

Add an AI Engineering Harness powered by Codex CLI and GitHub Actions.

The harness:
- is manually started with a small explicit task;
- reads AGENTS.md and the engineering constitution;
- can edit the job workspace;
- cannot modify its own governance files;
- must pass restore, build, tests and Docker Compose validation;
- creates an ai/evolution-* branch and pull request;
- never auto-merges.

## Credentials

The first stage uses the repository OPENAI_API_KEY secret. The credential is never written to source or generated artifacts.

A future evolution can replace long-lived credentials with workload identity federation.

## Security evolution — preparatory guard

CI exercises a governance change guard with isolated Git fixtures. It detects committed changes since a trusted immutable base SHA, as well as staged, unstaged and untracked changes, including protected-file renames and deletions. The guard also protects all files under `.github/workflows/`, `.ai/`, `docs/governance/`, and `tests/ai_harness/`, preventing new workflows or attempts to weaken its own governance checks. See [scripts/ai-change-guard.py](../../scripts/ai-change-guard.py) and [tests/ai_harness](../../tests/ai_harness/test_ai_change_guard.py).

## Approved evolution — workflow installed (October 10, 2026)

After the guard merged in [PR #17](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17), the proposal from [PR #18](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/18) was applied to the protected workflow through a separate human-approved governance change in [PR #19](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/19) (`d5f76b272d3b02826bd7b16eb4d032412bc5a015`). Governance integration is **complete**; the trusted guard is now part of the installed workflow.

The amended decision uses three isolated runners: `engineer` and `validate` have `contents: read`; `publish` has only the write permissions needed to open a PR and dispatch checks. Independently validated patches are bound by SHA-256, with human review and no auto-merge/deploy. CI, Security, and DAST are explicitly dispatched on the generated branch.

**Acceptance status:** code integration and governance-PR checks completed; the first real `workflow_dispatch` using Codex and publishing a PR has not yet been demonstrated. [Issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) must stay open until an end-to-end smoke test succeeds. See the [operations guide](../ai-first-operations.en.md).

## Consequences

AI assistance becomes inspectable as part of the SDLC while human review and quality gates remain explicit boundaries.
