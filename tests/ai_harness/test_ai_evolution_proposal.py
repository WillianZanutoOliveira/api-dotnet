"""Static checks for the human-governed AI Evolution workflow proposal."""

from __future__ import annotations

from pathlib import Path
import re
import shutil
import subprocess
import unittest


REPO = Path(__file__).resolve().parents[2]
PROPOSAL = REPO / "docs/governance/ai-evolution-proposed.yml"
ACTIVE = REPO / ".github/workflows/ai-evolution.yml"


class AiWorkflowGovernanceProposalTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = PROPOSAL.read_text(encoding="utf-8")

    @unittest.skipUnless(shutil.which("ruby"), "Ruby YAML parser not installed")
    def test_documented_workflow_yaml_is_syntactically_valid(self) -> None:
        result = subprocess.run(
            ("ruby", "-e", 'require "yaml"; YAML.parse_file(ARGV.first)', str(PROPOSAL)),
            capture_output=True,
            check=False,
            text=True,
        )
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_proposal_is_not_live_workflow(self) -> None:
        self.assertTrue(PROPOSAL.is_file())
        self.assertTrue(ACTIVE.is_file())
        self.assertNotEqual(PROPOSAL, ACTIVE)
        self.assertIn("GOVERNANCE PROPOSAL ONLY", self.workflow)

    def test_three_separate_jobs_have_distinct_permission_boundaries(self) -> None:
        matches = re.findall(r"^  (engineer|validate|publish):$", self.workflow, re.MULTILINE)
        self.assertEqual(matches, ["engineer", "validate", "publish"])
        engineer = self.workflow.split("  engineer:", 1)[1].split("  validate:", 1)[0]
        validator = self.workflow.split("  validate:", 1)[1].split("  publish:", 1)[0]
        publisher = self.workflow.split("  publish:", 1)[1]
        self.assertIn("contents: read", engineer)
        self.assertNotIn("contents: write", engineer)
        self.assertNotIn("pull-requests: write", engineer)
        self.assertNotIn("contents: write", validator)
        self.assertIn("contents: write", publisher)
        self.assertIn("pull-requests: write", publisher)
        self.assertIn("actions: write", publisher)
        self.assertNotIn("actions: write", engineer)
        self.assertNotIn("actions: write", validator)
        self.assertIn("needs: validate", publisher)

    def test_checkout_disables_persisted_credentials_in_every_job(self) -> None:
        self.assertEqual(self.workflow.count("persist-credentials: false"), 3)

    def test_every_action_is_commit_pinned(self) -> None:
        action_refs = re.findall(r"^\s+uses:\s+(\S+)", self.workflow, re.MULTILINE)
        self.assertGreaterEqual(len(action_refs), 7)
        for ref in action_refs:
            with self.subTest(action=ref):
                self.assertRegex(ref, r"^actions/[a-z-]+@[0-9a-f]{40}$")

    def test_agent_does_not_have_repository_publish_token(self) -> None:
        engineer = self.workflow.split("  engineer:", 1)[1].split("  validate:", 1)[0]
        self.assertNotIn("GH_TOKEN:", engineer)
        self.assertIn("OPENAI_API_KEY:", engineer)
        self.assertNotIn("gh pr create", engineer)
        self.assertNotIn("git push", engineer)

    def test_validator_and_publisher_reinspect_using_trusted_base_copy(self) -> None:
        validator = self.workflow.split("  validate:", 1)[1].split("  publish:", 1)[0]
        publisher = self.workflow.split("  publish:", 1)[1]
        for job in (validator, publisher):
            with self.subTest(job=job[:16]):
                self.assertIn("cp scripts/ai-change-guard.py", job)
                self.assertIn("trusted-guard.py", job)
                self.assertIn('git apply --check --index', job)
                self.assertIn('--base-sha "$GITHUB_SHA"', job)
                self.assertLess(job.index("cp scripts/ai-change-guard.py"), job.index('git apply --index'))
                self.assertLess(job.index('git apply --index'), job.index('trusted-guard.py" --repo'))

    def test_validation_includes_required_quality_gates(self) -> None:
        validator = self.workflow.split("  validate:", 1)[1].split("  publish:", 1)[0]
        for required in (
            "dotnet restore DistributedCommerce.slnx",
            "dotnet build DistributedCommerce.slnx --configuration Release",
            "dotnet format DistributedCommerce.slnx --verify-no-changes",
            "dotnet test DistributedCommerce.slnx --configuration Release",
            "bash scripts/security-config-check.sh",
            "docker compose -f docker-compose.yml -f docker-compose.vault.yml config",
            "python3 -m unittest discover -s tests/ai_harness",
        ):
            with self.subTest(gate=required):
                self.assertIn(required, validator)

    def test_publisher_binds_patch_to_validated_digest(self) -> None:
        self.assertIn("patch_sha256:", self.workflow)
        self.assertIn("VERIFIED_SHA256:", self.workflow)
        self.assertIn('if [ -z "$VERIFIED_SHA256" ] || [ "$actual" != "$VERIFIED_SHA256" ]', self.workflow)

    def test_publisher_explicitly_dispatches_normal_quality_gates(self) -> None:
        publisher = self.workflow.split("  publish:", 1)[1]
        self.assertIn("for workflow in ci.yml security.yml dast.yml; do", publisher)
        self.assertIn('gh workflow run "$workflow" --repo "$GITHUB_REPOSITORY" --ref "$BRANCH"', publisher)
        self.assertLess(publisher.index("gh pr create"), publisher.index("gh workflow run"))

    def test_dispatched_workflows_accept_manual_dispatch(self) -> None:
        for filename in ("ci.yml", "security.yml", "dast.yml"):
            with self.subTest(workflow=filename):
                workflow_path = REPO / ".github" / "workflows" / filename
                workflow = workflow_path.read_text(encoding="utf-8")
                self.assertRegex(workflow, r"(?m)^\s*workflow_dispatch:")

    def test_no_automatic_merge_or_main_push(self) -> None:
        self.assertNotRegex(self.workflow, r"(?m)^\s*(gh pr merge|git push origin main|git push --force)")
        self.assertIn("gh pr create", self.workflow)
        self.assertIn('BRANCH="ai/evolution-', self.workflow)
        self.assertIn("Human review", self.workflow)


if __name__ == "__main__":
    unittest.main()
