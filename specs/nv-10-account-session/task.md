# NV-10 — Sesión: refresh token y cuenta propia — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 11 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Refresh token en el login** — Requisito: NV-10-R01 · Fase: F1 · Aceptación: Login 200 devuelve `refreshToken` además de los campos actuales sin cambiar su forma; el access token incluye `sid`; en la base solo queda el hash; prueba de contrato que compara la respuesta con la de NV-05. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Renovación con rotación** — Requisito: NV-10-R02 · Fase: F1 · Aceptación: `POST /auth/refresh` con token válido devuelve access y refresh nuevos y el anterior deja de servir; token desconocido, vencido o revocado 401 uniforme; cuerpo inválido 400; vencimiento de 30 días por token y 90 por sesión. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Cuenta propia** — Requisito: NV-10-R03 · Fase: F1 · Aceptación: `GET /users/me` con Bearer válido devuelve exactamente `id`, `email` y `createdAt` del `sub`; sin token o con token revocado 401; `Cache-Control: no-store`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Persistencia e índices** — Requisito: NV-10-R04 · Fase: F2 · Aceptación: `refresh_tokens` con índice único en `tokenHash`, índice por `sessionId` e índice TTL; búsqueda de usuario por id con proyección explícita. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · Detección de reuso y concurrencia** — Requisito: NV-10-R05 · Fase: F2 · Aceptación: Reusar un token rotado revoca toda la sesión y responde 401; dos refresh simultáneos con el mismo token producen una sola emisión; pruebas de ambos casos. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Logout integrado** — Requisito: NV-10-R06 · Fase: F2 · Aceptación: Logout sigue respondiendo 204 y revoca el `jti` como hoy; con `sid` revoca también la sesión y su refresh token deja de servir; un access token sin `sid` sigue funcionando hasta su expiración. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Rechazo inmediato de sesiones revocadas** — Requisito: NV-10-R07 · Fase: F3 · Aceptación: Tras el logout o un reuso, cualquier access token de esa sesión recibe 401 en la siguiente solicitud, en cualquier réplica (estado en Redis). · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Límites y no regresión** — Requisito: NV-10-R08 · Fase: F3 · Aceptación: Rate limit de refresh con 429 y `Retry-After`; fallo de Redis 503; pruebas de regresión de register 201, login 200 y logout 204 con los contratos actuales. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-10-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-10-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-10-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
| T08 | R08 | F3 | ABIERTA |
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
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
