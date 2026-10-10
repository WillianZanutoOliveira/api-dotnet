# AI-First governance change proposal

[Português](README.pt-BR.md)

**Status:** Review proposal only. This directory is not a GitHub Actions workflow entry point. [Issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) tracks the separate human-owned change to the protected file `.github/workflows/ai-evolution.yml`.

## Scope and rationale

The proposed workflow at [ai-evolution-proposed.yml](ai-evolution-proposed.yml) replaces the broken, duplicate existing AI Evolution steps with **three separately permissioned jobs**:

1. **Engineer:** checks out the immutable dispatch commit without persisted repository credentials, runs Codex with only `OPENAI_API_KEY`, exports a binary diff and uploads an immutable short-lived artifact. The Codex job has no repository write token.
2. **Validate:** downloads the diff into a new runner with read-only repository permissions, saves a trusted guard copy **before** patch application, checks committed/staged/new/renamed/deleted protected paths (including all of `.ai/`, `.github/workflows/`, `docs/governance/`, and `tests/ai_harness/`), runs Python guard tests, .NET restore/build/format/tests, static security checks and Docker Compose validation, and publishes the verified patch SHA-256.
3. **Publish:** runs only after successful validation, uses the same immutable source SHA, checks the artifact digest, re-applies the patch, re-runs the trusted guard, and uses its scoped GitHub token to push **one non-main branch and open one PR**, then explicitly dispatches CI, Security and DAST on that branch. There is no auto-merge.

All third-party GitHub Actions references are full immutable commits. The proposal retains one task per manual `workflow_dispatch`, branch `ai/evolution-<run>-<attempt>`, serialized executions, no production deployment, no secret printing, and human-reviewed promotion.

## Maintainer-only activation procedure

The proposal should **not** be copied or activated by Codex or by an autonomous PR automation. A maintainer must:

1. Review/merge [PR #17](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17) so the trusted guard exists in `main`.
2. Review this proposal and its test `tests/ai_harness/test_ai_evolution_proposal.py`; ensure the proposal CI passes.
3. Make a **separate governance branch** from current `main`, manually copy the reviewed `docs/governance/ai-evolution-proposed.yml` to the protected `.github/workflows/ai-evolution.yml` and verify the exact diff. Do not let the agent modify it.
4. Validate YAML and GitHub Actions structure locally using Ruby and `actionlint` if available:
   ```bash
   ruby -e 'require "yaml"; YAML.parse_file(".github/workflows/ai-evolution.yml")'
   actionlint .github/workflows/ai-evolution.yml
   python3 -m unittest discover -s tests/ai_harness -p 'test_*.py' -v
   ```
5. Open a **governance-only PR** with appropriate human review; run the standard CI and Security checks and do not auto-merge.
6. Configure `OPENAI_API_KEY` as a repository Actions secret; validate repository/organization permissions for GitHub Actions `contents: write` and `pull-requests: write` in the isolated publisher job.
7. Once a maintainer integrates the governance change, use `workflow_dispatch` **on `main`** for a harmless docs-only task and inspect the generated PR. Confirm the API credential is never exposed, the agent never receives GitHub write credentials, protected paths cannot be changed, all validator gates are green, and no automatic merge occurs.

**GitHub Actions caveat:** PRs created by the default `GITHUB_TOKEN` may leave `pull_request` workflows waiting for approval. To reduce that dependency, the proposed isolated publisher has `actions: write` in addition to its branch/PR permissions and explicitly runs `gh workflow run` for CI, Security and DAST on the generated branch. `workflow_dispatch` events are permitted even when triggered with `GITHUB_TOKEN`. A maintainer must still verify **all three workflows completed successfully** and approve any waiting workflow runs before considering the PR for merge.

## Limitations

This is not an approval to run autonomous changes without supervision. The model may generate unsafe application code, and automatic static checks are not a replacement for code review, RBAC checks, threat modeling, environment separation, manual governance, or independent security scans. The demo workflow should not have production secrets or production-deploy permissions.


## Activation acceptance criteria

The maintainer must confirm evidence from an actual GitHub Actions demonstration run: the implementation job has no repository write credentials, the patch artifact's SHA-256 matches the independently validated digest, guard tests and authorization boundaries pass, precisely one PR is opened, human review is required, and **main never changes without a separately approved merge**. In separate isolated regression tests, the guard must reject newly created workflows, already committed governance changes, deletions, renames and untrusted base SHAs.

A valid YAML file alone does **not** mean the workflow is operational. A real controlled execution must succeed, and explicitly dispatched CI/Security/DAST results must be confirmed successful before merge.
