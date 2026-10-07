[🇧🇷 Português](0010-software-supply-chain.md)

# ADR-0010 — Software supply-chain automation and attested releases

## Status

Accepted.

## Context

CodeQL, Trivy and SBOM generation already inspect the repository, but a mature delivery chain should also reduce risk inside CI itself and allow consumers to verify where release artifacts came from.

Mutable action tags create an indirect dependency between a workflow and the future state of that tag.

## Decision

Adopt four additional controls.

### 1. Commit-pinned actions

CI, Security and AI Harness dependencies are referenced by immutable commit SHA with a human-readable version comment.

Dependabot remains responsible for future update proposals.

### 2. OpenSSF Scorecard

`.github/workflows/scorecard.yml` runs weekly and on changes to `main`.

Results are emitted as SARIF, retained as a temporary artifact and uploaded to GitHub Code Scanning.

Scorecard complements rather than replaces CodeQL and Trivy.

### 3. Ownership and security policy

`.github/CODEOWNERS` makes code/platform/security ownership explicit.

`SECURITY.md` documents responsible vulnerability reporting and secret-handling expectations.

### 4. OCI releases with provenance

A `v*` tag triggers `.github/workflows/release.yml`.

The workflow:

1. builds the Gateway and four workloads;
2. publishes versioned OCI images to GHCR;
3. adds an immutable-SHA-oriented tag;
4. creates a cryptographic build attestation for every image digest;
5. uses GitHub OIDC/Sigstore without a long-lived signing private key.

The attestation binds to the OCI digest rather than only to a mutable tag.

## Permissions

The release job explicitly receives only the capabilities it needs:

- `contents: read`;
- `packages: write`;
- `id-token: write`;
- `attestations: write`;
- `artifact-metadata: write`.

## Verification

Consumers can verify image provenance with GitHub CLI:

```bash
gh attestation verify oci://ghcr.io/<owner>/<repo>/orders:<version> \
  --repo <owner>/<repo>
```

Attestation verification should be part of a real consumption policy. Provenance establishes origin and build context; it is not a vulnerability-free guarantee.

## Consequences

### Benefits

- reduced mutable-action risk;
- continuous OpenSSF posture assessment;
- verifiable provenance;
- clearer dependency transparency;
- version + SHA release traceability;
- visible DevSecOps and supply-chain engineering evidence.

### Trade-offs

- releases create additional GHCR artifacts;
- attestation permissions exist only in the release workflow;
- GitHub branch rulesets still require repository-admin configuration;
- provenance does not replace vulnerability scanning.
