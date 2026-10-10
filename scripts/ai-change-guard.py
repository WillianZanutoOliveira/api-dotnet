#!/usr/bin/env python3
"""Fail-closed Git change guard for AI engineering tasks.

Run inside an agent worktree before staging, committing or pushing. The
governing GitHub Actions workflow must be updated separately by a human.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import sys

PROTECTED_FILES = frozenset(
    {
        "AGENTS.md",
        "SECURITY.md",
        ".github/CODEOWNERS",
        ".ai/engineering-constitution.md",
        ".ai/prompts/engineer.md",
        ".github/workflows/ai-evolution.yml",
        ".github/workflows/security.yml",
        ".github/workflows/scorecard.yml",
        ".github/workflows/release.yml",
        ".github/workflows/performance.yml",
        ".github/workflows/gitops-promote.yml",
        # A future agent must not be able to rewrite its own guard or CI checks.
        ".github/workflows/ci.yml",
        "scripts/ai-change-guard.py",
        "tests/ai_harness/test_ai_change_guard.py",
    }
)

CHANGE_COMMANDS = (
    ("diff", "--cached", "--name-only", "--no-renames", "-z", "--"),
    ("diff", "--name-only", "--no-renames", "-z", "--"),
    ("ls-files", "--others", "--exclude-standard", "-z", "--"),
)


def git(repo: Path, *arguments: str) -> bytes:
    result = subprocess.run(
        ("git", "-C", str(repo), *arguments),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode:
        message = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(message or f"git exited with {result.returncode}")
    return result.stdout


def inspect_changes(repo: Path) -> set[str]:
    """Include staged, unstaged and untracked paths; expose both ends of renames."""
    root = Path(os.fsdecode(git(repo, "rev-parse", "--show-toplevel").strip()))
    paths: set[str] = set()
    for command in CHANGE_COMMANDS:
        output = git(root, *command)
        paths.update(os.fsdecode(path) for path in output.split(b"\0") if path)
    return paths


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo",
        type=Path,
        default=Path.cwd(),
        help="Working tree to inspect (default: current directory)",
    )
    args = parser.parse_args()

    try:
        paths = inspect_changes(args.repo)
    except (OSError, RuntimeError) as error:
        print(f"AI change guard failed closed: {error}", file=sys.stderr)
        return 2

    blocked = sorted(paths & PROTECTED_FILES)
    if blocked:
        print("AI change guard: protected governance files were changed:", file=sys.stderr)
        for path in blocked:
            print(f"  {path!r}", file=sys.stderr)
        return 1

    if not paths:
        print("AI change guard: task produced no eligible Git changes.", file=sys.stderr)
        return 2

    print(f"AI change guard: {len(paths)} changed path(s), governance intact.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
