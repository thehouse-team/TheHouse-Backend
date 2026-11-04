# NV-06 — Task list Logout U2

| Campo | Valor |
| --- | --- |
| ID | NV-06 |
| Documento | `task.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Spec | `spec.md` 1.0.0-u2 |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks totales | 10 |
| Tasks U2 | 10 |
| Tasks completadas | 10 |

## Reglas

1. Todas las tasks U2 fueron ejecutadas y cerradas con evidencia.
2. Solo se ejecutan con **Alcance: U2** y autorización de Fase B.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. No se implementa refresh ni endpoints fuera de `POST /api/v1/auth/logout`.

## F1 — Contrato y revocación

- [x] **T01 · Contrato y middleware de autenticación** — Requisitos: NV-06-R01, NV-06-R02 · Fase: F1 · **Alcance: U2** · Aceptación: Bearer obligatorio, sin cuerpo, validación de JWT y `jti`, y respuesta 204/401 según corresponda. · Evidencia: contrato y pruebas de middleware. · Depende de: autorización Fase B.
- [x] **T02 · Repositorio de revocación Redis** — Requisitos: NV-06-R02, NV-06-R03 · Fase: F1 · **Alcance: U2** · Aceptación: clave `auth:revoked:jti:{jti}`, TTL hasta `exp`, `SET` atómico, consulta de revocación y ausencia de caché local. · Evidencia: pruebas unitarias y de Redis. · Depende de: T01.
- [x] **T03 · Validación y consulta de revocación** — Requisitos: NV-06-R02, NV-06-R03 · Fase: F1 · **Alcance: U2** · Aceptación: valida firma/claims, consulta Redis, rechaza revocado con 401 y devuelve 503 si Redis falla. · Evidencia: pruebas de integración y fallo. · Depende de: T01, T02.

## F2 — Endpoint y semántica

- [x] **T04 · Caso de uso y endpoint de logout** — Requisitos: NV-06-R01, NV-06-R04 · Fase: F2 · **Alcance: U2** · Aceptación: primer logout 204, segundo 401, expirado 401, sin cerrar otras sesiones y sin cuerpo de respuesta. · Evidencia: pruebas de integración. · Depende de: T03.
- [x] **T06 · Logging seguro, cabeceras y rate limiting** — Requisito: NV-06-R06 · Fase: F2 · **Alcance: U2** · Aceptación: sin JWT/jti en logs o respuestas, `no-store`, rate limit 30/60 s por IP y token no aceptado por query. · Evidencia: pruebas de logs, cabeceras y límite. · Depende de: T04.

## F3 — Errores

- [x] **T05 · Problem Details de logout** — Requisito: NV-06-R05 · Fase: F3 · **Alcance: U2** · Aceptación: 400/401/429/503/500 con `application/problem+json`, `traceId` y sin detalles internos. · Evidencia: pruebas de contrato. · Depende de: T04.
- [x] **T08 · Pruebas de integración de logout** — Requisitos: NV-06-R01, NV-06-R03, NV-06-R05, NV-06-R07 · Fase: F3 · **Alcance: U2** · Aceptación: 204, reutilización 401, token ausente/inválido 401, Redis caído 503 y Problem Details. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T04, T05.

## F4 — Pruebas y evidencia

- [x] **T07 · Pruebas unitarias de logout** — Requisitos: NV-06-R02, NV-06-R04, NV-06-R07 · Fase: F4 · **Alcance: U2** · Aceptación: clave/TTL, token sin `jti`, expirado, semántica 204/401 y refresh futuro documentado. · Evidencia: reporte de ejecución. · Depende de: T02, T04.
- [x] **T09 · Pruebas multi-réplica y de seguridad** — Requisitos: NV-06-R02, NV-06-R03, NV-06-R04, NV-06-R06, NV-06-R07 · Fase: F4 · **Alcance: U2** · Aceptación: token emitido en A, logout en B y reutilización rechazada en A; logs sin secretos; rate limit compartido. · Evidencia: reporte multi-réplica e informe de seguridad. · Depende de: T03, T04, T06.
- [x] **T10 · Evidencia y revisión de logout** — Requisito: NV-06-R07 · Fase: F4 · **Alcance: U2** · Aceptación: cada requisito tiene evidencia o brecha declarada; revisión PASS/PASS WITH WARNINGS/BLOCKED. · Evidencia: matriz y acta. · Depende de: T07, T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01, R02 | F1 | U2 | COMPLETADA |
| T02 | R02, R03 | F1 | U2 | COMPLETADA |
| T03 | R02, R03 | F1 | U2 | COMPLETADA |
| T04 | R01, R04 | F2 | U2 | COMPLETADA |
| T05 | R05 | F3 | U2 | COMPLETADA |
| T06 | R06 | F2 | U2 | COMPLETADA |
| T07 | R02, R04, R07 | F4 | U2 | COMPLETADA |
| T08 | R01, R03, R05, R07 | F3 | U2 | COMPLETADA |
| T09 | R02, R03, R04, R06, R07 | F4 | U2 | COMPLETADA |
| T10 | R07 | F4 | U2 | COMPLETADA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T04, T08 |
| R02 | T01, T02, T03, T07, T09 |
| R03 | T02, T03, T08, T09 |
| R04 | T04, T07, T09 |
| R05 | T05, T08 |
| R06 | T06, T09 |
| R07 | T07, T08, T09, T10 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 10 |
| Tasks U2 | 10 |
| Tasks abiertas | 0 |
| Tasks completadas | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
## Cierre U2 NV-06

| Campo | Resultado |
| --- | --- |
| Resultado de revisión | **PASS WITH WARNINGS** |
| Estado | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Workflow | `U2 Linux Validation` |
| Run | `41771006554` |
| Job | `126380428996` |
| Artifact | `u2-test-results` |
| Branch | `feature/u2-entrega` |
| Commit técnico | `8cfad4a` |
| Build Linux .NET 10 | PASS |
| Pruebas automatizadas | PASS |
| Seguridad | PASS |

### Advertencias declaradas

1. La implementación de `RedisTokenRevocationStore` está cubierta mediante pruebas automatizadas y comportamiento fail-closed, pero la conectividad contra una instancia Redis real/cloud se comprobará en NV-03.
2. `TokenRevocationMiddleware` y `RequireTokenNotRevoked()` quedan preparados para los futuros endpoints autenticados. U2 no contiene todavía otro endpoint productivo protegido al cual adjuntar la convención.
3. Dos logout exactamente simultáneos podrían observar el token como no revocado antes del `SET NX`; no provocan una revocación incorrecta y la semántica secuencial requerida 204 → 401 está validada.
