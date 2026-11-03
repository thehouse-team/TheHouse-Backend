# NV-07 — Task list Property Core U2

| Campo | Valor |
| --- | --- |
| ID | NV-07 |
| Documento | `task.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Spec | `spec.md` 1.0.0-u2 |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks totales | 10 |
| Tasks U2 | 10 |
| Tasks completadas | 0 |

## Reglas

1. Todas las tasks están abiertas.
2. Solo se ejecutan con **Alcance: U2** y autorización de Fase B.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. Solo se implementan las dos lecturas aprobadas.

## F1 — Contrato y paginación

- [ ] **T01 · Contrato y DTOs de propiedades** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R05 · Fase: F1 · **Alcance: U2** · Aceptación: DTOs de listado/detalle con campos públicos; sin `ownerId`, dirección exacta, coordenadas ni contacto. · Evidencia: contrato y pruebas de serialización. · Depende de: autorización Fase B.
- [ ] **T02 · Validación de paginación e ID** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R03 · Fase: F1 · **Alcance: U2** · Aceptación: `page` >= 1, `pageSize` 1–50, ObjectId de 24 hex, cálculo de `totalPages` y 400 para entradas inválidas. · Evidencia: pruebas unitarias y casos negativos. · Depende de: T01.

## F2 — Acceso a datos y proyección

- [ ] **T03 · Repositorio MongoDB, proyección e índice** — Requisitos: NV-07-R04, NV-07-R05 · Fase: F2 · **Alcance: U2** · Aceptación: solo lectura, proyección explícita, orden `createdAt`/`_id` descendente, índice `{createdAt:-1,_id:-1}` y sin N+1. · Evidencia: revisión de consultas, índice y pruebas. · Depende de: T01.

## F3 — Endpoints y errores

- [ ] **T04 · Endpoint de listado de propiedades** — Requisitos: NV-07-R01, NV-07-R04, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/properties` con paginación, resultado vacío 200, 400/429/503 y Problem Details. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [ ] **T05 · Endpoint de detalle de propiedad** — Requisitos: NV-07-R02, NV-07-R03, NV-07-R04, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/properties/{id}` con 200/400/404/429/503 y solo campos públicos. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [ ] **T06 · Problem Details y respuestas seguras** — Requisitos: NV-07-R05, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: errores con `application/problem+json`, `traceId`, sin stack trace, detalles de Mongo ni campos privados. · Evidencia: pruebas de contrato y revisión de respuestas. · Depende de: T04, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Pruebas unitarias de propiedades** — Requisitos: NV-07-R01, NV-07-R03, NV-07-R05 · Fase: F4 · **Alcance: U2** · Aceptación: paginación, `totalPages`, ObjectId, DTO y proyección cubiertos. · Evidencia: reporte de ejecución. · Depende de: T02.
- [ ] **T08 · Pruebas de integración de propiedades** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R04, NV-07-R06 · Fase: F4 · **Alcance: U2** · Aceptación: listado con datos, vacío, página fuera de rango, detalle 200/404/400/429/503. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T03, T04, T05, T06.
- [ ] **T09 · Pruebas de seguridad de propiedades** — Requisitos: NV-07-R05, NV-07-R06, NV-07-R07 · Fase: F4 · **Alcance: U2** · Aceptación: sin `ownerId`/dirección/coordenadas/contacto; errores sin stack trace; rate limit compartido. · Evidencia: informe de seguridad. · Depende de: T03, T05, T06.
- [ ] **T10 · Evidencia y revisión de propiedades** — Requisito: NV-07-R07 · Fase: F4 · **Alcance: U2** · Aceptación: cada requisito tiene evidencia o brecha declarada; revisión PASS/PASS WITH WARNINGS/BLOCKED. · Evidencia: matriz y acta. · Depende de: T07, T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01, R02, R05 | F1 | U2 | ABIERTA |
| T02 | R01, R02, R03 | F1 | U2 | ABIERTA |
| T03 | R04, R05 | F2 | U2 | ABIERTA |
| T04 | R01, R04, R06 | F3 | U2 | ABIERTA |
| T05 | R02, R03, R04, R06 | F3 | U2 | ABIERTA |
| T06 | R05, R06 | F3 | U2 | ABIERTA |
| T07 | R01, R03, R05 | F4 | U2 | ABIERTA |
| T08 | R01, R02, R04, R06 | F4 | U2 | ABIERTA |
| T09 | R05, R06, R07 | F4 | U2 | ABIERTA |
| T10 | R07 | F4 | U2 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T02, T04, T07, T08 |
| R02 | T01, T02, T05, T08 |
| R03 | T02, T05, T07 |
| R04 | T03, T04, T05, T08 |
| R05 | T01, T03, T06, T07, T09 |
| R06 | T04, T05, T06, T08, T09 |
| R07 | T09, T10 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 10 |
| Tasks U2 | 10 |
| Tasks abiertas | 10 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
