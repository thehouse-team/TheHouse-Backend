# NV-17 — Visitas: solicitud, confirmación y calendario — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `task.md` |
| Versión | 1.3.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-11 («apruebo y ejecuta»: aprobación de 1.3.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-17-visits/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.3.0, T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.3.0-borrador |
| Plan | `plan.md` 1.3.0-borrador |
| Tasks totales | 11 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Calendario del arrendador** — Requisito: NV-17-R01 · Fase: F1 · Aceptación: Lista las visitas de sus cuartos por rango (≤ 62 días), estado y cuarto, ordenadas por horario; detalle con nombre y universidad del estudiante; lo ajeno 404; sin modo arrendador 403. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Confirmar** — Requisito: NV-17-R02 · Fase: F1 · Aceptación: `requested` futura → `confirmed` con `confirmedAt` e historial; cualquier otro estado 409 `visit_state`; visita pasada 409 `visit_in_past`; cuarto no publicado 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Reprogramar** — Requisito: NV-17-R03 · Fase: F1 · Aceptación: Nuevo horario válido con las reglas de 1.1.0, `previousScheduledAt` guardado, estado `confirmed`; 422 horario inválido; 409 `student_schedule_overlap`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Cancelar por cualquiera de las partes** — Requisito: NV-17-R04 · Fase: F1 · Aceptación: Arrendador con motivo del catálogo (nota obligatoria con `other`); estudiante también sobre `confirmed`; `cancellation.by` correcto; visita pasada 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T05 · Dirección exacta al confirmar** — Requisito: NV-17-R05 · Fase: F2 · Aceptación: La visita del estudiante incluye `address` y `location` solo si está `confirmed`; en `requested` y `cancelled` son `null`; nunca en vistas del arrendador ni públicas. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Compatibilidad y NV-16** — Requisito: NV-17-R06 · Fase: F2 · Aceptación: Las respuestas de 1.2.0 conservan todos sus campos; el filtro `status` acepta `confirmed`; la solicitud de NV-16 muestra la visita activa `requested` o `confirmed`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Privacidad, autorización y límites** — Requisito: NV-17-R07 · Fase: F3 · Aceptación: Pruebas con estudiante, arrendador y un tercero; sin datos de contacto; rate limits 429; Redis caído 503. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-17-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitar, calendario, confirmar con dirección, reprogramar, cancelar por ambas partes). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-17-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-17-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-17-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F1 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R09 | F4 | ABIERTA |
| T10 | R09 | F4 | ABIERTA |
| T11 | R09 | F4 | ABIERTA |

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
| R08 | T08 |
| R09 | T09, T10, T11 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Tasks | 11 |
| Tasks abiertas | 11 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.3.0, T01–T11 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
