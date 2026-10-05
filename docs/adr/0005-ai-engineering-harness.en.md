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

## Consequences

AI assistance becomes inspectable as part of the SDLC while human review and quality gates remain explicit boundaries.
