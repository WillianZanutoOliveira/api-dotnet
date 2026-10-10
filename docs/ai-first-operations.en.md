[🇧🇷 Português](ai-first-operations.md)

# Secure AI-First agent operations

## Automation status

The protected `.github/workflows/ai-evolution.yml` is **awaiting a separate human-governed fix** tracked in [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15). The current workflow has an unterminated shell quote and duplicate steps. **Do not treat autonomous agent runs as operational** until a maintainer fixes the workflow separately and verifies a controlled execution.

The change guard `scripts/ai-change-guard.py` and its regression tests are a **verified preparatory component**. CI exercises the tests, but the AI Evolution workflow does not invoke the guard yet; changing that protected workflow requires human governance review.

## Local verification

Requires Python 3 and Git, with no third-party Python dependencies:

```bash
python3 -m unittest discover -s tests/ai_harness -p 'test_*.py' -v
BASE_SHA="$(git rev-parse HEAD)" # capture BEFORE starting the agent
# After the agent runs, use a trusted copy of the guard:
python3 /trusted/path/ai-change-guard.py --repo . --base-sha "$BASE_SHA"
```

Run the second command in the agent's modified worktree **before** `git add`, commit, or push:

| Exit code | Meaning |
| --- | --- |
| 0 | Changes exist; no protected governance path was touched |
| 1 | Protected path changed, created, removed, or renamed |
| 2 | No eligible changes or Git failure; fail closed |

The guard inspects **committed changes since the trusted base, staged, unstaged, and untracked** changes with NUL-separated filenames and `--no-renames` to ensure renaming a protected file cannot hide its deletion. The base SHA must be a complete immutable commit hash captured before the agent runs; a mutable branch like `origin/main` is not an adequate trust anchor. It protects `AGENTS.md`-listed governance files as well as its own implementation/tests and `ci.yml`.

## Required trust boundary

**Do not execute a guard copy that the agent can rewrite**. The governance workflow maintainer must execute a trusted copy taken from `main` **before** Codex runs, or from a separate trusted job. If the agent modifies `scripts/ai-change-guard.py`, that trusted copy must reject the modification. This guard and its tests do not replace independent security jobs or human review.

## Pending protected-workflow fix

Only a maintainer, through a separate governance PR, should:

1. Fix the unterminated quote and remove duplicate Restore/Build/Test/Create PR steps.
2. Use a trusted guard copy from before agent execution; inspect changes **before staging, committing, or pushing** and again before PR creation.
3. Run restore, Release build, tests, `dotnet format --verify-no-changes`, `scripts/security-config-check.sh`, and Compose validation. Full smoke/security checks remain mandatory on the resulting PR.
4. Keep immutable SHA-pinned Actions, least-privilege permissions, `OPENAI_API_KEY` isolation, and exactly one branch/PR per task.
5. Never push to `main` or auto-merge. Test `workflow_dispatch` with a harmless documentation task and obtain human review.

The target is **reviewable delivery**, not unrestricted automation. See [ADR-0005](adr/0005-ai-engineering-harness.en.md), [AGENTS.md](../AGENTS.md), and the [engineering constitution](../.ai/engineering-constitution.md).
