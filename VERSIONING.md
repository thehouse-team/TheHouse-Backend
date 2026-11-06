# TheHouse Backend - Versioning and Container Image Policy

## 1. Source of truth

The immutable Git commit SHA is the source of truth for every U2 backend container image.

A container image MUST correspond to exactly one Git commit.

## 2. GHCR image

Canonical image repository:

ghcr.io/thehouse-team/thehouse-backend

U2 images use this immutable tag format:

sha-<full-git-commit-sha>

Example:

ghcr.io/thehouse-team/thehouse-backend:sha-0123456789abcdef0123456789abcdef01234567

The `latest` tag is not used for U2 deployment.

## 3. Deployment identity

After publication, the workflow records the OCI digest:

sha256:<digest>

Deployments MUST prefer the immutable digest reference:

ghcr.io/thehouse-team/thehouse-backend@sha256:<digest>

The Git tag is useful for human traceability.
The digest is the immutable deployment identity.

## 4. Required OCI metadata

Published images must contain at least:

- org.opencontainers.image.source
- org.opencontainers.image.revision
- org.opencontainers.image.version
- org.opencontainers.image.title

The source label links the package to the GitHub repository.

## 5. Publication and deployment gate (NV-09)

Images are published and deployed only by:

.github/workflows/cd.yml

The workflow:

- runs after the `CI` workflow succeeds on a push to `main`, or manually from `main`
- builds the exact commit validated by CI and pushes `sha-<commit>`
- rescans the published digest with Trivy (Critical/High with a fix block)
- signs the digest with Cosign keyless and attests a CycloneDX SBOM
- verifies the image in an ephemeral environment (smoke tests and OWASP ZAP)
- deploys to Azure Container Apps by digest only after approval in the `production` Environment
- verifies the signature again before deploying and rolls back automatically if the production smoke tests fail

Credentials:

- GHCR: GitHub-provided `GITHUB_TOKEN` (`packages: write` only in the publish job)
- Azure: OIDC federated credential bound to the `production` Environment (no client secret)
- Cosign: keyless (Sigstore), signature identity `https://github.com/thehouse-team/TheHouse-Backend/.github/workflows/cd.yml@refs/heads/main`

No personal access token or long-lived secret is stored in the repository.

The previous manual workflow `u2-ghcr-publish.yml` was retired by NV-09 T08.

Verify an image before using it:

```bash
cosign verify ghcr.io/thehouse-team/thehouse-backend@sha256:<digest>   --certificate-identity https://github.com/thehouse-team/TheHouse-Backend/.github/workflows/cd.yml@refs/heads/main   --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## 6. Mutability policy

Do not overwrite an existing SHA tag.

Do not use `latest` for infrastructure.

Do not deploy by a mutable convenience tag.

Every Azure deployment must identify:

- Git commit SHA
- GHCR SHA tag
- OCI digest
- GitHub Actions publication run

## 7. Observability version

When deployed, the application should override:

Observability__ServiceVersion=sha-<git-commit-sha>

This allows telemetry to be associated with the exact deployed build.

## 8. Package visibility

The GHCR package is public (verified 2026-11-05 with an anonymous pull token), consistent with the public repository (M-BE-050). Azure Container Apps pulls it without registry credentials.

Public visibility does not relax the digest, signature or approval rules above.

## 9. U2 evidence

For every image accepted for U2 evidence, record:

- repository
- branch
- commit SHA
- CD workflow run ID and approver(s)
- image tag
- image digest
- publication timestamp
- visibility
- signature identity
- deployment revision, when applicable
- previous image (rollback target)

The `cd-summary` job writes this record to the run summary and to the `deployment-record.json` artifact. Records are added to section 11 through a pull request, because the `main` ruleset does not allow direct pushes.

## 10. Terraform rule

Terraform must not depend on `latest`.

The final Container Apps revision must consume an immutable image identity, preferably the OCI digest.

No GHCR credentials may be committed to Git or embedded in the Docker image.

## 11. Deployment log

| Date | Commit | Digest | CD run | Approver(s) | Revision | Result |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-11-05 (before NV-09) | `c6518862788407fe678c1a524ad4d4b764dc6a71` | `sha256:15f196af9c32fc843cf1723b4615f896668b0368b292e1246807ab0d03194775` | manual (`u2-ghcr-publish.yml`) | — | — | Replaced on 2026-11-06 |
| 2026-11-06 | `84c526e` | `sha256:57f3a640…` | [42174853458](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42174853458) | vicmanujo | — | FAILED before deploy (OIDC subject); production unchanged |
| 2026-11-06 | `ddc552a` | `sha256:33402d6303ec…` | [42178536348](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42178536348) | vicmanujo | `ca-thehouse-u2--cddc552a-2-1` | Rolled back by a workflow bug (health step); restored `sha256:15f196af…` |
| 2026-11-06 | `d1bbbeacfe1207f4c86736e5ed4bc6abe1fe43b6` | `sha256:caa6f417eef39a75ba5fe180cf082b95b501e0ea1cacdb98f69bddbc92db061e` | [42180858665](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42180858665) | vicmanujo | `ca-thehouse-u2--cd1bbbea-3-1` | PASS — running in Azure |
