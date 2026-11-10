# NV-16 — Contacto, solicitudes y chat — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
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

- [ ] **T01 · Crear solicitud** — Requisito: NV-16-R01 · Fase: F1 · Aceptación: Valida preguntas del catálogo y mensaje; crea solicitud y conversación en la misma operación; 404 cuarto no publicado; 409 activa o límite; 422 propio cuarto; 409 participante suspendido. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Bandeja del estudiante y cancelación** — Requisito: NV-16-R02 · Fase: F1 · Aceptación: Lista, detalle y cancelación de sus solicitudes con la visita activa de NV-17; transiciones válidas; lo ajeno 404. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Bandeja del arrendador, aceptar y rechazar** — Requisito: NV-16-R03 · Fase: F1 · Aceptación: Lista por estado y cuarto, detalle, aceptar y rechazar con motivo; solo el dueño del cuarto; 409 en transiciones inválidas o cuarto no publicado. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Chat por consulta incremental** — Requisito: NV-16-R04 · Fase: F2 · Aceptación: Mensajes ordenados con cursor `after`, `hasMore` y `ETag`/304; envío idempotente por `clientMessageId`; conversación cerrada 409. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · No leídos y lectura** — Requisito: NV-16-R05 · Fase: F2 · Aceptación: `unreadCount` por conversación y `read` hasta un mensaje; nunca cuenta los propios mensajes. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Persistencia e índices** — Requisito: NV-16-R06 · Fase: F2 · Aceptación: Índice único parcial de solicitudes activas, único de `clientMessageId` y de lectura de mensajes por conversación; prueba de doble toque concurrente. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Privacidad y abuso** — Requisito: NV-16-R07 · Fase: F3 · Aceptación: Ninguna respuesta incluye teléfono, correo, apellidos ni ids de usuario; rate limits con 429; Redis caído 503; cuentas suspendidas no escriben; contenido fuera de logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-16-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure con un estudiante y un arrendador de prueba (solicitar, aceptar, rechazar, cancelar, chatear, 304 y no leídos). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-16-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-16-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-16-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

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
