# NV-19 — Reseñas y calificaciones — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 7 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Reseña verificada** — Requisito: NV-19-R01 · Fase: F1 · Aceptación: Solo autores elegibles crean reseña; duplicado 409; propietario 403. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Edición acotada** — Requisito: NV-19-R02 · Fase: F1 · Aceptación: El autor edita dentro de 30 días; después 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Consulta y promedios** — Requisito: NV-19-R03 · Fase: F2 · Aceptación: Lista paginada por fecha y promedios recalculados al crear, editar u ocultar. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Integridad de agregados** — Requisito: NV-19-R04 · Fase: F2 · Aceptación: Promedios consistentes con las reseñas publicadas tras operaciones concurrentes. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F4 — Pruebas y evidencia

- [ ] **T05 · Pruebas unitarias y de integración** — Requisito: NV-19-R05 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Pruebas de seguridad** — Requisito: NV-19-R05 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04.
- [ ] **T07 · Evidencia y revisión final** — Requisito: NV-19-R05 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T05, T06.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F4 | ABIERTA |
| T06 | R05 | F4 | ABIERTA |
| T07 | R05 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05, T06, T07 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 5 |
| Tasks | 7 |
| Tasks abiertas | 7 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
