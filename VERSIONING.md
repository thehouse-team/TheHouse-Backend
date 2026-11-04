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

## 5. Publication gate

U2 image publication is manual.

The workflow:

.github/workflows/u2-ghcr-publish.yml

requires:

- branch `feature/u2-entrega`
- explicit workflow_dispatch
- explicit `PUBLISH` confirmation
- `packages: write`
- GitHub-provided `GITHUB_TOKEN`

No personal access token is stored in the repository.

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

The publication workflow does not change package visibility.

The first publication must be reviewed before any visibility change.

Do not make the GHCR package public merely to simplify deployment without an explicit architecture/security decision.

## 9. U2 evidence

For every image accepted for U2 evidence, record:

- repository
- branch
- commit SHA
- publication workflow run ID
- image tag
- image digest
- publication timestamp
- visibility
- deployment revision, when applicable

## 10. Terraform rule

Terraform must not depend on `latest`.

The final Container Apps revision must consume an immutable image identity, preferably the OCI digest.

No GHCR credentials may be committed to Git or embedded in the Docker image.
