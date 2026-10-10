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

**This does not activate the existing AI Evolution workflow:** [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) requires a separately human-governed fix to the protected workflow. Any future integration must run a trusted guard copy that the agent itself cannot modify before committing.

## Consequences

AI assistance becomes inspectable as part of the SDLC while human review and quality gates remain explicit boundaries.
