# NV-23 — Smart Match — compatibilidad con Machine Learning — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Perfil Smart Match** — Requisito: NV-23-R01 · Fase: F1 · Aceptación: Se arma desde las preferencias de NV-11 o del cuerpo para invitados, con las mismas validaciones. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Cálculo de compatibilidad** — Requisito: NV-23-R02 · Fase: F1 · Aceptación: Puntaje 0–100 con factores de presupuesto, distancia, servicios, modalidad, tipo y extras; determinista por versión; casos de prueba con resultados esperados. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Recomendaciones ordenadas** — Requisito: NV-23-R03 · Fase: F1 · Aceptación: Lista paginada ordenada por puntaje descendente con desempate estable; descartes al final. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Retroalimentación y descartes** — Requisito: NV-23-R04 · Fase: F2 · Aceptación: Se registran con `modelVersion`; idempotentes; descartes por 30 días. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · Rendimiento** — Requisito: NV-23-R05 · Fase: F2 · Aceptación: p95 < 300 ms para 50 cuartos en el runner (RNF-14). · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Modelo entrenado y evaluación** — Requisito: NV-23-R06 · Fase: F2 · Aceptación: Pipeline fuera de línea con ML.NET; métrica comparada contra el modelo base; promoción solo con evidencia y nueva `modelVersion`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Minimización y límites** — Requisito: NV-23-R07 · Fase: F3 · Aceptación: Prueba que el cálculo no lee campos personales; límites de tamaño y rate limit verificados. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-23-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-23-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-23-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R08 | F4 | ABIERTA |
| T10 | R08 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06 |
| R07 | T07 |
| R08 | T08, T09, T10 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Tasks | 10 |
| Tasks abiertas | 10 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
