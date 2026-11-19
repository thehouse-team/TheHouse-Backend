# NV-20 — Notificaciones — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `task.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-18 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T08; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-20-notifications/`; sin comandos en Production) |
| Estado de ejecución | **🟡 DESPLEGADA — 1.2.0: 7 de 8 tasks (2026-11-19); T05 espera la prueba del push en un teléfono real con Mobile** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks totales | 8 |
| Tasks completadas | 7 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Registro de dispositivos** — Requisito: NV-20-R01 · Fase: F1 · Aceptación: PUT idempotente por instalación, máximo 5, un token por cuenta, DELETE y lista sin tokens; limpieza al logout, suspensión y eliminación. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T02 · Envío push de la bandeja** — Requisito: NV-20-R02 · Fase: F2 · Aceptación: Los 16 tipos se envían a los dispositivos del destinatario con título, cuerpo y datos de destino; nunca al actor; agrupados por conversación. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01.
- [x] **T03 · Cliente de FCM** — Requisito: NV-20-R03 · Fase: F2 · Aceptación: OAuth 2.0 con la cuenta de servicio, renovación del token de acceso, API HTTP v1, borrado de tokens inválidos y un reintento; un fallo nunca rompe la acción principal. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01.

## F3 — Seguridad, autorización y límites

- [x] **T04 · Privacidad, seguridad y límites** — Requisito: NV-20-R04 · Fase: F3 · Aceptación: Sin contenido privado en push, sin tokens en logs ni respuestas, BOLA, rate limit, secreto solo en la Container App. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03.

## F4 — Pruebas y evidencia

- [ ] **T05 · Contrato para Mobile y despliegue** — Requisito: NV-20-R05 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; secreto cargado por Victor; desplegado por el CD; prueba real en Azure con un teléfono: solicitud, mensaje y visita llegan como push. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04.
- [x] **T06 · Pruebas unitarias y de integración** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05.
- [x] **T07 · Pruebas de seguridad** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05.
- [x] **T08 · Evidencia y revisión final** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T06, T07.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F2 | COMPLETA |
| T03 | R03 | F2 | COMPLETA |
| T04 | R04 | F3 | COMPLETA |
| T05 | R05 | F4 | ABIERTA |
| T06 | R06 | F4 | COMPLETA |
| T07 | R06 | F4 | COMPLETA |
| T08 | R06 | F4 | COMPLETA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06, T07, T08 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 6 |
| Tasks | 8 |
| Tasks abiertas | 1 |
| Tasks completadas | 7 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 DESPLEGADA — 1.2.0: 7 de 8 tasks (2026-11-19); T05 espera la prueba del push en un teléfono real con Mobile** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
