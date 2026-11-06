# NV-09 — Task list de entrega continua (CD) U2

| Campo | Valor |
| --- | --- |
| ID | NV-09 |
| Documento | `task.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA» T01–T14; decisiones de la §7 aceptadas) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T14 autorizadas; T15–T19 sin autorizar** |
| Spec | `spec.md` 1.0.0-u2 |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks totales | 19 |
| Tasks completadas | 2 |

## Reglas de esta lista

1. Escribir este documento no completa ninguna task; cada cierre se respalda en `evidence.md`.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. `terraform apply` solo con «OK PARA APPLY»; el seed en producción solo con su autorización propia.
4. Las acciones en la configuración de GitHub (Environment, revisores) son humanas; las tasks las documentan y verifican.
5. Un control no ejecutado no se reporta como PASS.
6. Ningún reporte, log o evidencia incluye tokens, cadenas de conexión, llaves ni datos personales.

## F1 — Identidad, Terraform y aprobación

- [x] **T01 · Identidad federada con rol mínimo** — Requisito: NV-09-R07 · Fase: F1 · Aceptación: `cd_identity.tf` con identidad administrada, credencial federada para `repo:thehouse-team/TheHouse-Backend:environment:production` y rol con alcance a `ca-thehouse-u2` (integrado si alcanza, personalizado si no); sin client secret; plan revisado y `apply` con «OK PARA APPLY». · Evidencia: plan, inventario y definición del rol. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Imagen fuera del control de Terraform** — Requisito: NV-09-R09 · Fase: F1 · Aceptación: `template[0].container[0].image` en `ignore_changes`; `var.image` conserva la validación de digest; `terraform plan` sin cambios después de un despliegue del CD. · Evidencia: diff y salida del plan. · Depende de: «OK PARA APPLY».
- [x] **T03 · Environment `production`** — Requisito: NV-09-R08 · Fase: F1 · Aceptación: guía paso a paso; Environment con revisores Victor e Isai, "Prevent self-review", solo rama `main` y variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`; verificado con la API de GitHub. · Evidencia: guía y respuesta de la API. · Depende de: T01 (para los valores de las variables).

## F2 — Publicación y cadena de suministro

- [ ] **T04 · Workflow `cd.yml` con disparadores y permisos** — Requisito: NV-09-R01 · Fase: F2 · Aceptación: `workflow_run` de `CI` en `main` (solo `push` y `success`) y `workflow_dispatch`; `permissions: {}` global y permisos por job; acciones fijadas por SHA; `concurrency: cd-production` sin cancelación; checkout del `head_sha`. · Evidencia: workflow revisado y run. · Depende de: autorización «EJECUTA».
- [ ] **T05 · Job `publish`** — Requisito: NV-09-R02 · Fase: F2 · Aceptación: imagen `sha-<commit>` con etiquetas OCI; digest `sha256:` validado y expuesto como output; sin `latest`. · Evidencia: run, digest e `imagetools inspect`. · Depende de: T04.
- [ ] **T06 · Re-escaneo Trivy del digest** — Requisito: NV-09-R03 · Fase: F2 · Aceptación: Trivy 0.76.2 sobre el digest publicado; Critical/High corregibles bloquean. · Evidencia: reporte. · Depende de: T05.
- [ ] **T07 · Firma Cosign y SBOM atestiguado** — Requisito: NV-09-R04 · Fase: F2 · Aceptación: `cosign sign` keyless del digest; `cosign attest --type cyclonedx`; `cosign verify` y `verify-attestation` con la identidad de `cd.yml` en `main` pasan fuera del workflow. · Evidencia: salida de Cosign y entrada en Rekor. · Depende de: T05.
- [ ] **T08 · Retiro de `u2-ghcr-publish.yml`** — Requisito: NV-09-R01 · Fase: F2 · Aceptación: workflow retirado; `VERSIONING.md` describe la publicación por `cd.yml`. · Evidencia: diff. · Depende de: T05, T07.

## F3 — Verificación efímera

- [ ] **T09 · Entorno efímero y smoke tests completos** — Requisito: NV-09-R05 · Fase: F3 · Aceptación: digest publicado en `Testing` con MongoDB y Redis como `services` fijados; llave JWT aleatoria por run; seed de catálogo; `smoke_test.py --mode full` cubre health, los 7 endpoints, registro → login → logout → reuso del token (401), 429 y ETag/304. · Evidencia: reporte del run. · Depende de: T05.
- [ ] **T10 · ZAP API scan efímero** — Requisito: NV-09-R06 · Fase: F3 · Aceptación: imagen `ghcr.io/zaproxy/zaproxy:2.17.0` contra `/openapi/v1.json`; `.zap/rules.tsv` con excepciones justificadas; High bloquea. · Evidencia: reporte HTML/JSON. · Depende de: T09.

## F4 — Despliegue y verificación en producción

- [ ] **T11 · Job `deploy`** — Requisito: NV-09-R09 · Fase: F4 · Aceptación: `environment: production`; `cosign verify` antes de desplegar; OIDC; imagen anterior registrada; `az containerapp update` por digest con sufijo de revisión; revisión `Running` y `Healthy` dentro del tiempo límite. · Evidencia: run con aprobación y revisión. · Depende de: T01, T02, T03, T07, T10.
- [ ] **T12 · Smoke tests en producción** — Requisito: NV-09-R10 · Fase: F4 · Aceptación: `smoke_test.py --mode readonly`: health, GET de universidades y propiedades (lista y detalle), registro inválido 400, login inexistente 401, logout sin token 401, HSTS, `nosniff` y ETag; reintentos acotados por arranque en frío; sin escrituras persistentes. · Evidencia: reporte. · Depende de: T11.
- [ ] **T13 · Rollback automático** — Requisito: NV-09-R11 · Fase: F4 · Aceptación: si `deploy` o `smoke-production` fallan se restaura la imagen anterior, se repite el smoke y el run falla; probado una vez con `simulate_smoke_failure`. · Evidencia: run de la prueba controlada. · Depende de: T11, T12.
- [ ] **T14 · ZAP baseline en producción** — Requisito: NV-09-R12 · Fase: F4 · Aceptación: escaneo pasivo contra la URL pública después del smoke; High bloquea salvo excepción justificada. · Evidencia: reporte. · Depende de: T12.

## F5 — Pruebas extendidas

- [ ] **T15 · Fuzzing con RESTler** — Requisito: NV-09-R13 · Fase: F5 · Aceptación: `cd-extended.yml` (manual y semanal) compila RESTler `v9.3.1` y lo ejecuta en *test* y *fuzz-lean* contra el entorno efímero; bugs triageados. · Evidencia: reporte y triage. · Depende de: T09.
- [ ] **T16 · Performance** — Requisito: NV-09-R13 · Fase: F5 · Aceptación: licencia de NBomber verificada para uso académico (si no se confirma: BLOCKED y propuesta de k6 con decisión formal); con NBomber, proyecto `TheHouse.PerformanceTests/` solo con levantamiento explícito de las acciones protegidas; escenarios de listados, detalle y login contra el entorno efímero; p95 y errores registrados; umbral propuesto con evidencia. · Evidencia: licencia o decisión, y reporte. · Depende de: T09.

## F6 — Primer despliegue, seed y evidencia

- [ ] **T17 · Primer despliegue real y registro** — Requisitos: NV-09-R09, NV-09-R15 · Fase: F6 · Aceptación: `main` con el PR #3 integrado desplegado por el pipeline con aprobación registrada; `deployment-record.json`; `VERSIONING.md` actualizado por pull request. · Evidencia: run, registro y pull request. · Depende de: T11, T12, T13, T14 e integración del PR #3.
- [ ] **T18 · Seed de catálogo en producción** — Requisito: NV-09-R14 · Fase: F6 · Aceptación: autorización explícita; ejecución única con `--seed-universities --seed-properties --allow-production-seed`; banderas de seed desactivadas al terminar; conteos verificados por la API pública. · Evidencia: log sanitizado y respuesta de la API. · Depende de: T17.
- [ ] **T19 · Evidencia, costo y revisión final** — Requisitos: NV-09-R15, NV-09-R16 · Fase: F6 · Aceptación: `evidence.md` con runs, hallazgos, excepciones, duración y costo; evidencia enlazada a NV-03 T14, T19, T22 y T25; revisión de seguridad y coherencia con resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md` y acta. · Depende de: T17, T18.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R07 | F1 | COMPLETADA |
| T02 | R09 | F1 | ABIERTA |
| T03 | R08 | F1 | COMPLETADA |
| T04 | R01 | F2 | ABIERTA |
| T05 | R02 | F2 | ABIERTA |
| T06 | R03 | F2 | ABIERTA |
| T07 | R04 | F2 | ABIERTA |
| T08 | R01 | F2 | ABIERTA |
| T09 | R05 | F3 | ABIERTA |
| T10 | R06 | F3 | ABIERTA |
| T11 | R09 | F4 | ABIERTA |
| T12 | R10 | F4 | ABIERTA |
| T13 | R11 | F4 | ABIERTA |
| T14 | R12 | F4 | ABIERTA |
| T15 | R13 | F5 | ABIERTA |
| T16 | R13 | F5 | ABIERTA |
| T17 | R09, R15 | F6 | ABIERTA |
| T18 | R14 | F6 | ABIERTA |
| T19 | R15, R16 | F6 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T04, T08 |
| R02 | T05 |
| R03 | T06 |
| R04 | T07 |
| R05 | T09 |
| R06 | T10 |
| R07 | T01 |
| R08 | T03 |
| R09 | T02, T11, T17 |
| R10 | T12 |
| R11 | T13 |
| R12 | T14 |
| R13 | T15, T16 |
| R14 | T18 |
| R15 | T17, T19 |
| R16 | T19 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 16 |
| Tasks | 19 |
| Tasks abiertas | 17 |
| Tasks completadas | 2 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN (T01–T14)** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
