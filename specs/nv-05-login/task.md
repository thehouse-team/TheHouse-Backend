# NV-05 — Task list Login U2

| Campo | Valor |
| --- | --- |
| ID | NV-05 |
| Documento | `task.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Spec | `spec.md` 1.0.0-u2 |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks totales | 11 |
| Tasks U2 | 11 |
| Tasks completadas | 11 |

## Reglas

1. Todas las tasks U2 fueron ejecutadas y cerradas con evidencia.
2. Solo se ejecutan con **Alcance: U2** y autorización de Fase B.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. No se implementa refresh ni endpoints fuera de `POST /api/v1/auth/login`.

## F1 — Contrato y validación

- [x] **T01 · Contrato y DTOs de login** — Requisito: NV-05-R01 · Fase: F1 · **Alcance: U2** · Aceptación: `LoginRequest` con `email`/`password`; `LoginResponse` con `accessToken`/`tokenType`/`expiresIn`; campos desconocidos rechazados; sin refresh/contraseña/hash. · Evidencia: contrato y pruebas de serialización. · Depende de: autorización Fase B.
- [x] **T02 · Validación y verificación genérica de credenciales** — Requisito: NV-05-R02 · Fase: F1 · **Alcance: U2** · Aceptación: normalización, validación, búsqueda de usuario, hash dummy y 401 genérico para usuario inexistente o contraseña incorrecta. · Evidencia: pruebas unitarias y de timing. · Depende de: T01.

## F2 — JWT y seguridad

- [x] **T03 · Servicio JWT HS256** — Requisito: NV-05-R03 · Fase: F2 · **Alcance: U2** · Aceptación: firma/validación HS256, clave de entorno >=32 bytes, issuer `thehouse-api`, audience `thehouse-clients`, 900 s, clock skew 30 s, claims definidos y `jti`. · Evidencia: pruebas de claims, firma y expiración. · Depende de: T01.
- [x] **T04 · Caso de uso de login** — Requisitos: NV-05-R01, NV-05-R02, NV-05-R03, NV-05-R06 · Fase: F2 · **Alcance: U2** · Aceptación: valida, verifica, emite token y devuelve respuesta sin refresh/contraseña/hash; no registra secretos. · Evidencia: pruebas unitarias e integración. · Depende de: T02, T03.
- [x] **T07 · Logging seguro y cabeceras** — Requisito: NV-05-R06 · Fase: F2 · **Alcance: U2** · Aceptación: sin contraseña/token en logs; `Cache-Control: no-store`; token no aceptado por query; errores genéricos. · Evidencia: pruebas de logs y cabeceras. · Depende de: T04.

## F3 — Rate limiting y errores

- [x] **T05 · Rate limiting distribuido de login** — Requisito: NV-05-R04 · Fase: F3 · **Alcance: U2** · Aceptación: 5/60 s por IP+email y 20/60 s por IP en Redis; 429 con `Retry-After: 60`; Redis caído 503. · Evidencia: pruebas de límite y fallo. · Depende de: T01.
- [x] **T06 · Endpoint y Problem Details de login** — Requisitos: NV-05-R01, NV-05-R05 · Fase: F3 · **Alcance: U2** · Aceptación: 200/400/401/415/429/503/500 con Problem Details y `traceId`; sin stack trace ni detalles internos. · Evidencia: pruebas de integración y contrato. · Depende de: T04, T05.

## F4 — Pruebas y evidencia

- [x] **T08 · Pruebas unitarias de login** — Requisitos: NV-05-R02, NV-05-R03, NV-05-R06, NV-05-R07 · Fase: F4 · **Alcance: U2** · Aceptación: validación, hash dummy, claims, firma, expiración y logs cubiertos. · Evidencia: reporte de ejecución. · Depende de: T02, T03.
- [x] **T09 · Pruebas de integración de login** — Requisitos: NV-05-R04, NV-05-R05, NV-05-R07 · Fase: F4 · **Alcance: U2** · Aceptación: 200/400/401/415/429/503, `no-store`, token válido, rate limit y dependencias aisladas. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T05, T06.
- [x] **T10 · Pruebas de seguridad de login** — Requisitos: NV-05-R02, NV-05-R04, NV-05-R06, NV-05-R07 · Fase: F4 · **Alcance: U2** · Aceptación: enumeración/timing, logs sin secretos, token no aceptado por query y rate limit compartido. · Evidencia: informe de seguridad. · Depende de: T04, T05, T07.
- [x] **T11 · Evidencia y revisión de login** — Requisito: NV-05-R07 · Fase: F4 · **Alcance: U2** · Aceptación: cada requisito tiene evidencia o brecha declarada; revisión PASS/PASS WITH WARNINGS/BLOCKED. · Evidencia: matriz y acta. · Depende de: T08, T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01 | F1 | U2 | COMPLETADA |
| T02 | R02 | F1 | U2 | COMPLETADA |
| T03 | R03 | F2 | U2 | COMPLETADA |
| T04 | R01, R02, R03, R06 | F2 | U2 | COMPLETADA |
| T05 | R04 | F3 | U2 | COMPLETADA |
| T06 | R01, R05 | F3 | U2 | COMPLETADA |
| T07 | R06 | F2 | U2 | COMPLETADA |
| T08 | R02, R03, R06, R07 | F4 | U2 | COMPLETADA |
| T09 | R04, R05, R07 | F4 | U2 | COMPLETADA |
| T10 | R02, R04, R06, R07 | F4 | U2 | COMPLETADA |
| T11 | R07 | F4 | U2 | COMPLETADA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T04, T06 |
| R02 | T02, T04, T08, T10 |
| R03 | T03, T04, T08 |
| R04 | T05, T09, T10 |
| R05 | T06, T09 |
| R06 | T04, T07, T08, T10 |
| R07 | T08, T09, T10, T11 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 11 |
| Tasks U2 | 11 |
| Tasks abiertas | 0 |
| Tasks completadas | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
## Cierre U2 NV-05

| Campo | Resultado |
| --- | --- |
| Resultado de revisión | **PASS WITH WARNINGS** |
| Estado | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Workflow | `U2 Linux Validation` |
| Run | `41746514844` |
| Job | `126300775630` |
| Artifact | `u2-test-results` |
| Branch | `feature/u2-entrega` |
| Build Linux .NET 10 | PASS |
| Pruebas automatizadas | PASS |
| OpenAPI | PASS |
| Revisión de seguridad | PASS WITH WARNINGS |

### Advertencias declaradas

1. La ejecución CI valida la implementación y los adaptadores, pero no demuestra todavía conectividad contra instancias cloud reales de MongoDB y Redis. Esa evidencia corresponde a la fase de infraestructura/cloud.
2. La mitigación de enumeración temporal utiliza verificación PBKDF2 dummy para usuarios inexistentes. No se realizó en NV-05 un benchmark estadístico específico de diferencias temporales entre usuario inexistente y contraseña incorrecta.
3. Logout, revocación y ciclo de vida posterior del token pertenecen a NV-06 y quedan fuera de NV-05.
