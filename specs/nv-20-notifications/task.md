# NV-20 — Notificaciones — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
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

- [ ] **T01 · Generación desde las acciones** — Requisito: NV-20-R01 · Fase: F1 · Aceptación: Cada tipo del catálogo se crea para el destinatario correcto cuando ocurre su acción en NV-16, NV-17, NV-18 o NV-21; repetir el hecho no duplica; quien actúa no se notifica; si MongoDB falla la acción principal no se deshace. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Textos sin datos sensibles** — Requisito: NV-20-R02 · Fase: F1 · Aceptación: Cada tipo produce `title` y `body` en español con nombre sin apellidos y título del cuarto; ninguno contiene dirección, teléfono, correo, texto del chat, notas ni ids. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Bandeja y filtros** — Requisito: NV-20-R03 · Fase: F2 · Aceptación: Lista paginada por `updatedAt` desc con filtros `unread` y `category`; validación de parámetros. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Marcar como leídas** — Requisito: NV-20-R04 · Fase: F2 · Aceptación: Una (idempotente; ajena 404) o todas, opcionalmente por categoría; leer el chat marca la notificación de esa conversación. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T05 · Contador eficiente** — Requisito: NV-20-R05 · Fase: F2 · Aceptación: Total y por categoría con índice; `ETag` débil, `private, no-cache` y 304 cuando no cambió. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T06 · Agrupación de mensajes y retención** — Requisito: NV-20-R06 · Fase: F2 · Aceptación: Una notificación no leída por conversación con `count`; TTL de 90 días. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Privacidad, autorización y límites** — Requisito: NV-20-R07 · Fase: F3 · Aceptación: Pruebas con dos usuarios; rate limits 429; Redis caído 503; campos desconocidos 400. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-20-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitud, aceptación, mensaje agrupado, visita confirmada, convenio, contador con 304 y marcar leídas). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-20-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-20-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-20-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
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
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
