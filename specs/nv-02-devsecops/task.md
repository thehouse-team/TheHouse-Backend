# NV-02 — Task list CI y DevSecOps U2

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `task.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA») |
| Estado de ejecución | **🟡 CI EN VERDE EN EL PR #3 (run `42122674335`); ABIERTAS T14 Y T15** |
| Spec | `spec.md` 1.1.0-u2 |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks totales | 15 |
| Tasks completadas | 13 |

## Reglas de esta lista

1. Escribir este documento no completa ninguna task; cada cierre se respalda en `evidence.md`.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task usa secretos, publica imágenes ni despliega (eso es NV-09).
4. Un control no ejecutado no se reporta como PASS.
5. Las acciones en la configuración de GitHub (ruleset, visibilidad) son humanas; las tasks solo las documentan y verifican.

## F1 — Workflow base, build y pruebas

- [x] **T01 · Workflow `ci.yml` con disparadores y permisos** — Requisito: NV-02-R01 · Fase: F1 · Aceptación: `pull_request` a `main`, `push` a `main` y `workflow_dispatch`; `permissions: contents: read`; acciones fijadas; `concurrency` y `timeout-minutes`; sin secretos. · Evidencia: workflow revisado y run en un pull request. · Depende de: autorización «EJECUTA».
- [x] **T02 · Job `build-test`** — Requisito: NV-02-R02 · Fase: F1 · Aceptación: SDK desde `global.json`; build Release con advertencias como error; suites unitaria, de seguridad e integración con TRX; una prueba fallida rompe el job. · Evidencia: run y TRX. · Depende de: T01.
- [x] **T03 · Cobertura informativa** — Requisito: NV-02-R03 · Fase: F1 · Aceptación: cobertura de líneas y ramas por proyecto en el resumen del run y como artefacto; no bloquea. · Evidencia: resumen y archivo Cobertura. · Depende de: T02.
- [x] **T13 · Retiro de workflows de validación duplicados** — Requisito: NV-02-R01 · Fase: F1 · Aceptación: `u2-linux-validation.yml` y `u2-container-validation.yml` retirados una vez que `build-test` y `container-security` los cubren; `u2-ghcr-publish.yml` se conserva para NV-09. · Evidencia: diff y run de `ci.yml`. · Depende de: T02, T09.

## F2 — Código, dependencias y secretos

- [x] **T04 · Job `sast-semgrep`** — Requisito: NV-02-R04 · Fase: F2 · Aceptación: imagen de Semgrep CE fijada; rulesets `p/csharp`, `p/secrets`, `p/dockerfile` y `p/terraform`; ERROR bloquea; SARIF como artefacto. · Evidencia: run y SARIF. · Depende de: T01.
- [x] **T05 · Reglas Semgrep propias** — Requisito: NV-02-R04 · Fase: F2 · Aceptación: al menos 8 reglas del proyecto en `.semgrep/` con pruebas de regla (casos que deben y no deben coincidir); supresiones justificadas. · Evidencia: reglas, pruebas y run. · Depende de: T04.
- [x] **T06 · Job `sca-sbom`: dependencias vulnerables** — Requisito: NV-02-R05 · Fase: F2 · Aceptación: `dotnet list package --vulnerable --include-transitive` sobre la solución; High o Critical bloquea salvo excepción registrada. · Evidencia: reporte del run. · Depende de: T01.
- [x] **T07 · SBOM CycloneDX** — Requisito: NV-02-R06 · Fase: F2 · Aceptación: SBOM de la solución generado en cada run, con el commit en sus metadatos y retenido 14 días. · Evidencia: artefacto `sbom.json`. · Depende de: T06.
- [x] **T08 · Job `secrets-gitleaks`** — Requisito: NV-02-R07 · Fase: F2 · Aceptación: imagen de Gitleaks fijada (sin la action que exige licencia), escaneo de todo el historial con `fetch-depth: 0`, valores redactados; un hallazgo bloquea. · Evidencia: run y reporte redactado. · Depende de: T01.

## F3 — Contenedor e infraestructura

- [x] **T09 · Job `container-security`** — Requisitos: NV-02-R08, NV-02-R09 · Fase: F3 · Aceptación: build de la imagen desde `Dockerfile`; Trivy de imagen (Critical/High corregibles bloquean) y de configuración del Dockerfile y Terraform (High/Critical bloquean); Dockle (FATAL bloquea); usuario no root verificado. · Evidencia: reportes Trivy y Dockle. · Depende de: T01.
- [x] **T10 · Job `iac-terraform`** — Requisito: NV-02-R09 · Fase: F3 · Aceptación: Terraform 1.17.x; `fmt -check -recursive`, `init -backend=false` y `validate` sin credenciales. · Evidencia: salida del job. · Depende de: T01.

## F4 — Mutación

- [x] **T11 · Job `mutation-stryker` y línea base** — Requisitos: NV-02-R10, NV-02-R13 · Fase: F4 · Aceptación: Stryker.NET sobre Domain y Application con `TheHouse.UnitTests`; programado semanal y manual, no en cada pull request; score y mutantes sobrevivientes registrados; umbral propuesto con evidencia. · Evidencia: reporte Stryker y minutos consumidos. · Depende de: T02.

## F5 — Gate, protección y evidencia

- [x] **T12 · Job `quality-gate` y resumen de hardening** — Requisito: NV-02-R11 · Fase: F5 · Aceptación: depende de los jobs obligatorios, falla si alguno no terminó en éxito y publica una tabla de resultados en el resumen del run. · Evidencia: run completo. · Depende de: T02, T04, T06, T07, T08, T09, T10.
- [ ] **T14 · Protección de rama con checks requeridos** — Requisito: NV-02-R11 · Fase: F5 · Aceptación: guía paso a paso para agregar `build-test`, `sast-semgrep`, `sca-sbom`, `secrets-gitleaks`, `container-security`, `iac-terraform` y `quality-gate` al ruleset de `main`; verificación con la API de GitHub de que el ruleset se aplica, o registro de que sigue sin aplicarse por M-BE-050. · Evidencia: guía y respuesta de la API. · Depende de: T12 y decisión M-BE-050.
- [ ] **T15 · Evidencia, costo y revisión final** — Requisitos: NV-02-R12, NV-02-R13 · Fase: F5 · Aceptación: `evidence.md` con run, commit, resultados por job, hallazgos, excepciones y minutos consumidos; revisión de seguridad y coherencia con resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md` y acta. · Depende de: T12, T14.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETADA |
| T02 | R02 | F1 | COMPLETADA |
| T03 | R03 | F1 | COMPLETADA |
| T04 | R04 | F2 | COMPLETADA |
| T05 | R04 | F2 | COMPLETADA |
| T06 | R05 | F2 | COMPLETADA |
| T07 | R06 | F2 | COMPLETADA |
| T08 | R07 | F2 | COMPLETADA |
| T09 | R08, R09 | F3 | COMPLETADA |
| T10 | R09 | F3 | COMPLETADA |
| T11 | R10, R13 | F4 | COMPLETADA |
| T12 | R11 | F5 | COMPLETADA |
| T13 | R01 | F1 | COMPLETADA |
| T14 | R11 | F5 | ABIERTA |
| T15 | R12, R13 | F5 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T13 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04, T05 |
| R05 | T06 |
| R06 | T07 |
| R07 | T08 |
| R08 | T09 |
| R09 | T09, T10 |
| R10 | T11 |
| R11 | T12, T14 |
| R12 | T15 |
| R13 | T11, T15 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Tasks | 15 |
| Tasks abiertas | 2 |
| Tasks completadas | 13 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 CI EN VERDE; ABIERTAS T14 Y T15** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
