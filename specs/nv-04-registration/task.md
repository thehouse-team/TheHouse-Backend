# NV-04 — Task list Registration U2

| Campo | Valor |
| --- | --- |
| ID | NV-04 |
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

1. Todas las tasks U2 fueron ejecutadas y validadas.
2. La Fase B fue autorizada y ejecutada únicamente para el alcance U2.
3. Cada task declara al menos un requisito.
4. La aprobación documental no completa ninguna task.
5. No se implementa nada fuera de `POST /api/v1/auth/register`.

## F1 — Contrato y validación

- [x] **T01 · Contrato y DTOs de registro** — Requisito: NV-04-R01 · Fase: F1 · **Alcance: U2** · Aceptación: `RegisterRequest` solo `email`/`password`; `RegisterResponse` con `id`/`email`/`createdAt`; campos desconocidos rechazados; sin contraseña ni hash. · Evidencia: contrato revisado y pruebas de serialización. · Depende de: autorización Fase B.
- [x] **T02 · Validación y normalización** — Requisito: NV-04-R02 · Fase: F1 · **Alcance: U2** · Aceptación: email trim/lowercase, máximo 254, formato válido; password 12–128 sin recorte; cuerpo máximo 8 KB; errores 400. · Evidencia: pruebas unitarias y casos negativos. · Depende de: T01.

## F2 — Persistencia y seguridad

- [x] **T03 · Modelo de usuario, repositorio e índice único** — Requisito: NV-04-R03 · Fase: F2 · **Alcance: U2** · Aceptación: colección `users`, campos mínimos, email normalizado, índice único `{email:1}`, duplicado detectado por el índice. · Evidencia: pruebas de repositorio y revisión de índices. · Depende de: T01.
- [x] **T04 · Servicio de hashing PBKDF2** — Requisito: NV-04-R04 · Fase: F2 · **Alcance: U2** · Aceptación: PBKDF2-HMAC-SHA256 con APIs de .NET; factor de trabajo centralizado/configurable (default 210,000, mínimo 210,000 validado); salt 128 bits único por contraseña generado con CSPRNG; hash 256 bits; formato versionado con parámetros; comparación constante. · Evidencia: pruebas unitarias y revisión. · Depende de: T01.
- [x] **T05 · Caso de uso de registro** — Requisitos: NV-04-R03, NV-04-R04, NV-04-R06 · Fase: F2 · **Alcance: U2** · Aceptación: valida, normaliza, hashea e inserta; mapea duplicado a 409; no acepta campos internos; no registra contraseña. · Evidencia: pruebas unitarias e integración. · Depende de: T02, T03, T04.

## F3 — Endpoint, errores y límites

- [x] **T06 · Endpoint POST /api/v1/auth/register** — Requisitos: NV-04-R01, NV-04-R05 · Fase: F3 · **Alcance: U2** · Aceptación: 201 con `id`/`email`/`createdAt`; 400/409/415/503; sin contraseña ni hash. · Evidencia: pruebas de integración y respuestas. · Depende de: T05.
- [x] **T07 · Problem Details de registro** — Requisito: NV-04-R05 · Fase: F3 · **Alcance: U2** · Aceptación: errores con `application/problem+json`, `traceId` y sin stack trace/rutas/colecciones. · Evidencia: pruebas de contrato. · Depende de: T06.
- [x] **T08 · Rate limiting de registro** — Requisito: NV-04-R07 · Fase: F3 · **Alcance: U2** · Aceptación: 10 solicitudes/60 s por IP en Redis, 429 con `Retry-After: 60`, fallo de Redis 503. · Evidencia: pruebas de límite y fallo. · Depende de: T01.

## F4 — Pruebas y evidencia

- [x] **T09 · Pruebas unitarias de registro** — Requisitos: NV-04-R02, NV-04-R04, NV-04-R06, NV-04-R08 · Fase: F4 · **Alcance: U2** · Aceptación: normalización, validación, hash/verificación, salt único/CSPRNG, factor de trabajo configurado, DTO y mass assignment cubiertos. · Evidencia: reporte de ejecución. · Depende de: T02, T04.
- [x] **T10 · Pruebas de integración de registro** — Requisitos: NV-04-R01, NV-04-R03, NV-04-R05, NV-04-R07, NV-04-R08 · Fase: F4 · **Alcance: U2** · Aceptación: 201, 400, 409, 415, 429, email normalizado, sin contraseña en respuesta y Mongo/Redis aislados. · Evidencia: reporte y respuestas sanitizadas. · Depende de: T05, T06, T07, T08.
- [x] **T11 · Pruebas de seguridad y evidencia** — Requisitos: NV-04-R02, NV-04-R04, NV-04-R06, NV-04-R08 · Fase: F4 · **Alcance: U2** · Aceptación: mass assignment, logs sin contraseña, rate limit y errores sin stack trace; evidencia por requisito. · Evidencia: informe de seguridad y matriz. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01 | F1 | U2 | COMPLETADA |
| T02 | R02 | F1 | U2 | COMPLETADA |
| T03 | R03 | F2 | U2 | COMPLETADA |
| T04 | R04 | F2 | U2 | COMPLETADA |
| T05 | R03, R04, R06 | F2 | U2 | COMPLETADA |
| T06 | R01, R05 | F3 | U2 | COMPLETADA |
| T07 | R05 | F3 | U2 | COMPLETADA |
| T08 | R07 | F3 | U2 | COMPLETADA |
| T09 | R02, R04, R06, R08 | F4 | U2 | COMPLETADA |
| T10 | R01, R03, R05, R07, R08 | F4 | U2 | COMPLETADA |
| T11 | R02, R04, R06, R08 | F4 | U2 | COMPLETADA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T06, T10 |
| R02 | T02, T09, T11 |
| R03 | T03, T05, T10 |
| R04 | T04, T05, T09, T11 |
| R05 | T06, T07, T10 |
| R06 | T05, T09, T11 |
| R07 | T08, T10 |
| R08 | T09, T10, T11 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Tasks | 11 |
| Tasks U2 | 11 |
| Tasks abiertas | 0 |
| Tasks completadas | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
