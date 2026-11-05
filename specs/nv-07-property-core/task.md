# NV-07 — Task list Property Core U2

| Campo | Valor |
| --- | --- |
| ID | NV-07 |
| Documento | `task.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **🟡 1.0.0-u2 CERRADA; T11 y T12 (1.1.0-u2) ⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Spec | `spec.md` 1.1.0-u2 |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks totales | 12 |
| Tasks U2 | 12 |
| Tasks completadas | 10 |

## Reglas

1. Todas las tasks están abiertas.
2. Solo se ejecutan con **Alcance: U2** y autorización de Fase B.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. Solo se implementan las dos lecturas aprobadas.

## F1 — Contrato y paginación

- [x] **T01 · Contrato y DTOs de propiedades** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R05 · Fase: F1 · **Alcance: U2** · Aceptación: DTOs de listado/detalle con campos públicos; sin `ownerId`, dirección exacta, coordenadas ni contacto. · Evidencia: contrato y pruebas de serialización. · Depende de: autorización Fase B.
- [x] **T02 · Validación de paginación e ID** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R03 · Fase: F1 · **Alcance: U2** · Aceptación: `page` >= 1, `pageSize` 1–50, ObjectId de 24 hex, cálculo de `totalPages` y 400 para entradas inválidas. · Evidencia: pruebas unitarias y casos negativos. · Depende de: T01.

## F2 — Acceso a datos y proyección

- [x] **T03 · Repositorio MongoDB, proyección e índice** — Requisitos: NV-07-R04, NV-07-R05 · Fase: F2 · **Alcance: U2** · Aceptación: solo lectura, proyección explícita, orden `createdAt`/`_id` descendente, índice `{createdAt:-1,_id:-1}` y sin N+1. · Evidencia: revisión de consultas, índice y pruebas. · Depende de: T01.

## F3 — Endpoints y errores

- [x] **T04 · Endpoint de listado de propiedades** — Requisitos: NV-07-R01, NV-07-R04, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/properties` con paginación, resultado vacío 200, 400/429/503 y Problem Details. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [x] **T05 · Endpoint de detalle de propiedad** — Requisitos: NV-07-R02, NV-07-R03, NV-07-R04, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: `GET /api/v1/properties/{id}` con 200/400/404/429/503 y solo campos públicos. · Evidencia: pruebas de integración. · Depende de: T02, T03.
- [x] **T06 · Problem Details y respuestas seguras** — Requisitos: NV-07-R05, NV-07-R06 · Fase: F3 · **Alcance: U2** · Aceptación: errores con `application/problem+json`, `traceId`, sin stack trace, detalles de Mongo ni campos privados. · Evidencia: pruebas de contrato y revisión de respuestas. · Depende de: T04, T05.
- [ ] **T11 · Caché HTTP condicional de propiedades** — Requisito: NV-07-R08 · Fase: F3 · **Alcance: U2 (1.1.0-u2)** · Aceptación: GET 200 de listado y detalle con `ETag` débil por contenido y `Cache-Control: public, max-age=60`; `If-None-Match` coincidente → 304 sin cuerpo; errores con `no-store` y sin `ETag`; sin cambios al contrato JSON. · Evidencia: diff autorizado y pruebas. · Depende de: T04 y T05 y autorización «EJECUTA».

## F4 — Pruebas y evidencia

- [x] **T07 · Pruebas unitarias de propiedades** — Requisitos: NV-07-R01, NV-07-R03, NV-07-R05 · Fase: F4 · **Alcance: U2** · Aceptación: paginación, `totalPages`, ObjectId, DTO y proyección cubiertos. · Evidencia: reporte de ejecución. · Depende de: T02.
- [x] **T08 · Pruebas de integración de propiedades** — Requisitos: NV-07-R01, NV-07-R02, NV-07-R04, NV-07-R06 · Fase: F4 · **Alcance: U2** · Aceptación: listado con datos, vacío, página fuera de rango, detalle 200/404/400/429/503. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T03, T04, T05, T06.
- [x] **T09 · Pruebas de seguridad de propiedades** — Requisitos: NV-07-R05, NV-07-R06, NV-07-R07 · Fase: F4 · **Alcance: U2** · Aceptación: sin `ownerId`/dirección/coordenadas/contacto; errores sin stack trace; rate limit compartido. · Evidencia: informe de seguridad. · Depende de: T03, T05, T06.
- [x] **T10 · Evidencia y revisión de propiedades** — Requisito: NV-07-R07 · Fase: F4 · **Alcance: U2** · Aceptación: cada requisito tiene evidencia o brecha declarada; revisión PASS/PASS WITH WARNINGS/BLOCKED. · Evidencia: matriz y acta. · Depende de: T07, T08, T09.
- [ ] **T12 · Pruebas y evidencia de caché HTTP de propiedades** — Requisitos: NV-07-R07, NV-07-R08 · Fase: F4 · **Alcance: U2 (1.1.0-u2)** · Aceptación: integración y seguridad cubren `ETag` estable, 304 (incluidos lista y `*`), 200 sin coincidencia, errores sin `ETag`, compresión y rate limit; `evidence.md` actualizado. · Evidencia: reporte de pruebas y acta. · Depende de: T09 y T11.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01, R02, R05 | F1 | U2 | COMPLETADA |
| T02 | R01, R02, R03 | F1 | U2 | COMPLETADA |
| T03 | R04, R05 | F2 | U2 | COMPLETADA |
| T04 | R01, R04, R06 | F3 | U2 | COMPLETADA |
| T05 | R02, R03, R04, R06 | F3 | U2 | COMPLETADA |
| T06 | R05, R06 | F3 | U2 | COMPLETADA |
| T07 | R01, R03, R05 | F4 | U2 | COMPLETADA |
| T08 | R01, R02, R04, R06 | F4 | U2 | COMPLETADA |
| T09 | R05, R06, R07 | F4 | U2 | COMPLETADA |
| T10 | R07 | F4 | U2 | COMPLETADA |
| T11 | R08 | F3 | U2 | ABIERTA |
| T12 | R07, R08 | F4 | U2 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T02, T04, T07, T08 |
| R02 | T01, T02, T05, T08 |
| R03 | T02, T05, T07 |
| R04 | T03, T04, T05, T08 |
| R05 | T01, T03, T06, T07, T09 |
| R06 | T04, T05, T06, T08, T09 |
| R07 | T09, T10, T12 |
| R08 | T11, T12 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Tasks | 12 |
| Tasks U2 | 12 |
| Tasks abiertas | 2 |
| Tasks completadas | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **🟡 1.0.0-u2 CERRADA; T11 y T12 (1.1.0-u2) ⛔ NO EJECUTAR HASTA «EJECUTA»** |
## Cierre U2 NV-07

| Campo | Resultado |
| --- | --- |
| Resultado de revisión | **PASS WITH WARNINGS** |
| Estado | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Workflow | `U2 Linux Validation` |
| Run | `41776038583` |
| Job | `126396500905` |
| Artifact | `u2-test-results` |
| Branch | `feature/u2-entrega` |
| Commit técnico | `faafa46` |
| Build Linux .NET 10 | PASS |
| Pruebas automatizadas | PASS |
| Seguridad | PASS |

### Advertencias declaradas

1. La implementación de `MongoPropertyRepository` está cubierta mediante revisión, pruebas unitarias e integración aislada, pero la conectividad contra MongoDB real/cloud se verificará durante NV-03.
2. El orden `createdAt DESC, _id DESC` y el índice correspondiente están implementados y reproducidos por los tests, pero su validación contra MongoDB real queda pendiente de NV-03.
3. No existen hallazgos Critical o High abiertos.
