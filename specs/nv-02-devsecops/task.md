# NV-02 — Task list DevSecOps

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 24 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas y deben permanecer así durante la planificación.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Escribir este documento no completa ninguna task.
4. Cada task declara al menos un requisito.
5. Las dependencias con NV-01 y NV-03 no conceden autorización.
6. Ninguna task autoriza escanear producción, terceros o datos reales.
7. Un control no ejecutado no se reporta como PASS.

## F1 — Línea base de pruebas

- [ ] **T01 · Inventario y plan de suites** — Requisito: NV-02-R01 · Fase: F1 · Aceptación: riesgos mapeados a suites, alcance y limitaciones declarados; los placeholders identificados. · Evidencia: matriz riesgo→suite y revisión. · Depende de: autorización NV-02.
- [ ] **T02 · Pruebas unitarias de Domain/Application** — Requisito: NV-02-R01 · Fase: F1 · Aceptación: casos de negocio y límites aislados, deterministas y sin infraestructura real. · Evidencia: reporte xUnit y comando reproducible. · Depende de: T01.
- [ ] **T03 · Harness de integración con WebApplicationFactory** — Requisito: NV-02-R02 · Fase: F1 · Aceptación: composición real, datos aislados, limpieza y ausencia de servicios productivos. · Evidencia: harness revisado y prueba de humo. · Depende de: T01 y NV-01.
- [ ] **T04 · Integración de fundamentos HTTP** — Requisito: NV-02-R02 · Fase: F1 · Aceptación: salud, errores, versión y correlación verificados contra NV-01. · Evidencia: reporte de integración. · Depende de: T03.
- [ ] **T05 · Suite de seguridad** — Requisito: NV-02-R03 · Fase: F1 · Aceptación: autorización, abuso, cabeceras, entrada y límites con casos negativos; sin datos productivos. · Evidencia: reporte y hallazgos. · Depende de: T03.

## F2 — Análisis estático y suministro

- [ ] **T06 · Semgrep con reglas y baseline** — Requisito: NV-02-R04 · Fase: F2 · Aceptación: configuración versionada, baseline medido y supresiones justificadas. · Evidencia: reporte SARIF/JSON y versión de reglas. · Depende de: T01.
- [ ] **T07 · SonarAnalyzer y SonarCloud condicionado a $0** — Requisito: NV-02-R04 · Fase: F2 · Aceptación: análisis local operativo; SonarCloud solo con elegibilidad gratuita verificada. · Evidencia: configuración, reporte y prueba de costo $0. · Depende de: T01.
- [ ] **T08 · Auditoría SCA de NuGet** — Requisito: NV-02-R05 · Fase: F2 · Aceptación: inventario de dependencias, vulnerabilidades y licencias; política de versiones fijadas. · Evidencia: reporte de auditoría y plan de tratamiento. · Depende de: T01.
- [ ] **T09 · SBOM CycloneDX** — Requisito: NV-02-R06 · Fase: F2 · Aceptación: SBOM generado, versionado y trazable a la entrega o imagen. · Evidencia: archivo SBOM y verificación de formato. · Depende de: T01.

## F3 — Pruebas avanzadas

- [ ] **T10 · Baseline de mutación con Stryker.NET** — Requisito: NV-02-R07 · Fase: F3 · Aceptación: alcance acotado, score medido, mutantes no cubiertos y runtime documentados. · Evidencia: reporte Stryker y configuración. · Depende de: T01, T02.
- [ ] **T11 · Contrato con PactNet o alcance equivalente** — Requisito: NV-02-R08 · Fase: F3 · Aceptación: consumidor/proveedor identificado y verificación reproducible; sin divergencia con OpenAPI. · Evidencia: pactos y reporte de verificación. · Depende de: T03, T04.
- [ ] **T12 · Performance con NBomber** — Requisito: NV-02-R09 · Fase: F3 · Aceptación: escenario, datos, concurrencia, duración, entorno y SLO declarados; resultados no extrapolados. · Evidencia: reporte NBomber y configuración. · Depende de: T04.

## F4 — Seguridad dinámica

- [ ] **T13 · DAST con OWASP ZAP** — Requisito: NV-02-R10 · Fase: F4 · Aceptación: entorno aislado, alcance y credenciales de prueba aprobados; sin atacar terceros. · Evidencia: reporte ZAP y triage. · Depende de: T04.
- [ ] **T14 · Fuzzing REST con RESTler** — Requisito: NV-02-R11 · Fase: F4 · Aceptación: OpenAPI aprobado, límites de tasa, datos de prueba y triage de hallazgos. · Evidencia: reporte RESTler y configuración. · Depende de: T04.

## F5 — Contenedor y firma

- [ ] **T15 · Escaneo de imagen con Trivy** — Requisito: NV-02-R12 · Fase: F5 · Aceptación: imagen autorizada por NV-03, reporte de capas y bloqueo de críticos/altos corregibles. · Evidencia: reporte Trivy y digest. · Depende de: imagen de NV-03.
- [ ] **T16 · Lint de contenedor con Dockle** — Requisito: NV-02-R12 · Fase: F5 · Aceptación: usuario no root, sin secretos, configuración revisada y hallazgos fatales tratados. · Evidencia: reporte Dockle. · Depende de: imagen de NV-03.
- [ ] **T17 · Firma y verificación con Cosign** — Requisito: NV-02-R13 · Fase: F5 · Aceptación: imagen firmada, identidad/clave protegida y verificación previa al despliegue. · Evidencia: firma, digest y verificación. · Depende de: T15.

## F6 — Gates, CI y evidencia

- [ ] **T18 · Catálogo de quality gates** — Requisito: NV-02-R14 · Fase: F6 · Aceptación: cada gate con alcance, criterio, evidencia, bloqueo y excepción. · Evidencia: catálogo revisado. · Depende de: T06 a T14.
- [ ] **T19 · Registro de hallazgos y excepciones** — Requisito: NV-02-R16 · Fase: F6 · Aceptación: severidad, origen, responsable, vencimiento y estado; excepciones aprobadas. · Evidencia: registro y revisión. · Depende de: T18.
- [ ] **T20 · Workflow CI de build y pruebas** — Requisito: NV-02-R15 · Fase: F6 · Aceptación: permisos mínimos, acciones fijadas, secretos protegidos y artefactos retenidos; no se crea en esta entrega. · Evidencia: diseño y revisión del workflow futuro. · Depende de: T01 a T12.
- [ ] **T21 · Pipeline de seguridad** — Requisito: NV-02-R15 · Fase: F6 · Aceptación: SAST/SCA/SBOM/DAST/fuzzing/container/firma integrados con reportes y gates. · Evidencia: diseño y reportes de prueba. · Depende de: T06 a T17.
- [ ] **T22 · Protección de rama y gates de PR** — Requisito: NV-02-R14, NV-02-R15 · Fase: F6 · Aceptación: revisión obligatoria, checks requeridos y ausencia de bypass silencioso. · Evidencia: configuración revisada. · Depende de: T20, T21.
- [ ] **T23 · Retención y reproducción de evidencia** — Requisito: NV-02-R16 · Fase: F6 · Aceptación: reportes, comandos, versiones y entorno conservados sin secretos y con costo $0. · Evidencia: inventario de artefactos. · Depende de: T20 a T22.
- [ ] **T24 · Revisión final de seguridad y cierre** — Requisitos: NV-02-R14, NV-02-R16 · Fase: F6 · Aceptación: hallazgos y excepciones revisados; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: acta y matriz de evidencia. · Depende de: T18 a T23.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R01 | F1 | ABIERTA |
| T03 | R02 | F1 | ABIERTA |
| T04 | R02 | F1 | ABIERTA |
| T05 | R03 | F1 | ABIERTA |
| T06 | R04 | F2 | ABIERTA |
| T07 | R04 | F2 | ABIERTA |
| T08 | R05 | F2 | ABIERTA |
| T09 | R06 | F2 | ABIERTA |
| T10 | R07 | F3 | ABIERTA |
| T11 | R08 | F3 | ABIERTA |
| T12 | R09 | F3 | ABIERTA |
| T13 | R10 | F4 | ABIERTA |
| T14 | R11 | F4 | ABIERTA |
| T15 | R12 | F5 | ABIERTA |
| T16 | R12 | F5 | ABIERTA |
| T17 | R13 | F5 | ABIERTA |
| T18 | R14 | F6 | ABIERTA |
| T19 | R16 | F6 | ABIERTA |
| T20 | R15 | F6 | ABIERTA |
| T21 | R15 | F6 | ABIERTA |
| T22 | R14, R15 | F6 | ABIERTA |
| T23 | R16 | F6 | ABIERTA |
| T24 | R14, R16 | F6 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T02 |
| R02 | T03, T04 |
| R03 | T05 |
| R04 | T06, T07 |
| R05 | T08 |
| R06 | T09 |
| R07 | T10 |
| R08 | T11 |
| R09 | T12 |
| R10 | T13 |
| R11 | T14 |
| R12 | T15, T16 |
| R13 | T17 |
| R14 | T18, T22, T24 |
| R15 | T20, T21, T22 |
| R16 | T19, T23, T24 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 16 |
| Tasks | 24 |
| Tasks abiertas | 24 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado | **⛔ NO EJECUTAR** |

Ninguna task puede ejecutarse hasta la aprobación humana de `spec.md`, `plan.md` y `task.md`, con versiones, IDs, rutas, entornos y restricciones explícitas.
