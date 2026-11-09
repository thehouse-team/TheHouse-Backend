# NV-17 — Visitas: solicitudes (v1) — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `task.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-17-visits/`; la migración `--migrate-to-rooms` en Production requiere autorización aparte) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas; T07–T10 esperan CI, migración y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 6 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Visitas a cuartos publicados** — Requisito: NV-17-R01 · Fase: F1 · Aceptación: `POST /visits` con `roomId` de un cuarto publicado crea la visita (201); cuarto no publicado o inexistente 404; ambos ids o ninguno 400. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Respuesta con cuarto** — Requisito: NV-17-R02 · Fase: F1 · Aceptación: Las respuestas incluyen `room { id, buildingId, title, city }` y, en transición, `property` o `null`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Reglas por cuarto** — Requisito: NV-17-R03 · Fase: F1 · Aceptación: Horario, idempotencia y conflictos de 1.1.0 aplicados por cuarto, con sus pruebas. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T04 · Transición desde propiedades** — Requisito: NV-17-R04 · Fase: F2 · Aceptación: `propertyId` se traduce a su cuarto y produce la misma visita que con `roomId`; misma llave con el id de propiedad o de cuarto es el mismo cuerpo. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T05 · Migración de visitas** — Requisito: NV-17-R05 · Fase: F2 · Aceptación: `--migrate-to-rooms` convierte las visitas existentes, reporta las no mapeadas y es idempotente. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [x] **T06 · Autorización y límites** — Requisito: NV-17-R06 · Fase: F3 · Aceptación: Pruebas con dos usuarios; rate limits 429; Redis caído 503; `no-store`. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Contrato para Mobile y despliegue** — Requisito: NV-17-R07 · Fase: F4 · Aceptación: Contrato §6.1 entregado; migración autorizada en Production; prueba HTTP real en Azure (crear por cuarto y por propiedad, repetir, conflicto, listar, cancelar). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-17-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-17-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-17-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F2 | COMPLETA |
| T05 | R05 | F2 | COMPLETA |
| T06 | R06 | F3 | COMPLETA |
| T07 | R07 | F4 | ABIERTA |
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
| Tasks abiertas | 4 |
| Tasks completadas | 6 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas; T07–T10 esperan CI, migración y Azure** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
