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
| 2026-11-06 | `d1bbbeacfe1207f4c86736e5ed4bc6abe1fe43b6` | `sha256:caa6f417eef39a75ba5fe180cf082b95b501e0ea1cacdb98f69bddbc92db061e` | [42180858665](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42180858665) | vicmanujo | `ca-thehouse-u2--cd1bbbea-3-1` | PASS — replaced on 2026-11-06 |
| 2026-11-06 | `55feed2` | `sha256:4af4456d…` | [42192269362](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42192269362) | vicmanujo | `ca-thehouse-u2--c55feed2-7-1` | PASS — NV-10 (refresh y /users/me) |
| 2026-11-06 | `1c9d73f` | `sha256:246d086a…` | [42193975974](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42193975974) | vicmanujo | `ca-thehouse-u2--c1c9d73f-8-1` | PASS |
| 2026-11-07 | `0c8b477` | `sha256:506208c5…` | [42348699940](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42348699940) | vicmanujo | `ca-thehouse-u2--c0c8b477-9-1` | PASS — catálogo regional y `nosniff` |
| 2026-11-07 | `0c8b477` | — | [42349310343](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42349310343) | IsaiAxel | `ca-thehouse-u2--c0c8b477-10-1` → `rb-10-1` | ROLLED BACK on purpose (NV-09 T13); running `sha256:506208c5…` |
| 2026-11-07 | `32f9ea00d0d03eb676b8dc0e742cdac361c44f4b` | `sha256:27b71e88ecaf7751c64f09a17c7d360f7969632d06b909acd1bb719072918985` | [42354881476](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42354881476) | vicmanujo | `ca-thehouse-u2--c32f9ea0-13-1` | PASS — PR #15: feat(nv-15,nv-17): favoritos y visitas v1 para Mobile |
| 2026-11-07 | `88599c29e91c934f728d49f744582038298b39b4` | `sha256:660118cf994abac4ffdf325d2790e3fbc7d2f97a1274d74b3edbdaf88c47576a` | [42393781540](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42393781540) | vicmanujo | `ca-thehouse-u2--c88599c2-16-1` | PASS — PR #18: feat(nv-11): perfil, universidad, preferencias y búsqueda de universidades |
| 2026-11-07 | `13db0e69888b9c7d13edd1d8c49cbb5ff2f8e208` | `sha256:9d55034fa82538cdc18d71a3328ea35cadf688cc42ae64b47cd31f304ca94611` | [42400488050](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42400488050) | vicmanujo | `ca-thehouse-u2--c13db0e6-19-1` | PASS — PR #21: feat(nv-12): modo arrendador, edificios y cuartos |
| 2026-11-07 | `7310ab0f448c9cd5e4ff0bb5203fde225bced60d` | `sha256:f37ff11f16cd8f2c3345fc7ca6cfd1157ba099d361c971109307dc2abd176a77` | [42403433110](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42403433110) | vicmanujo | `ca-thehouse-u2--c7310ab0-22-1` | PASS — PR #24: feat(nv-21): verificación de edificios, reportes y moderación |
| 2026-11-07 | `f39aa59fb4fc108a1efe9e5c2c02abaab27a791b` | `sha256:be6ac4f66f444b7463e2e8ab05640029f6df08b865e7b962c00f5bd24f57a050` | [42407348366](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42407348366) | vicmanujo | `ca-thehouse-u2--cf39aa59-25-1` | PASS — PR #27: feat(nv-14): búsqueda de cuartos, mapa, conteo, detalle y colonias |
| 2026-11-09 | `14f4ff2dc0b4c1aa83867e45d7eb96008e6ab211` | `sha256:8b7261623b69db3efa1e2824f2959e69e814a8b95f93281cb1be95eba65463ba` | [42680553139](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42680553139) | vicmanujo | `ca-thehouse-u2--c14f4ff2-29-1` | PASS — PR #30: feat(NV-15/NV-17): favoritos y visitas sobre cuartos publicados (1.2.0) |
| 2026-11-10 | `00839f8a6b2c6169295fcbadff975a873029ada0` | `sha256:2948a64f2fe5cdf8ac443a581a2eac7b2b131f50499796aa81be853c871b332b` | [42932081749](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42932081749) | vicmanujo | `ca-thehouse-u2--c00839f8-35-1` | PASS — PR #36: feat(NV-16): solicitudes de interés, bandejas y chat por consulta incremental |
| 2026-11-12 | `00405b3aa3c0e7fb3681b0b9c7777c2ad1bce934` | `sha256:ae07ab6d1a1086812699d466f189ee6f7cd67749ff36499f78ae3dd8cdec9c61` | [43272131477](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/43272131477) | vicmanujo | `ca-thehouse-u2--c00405b3-38-1` | PASS — PR #40: fix(security): actualizar .NET a 10.0.15 (CVE-2026-114146/114147/114149) |
| 2026-11-12 | `6dcf73da054a00418ae1c728e61a850ec3f1b764` | `sha256:d0a2b641a95c381541bf059d8ba216cf1fe8535f9ea53ae901c6e795b5b56d6c` | [43308222051](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/43308222051) | vicmanujo | `ca-thehouse-u2--c6dcf73d-41-1` | PASS — PR #43: feat(NV-18): cierre de trato con QR, Mi renta y fin de renta (1.1.0) |
| 2026-11-13 | `ebb8deb59468dc2a1313e49e02d47a24d79106d4` | `sha256:11d7f6122ef9e92df0e06838f2127ce64911ae5d29a5ea6231190efba6a26e9c` | [43525078903](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/43525078903) | vicmanujo | `ca-thehouse-u2--cebb8deb-43-1` | PASS — PR #46: feat(NV-20): bandeja de notificaciones (1.1.0) |
| 2026-11-17 | `4f677e24295c3816469e1251a0c57a7c10e0b9ca` | `sha256:31f2ccf1c09ea623c64617ba9e569ca060458e93916ab140a99d5fd3cad4e8c7` | [44179627187](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/44179627187) | vicmanujo | `ca-thehouse-u2--c4f677e2-45-1` | PASS — PR #49: feat(NV-19): reseñas y calificaciones (1.1.0) |
| 2026-11-17 | `011c2ad20e60231941e7c8f3edd2d9ec37e7a404` | `sha256:988c0f5c12d91dee83b5a43865b06cb52a73117480f1d91ca8f9c808fbd0ed03` | [44200967522](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/44200967522) | vicmanujo | `ca-thehouse-u2--c011c2ad-48-1` | PASS — PR #53: feat(NV-13): fotografías y documentos de verificación (1.1.0) |
| 2026-11-17 | `31614c54864d415811a595ea048829db04733eeb` | `sha256:aa1a56f7b8c71e8c18921c8217ad7b012dc691d88430093c2f2ad118bce2bf2f` | [44210636026](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/44210636026) | vicmanujo | `ca-thehouse-u2--c31614c5-51-1` | PASS — PR #56: feat(NV-22): búsqueda de compañero de vivienda (1.1.0) |
| 2026-11-17 | `aeb40ef57c130f81f5cb26b8df2fd2537a8bdee8` | `sha256:b852c1a5ab64f9d94ea7f8eae75f92ec6ba85cdd7574ba3e81a9cc7a7fcaef09` | [44222853524](https://github.com/thehouse-team/TheHouse-Backend/actions/runs/44222853524) | vicmanujo | `ca-thehouse-u2--caeb40ef-54-1` | PASS — PR #59: feat(NV-25): estadísticas del arrendador (1.1.0) |

Rows from 2026-11-07 onward come from each run's `deployment-record.json` artifact (NV-09 T17, 2026-11-17). Documentation-only merges run `resolve` in `skip` mode and do not appear here (NV-09 T20).
