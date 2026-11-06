# NV-25 — Estadísticas del propietario y métricas de Smart Match — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 9 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Registro de eventos** — Requisito: NV-25-R01 · Fase: F1 · Aceptación: Vistas deduplicadas por día y eventos de favoritos, solicitudes y recomendaciones con `actorHash` no reversible. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Estadísticas del propietario** — Requisito: NV-25-R03 · Fase: F1 · Aceptación: Rango de hasta 90 días con vistas, favoritos, solicitudes y conversión; solo cuartos propios. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Recomendaciones por reglas** — Requisito: NV-25-R04 · Fase: F1 · Aceptación: Reglas documentadas y probadas con casos que disparan cada sugerencia. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T02 · Agregados diarios** — Requisito: NV-25-R02 · Fase: F2 · Aceptación: Tarea idempotente que produce contadores por cuarto y día; reejecución sin duplicar. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T03, T04.
- [ ] **T05 · Métricas de Smart Match** — Requisito: NV-25-R05 · Fase: F2 · Aceptación: Puntaje promedio, CTR, tasa de utilidad y descartes por versión de modelo; solo admin. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T03, T04.

## F3 — Seguridad, autorización y límites

- [ ] **T06 · Privacidad de la analítica** — Requisito: NV-25-R06 · Fase: F3 · Aceptación: Ninguna respuesta permite identificar usuarios; prueba con eventos de un mismo usuario en dos días. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T03, T04, T02, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Pruebas unitarias y de integración** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas de seguridad** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T09 · Evidencia y revisión final** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T07, T08.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F2 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F1 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F3 | ABIERTA |
| T07 | R07 | F4 | ABIERTA |
| T08 | R07 | F4 | ABIERTA |
| T09 | R07 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06 |
| R07 | T07, T08, T09 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 9 |
| Tasks abiertas | 9 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
