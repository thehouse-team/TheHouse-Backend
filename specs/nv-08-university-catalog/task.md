# NV-08 — Task list University Catalog U2

| Campo | Valor |
| --- | --- |
| ID | NV-08 |
| Documento | `task.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Spec | `spec.md` 1.0.0-u2 |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks totales | 11 |
| Tasks U2 | 11 |
| Tasks completadas | 0 |

## Reglas

1. Todas las tasks están abiertas.
2. Solo se ejecutan con **Alcance: U2** y autorización de Fase B.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. El seed no se ejecuta en Fase A.

## F1 — Contrato y validación

- [ ] **T01 · Contrato y DTOs de universidades** — Requisitos: NV-08-R01, NV-08-R02 · Fase: F1 · **Alcance: U2** · Aceptación: DTOs de listado/detalle con `id`, `code`, `name`, `city`, `country`, `createdAt`; sin campos internos. · Evidencia: contrato y pruebas de serialización. · Depende de: autorización Fase B.
- [ ] **T02 · Validación de paginación e ID** — Requisitos: NV-08-R01, NV-08-R02, NV-08-R03 · Fase: F1 · **Alcance: U2** · Aceptación: `page` >= 1, `pageSize` 1–50, ObjectId de 24 hex, cálculo de `totalPages` y 400 para inválidos. · Evidencia: pruebas unitarias y casos negativos. · Depende de: T01.

## F2 — Seed y acceso a datos

- [ ] **T03 · Repositorio MongoDB e índices** — Requisito: NV-08-R05 · Fase: F2 · **Alcance: U2** · Aceptación: solo lectura, proyección explícita, índice único `{code:1}`, orden `{name:1,_id:1}` y sin N+1. · Evidencia: revisión de consultas e índices. · Depende de: T01.
- [ ] **T04 · Seed idempotente de universidades** — Requisito: NV-08-R04 · Fase: F2 · **Alcance: U2** · Aceptación: tres registros UTTT/UAEH/ITSOEH; upsert por `code`; dos ejecuciones no duplican; sin datos personales; origen y validación en `evidence.md` y conteos. · Evidencia: `evidence.md`, comando y revisión. · Depende de: T03.

## F3 — Endpoints y errores

- [ ] **T05 · Endpoint de listado de universidades** — Requisitos: NV-08-R01, NV-08-R05, NV-08-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/universities` con paginación, resultado vacío 200, 400/429/503 y Problem Details. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [ ] **T06 · Endpoint de detalle de universidad** — Requisitos: NV-08-R02, NV-08-R03, NV-08-R05, NV-08-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/universities/{id}` con 200/400/404/429/503 y solo campos públicos. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [ ] **T07 · Problem Details y respuestas seguras** — Requisito: NV-08-R06 · Fase: F3 · **Alcance: U2** · Aceptación: errores con `application/problem+json`, `traceId`, sin stack trace, detalles de Mongo ni campos privados. · Evidencia: pruebas de contrato. · Depende de: T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Pruebas unitarias de universidades** — Requisitos: NV-08-R03, NV-08-R04 · Fase: F4 · **Alcance: U2** · Aceptación: paginación, `totalPages`, ObjectId, DTO y lógica de seed cubiertos. · Evidencia: reporte de ejecución. · Depende de: T02, T04.
- [ ] **T09 · Pruebas de integración de universidades** — Requisitos: NV-08-R01, NV-08-R04, NV-08-R05, NV-08-R06 · Fase: F4 · **Alcance: U2** · Aceptación: listado con datos, vacío, detalle 200/404, 400/429/503 y seed dos veces sin duplicados. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T03, T04, T05, T06.
- [ ] **T10 · Pruebas de seguridad y seed** — Requisitos: NV-08-R02, NV-08-R04, NV-08-R07 · Fase: F4 · **Alcance: U2** · Aceptación: sin datos personales, errores sin stack trace, rate limit compartido y seed sin duplicados. · Evidencia: informe de seguridad. · Depende de: T04, T06, T07.
- [ ] **T11 · Evidencia y revisión de universidades** — Requisito: NV-08-R07 · Fase: F4 · **Alcance: U2** · Aceptación: cada requisito tiene evidencia o brecha declarada; revisión PASS/PASS WITH WARNINGS/BLOCKED. · Evidencia: matriz y acta. · Depende de: T08, T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01, R02 | F1 | U2 | ABIERTA |
| T02 | R01, R02, R03 | F1 | U2 | ABIERTA |
| T03 | R05 | F2 | U2 | ABIERTA |
| T04 | R04 | F2 | U2 | ABIERTA |
| T05 | R01, R05, R06 | F3 | U2 | ABIERTA |
| T06 | R02, R03, R05, R06 | F3 | U2 | ABIERTA |
| T07 | R06 | F3 | U2 | ABIERTA |
| T08 | R03, R04 | F4 | U2 | ABIERTA |
| T09 | R01, R04, R05, R06 | F4 | U2 | ABIERTA |
| T10 | R02, R04, R07 | F4 | U2 | ABIERTA |
| T11 | R07 | F4 | U2 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T02, T05, T09 |
| R02 | T01, T02, T06, T10 |
| R03 | T02, T06, T08 |
| R04 | T04, T08, T09, T10 |
| R05 | T03, T05, T06, T09 |
| R06 | T05, T06, T07, T09 |
| R07 | T10, T11 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 11 |
| Tasks U2 | 11 |
| Tasks abiertas | 11 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
