# NV-17 — Visitas: solicitudes (v1) — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 12 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Solicitar visita** — Requisito: NV-17-R01 · Fase: F1 · Aceptación: `POST /visits` con propiedad existente y horario válido crea una visita `requested` (201); propiedad inexistente 404; formato inválido o fecha sin desplazamiento 400. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Reglas de horario y zona** — Requisito: NV-17-R02 · Fase: F1 · Aceptación: Rechaza con 422 horarios a menos de 2 h, a más de 60 días, fuera de 08:00–20:00 en `America/Mexico_City` o no múltiplos de 15 min; respuestas en UTC con `timeZone`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Estados y transiciones** — Requisito: NV-17-R03 · Fase: F1 · Aceptación: Máquina de estados con `requested` → `cancelled`; estados futuros reservados; transiciones inválidas 409; `history` registrado. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Consultar y listar** — Requisito: NV-17-R04 · Fase: F1 · Aceptación: `GET /visits/{id}` (solo propias, ajena 404) y `GET /users/me/visits` paginado, filtrable por estado, orden `scheduledAt` asc. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T05 · Cancelar** — Requisito: NV-17-R05 · Fase: F1 · Aceptación: `POST /visits/{id}/cancel` cancela una visita `requested` futura (200); repetir responde 200; visita pasada 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T06 · Idempotencia** — Requisito: NV-17-R06 · Fase: F2 · Aceptación: Misma `Idempotency-Key` y mismo cuerpo dentro de 24 h devuelven la visita original (200, `Idempotent-Replayed`); cuerpo distinto 409; sin llave 400; prueba de reintentos y doble toque. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [ ] **T07 · Conflictos y concurrencia** — Requisito: NV-17-R07 · Fase: F2 · Aceptación: Visita `requested` duplicada para la misma propiedad, otra visita a menos de 60 min o más de 5 activas → 409; pruebas concurrentes con el lock por usuario y el índice único parcial. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.

## F3 — Seguridad, autorización y límites

- [ ] **T08 · Autorización y límites** — Requisito: NV-17-R08 · Fase: F3 · Aceptación: Sin `userId` del cliente; pruebas con dos usuarios; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07.

## F4 — Pruebas y evidencia

- [ ] **T09 · Contrato para Mobile y despliegue** — Requisito: NV-17-R09 · Fase: F4 · Aceptación: Contrato de la §6.1 entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (crear, repetir con la misma llave, conflicto, listar, consultar, cancelar). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas unitarias y de integración** — Requisito: NV-17-R10 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- [ ] **T11 · Pruebas de seguridad** — Requisito: NV-17-R10 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- [ ] **T12 · Evidencia y revisión final** — Requisito: NV-17-R10 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T10, T11.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F1 | ABIERTA |
| T05 | R05 | F1 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F2 | ABIERTA |
| T08 | R08 | F3 | ABIERTA |
| T09 | R09 | F4 | ABIERTA |
| T10 | R10 | F4 | ABIERTA |
| T11 | R10 | F4 | ABIERTA |
| T12 | R10 | F4 | ABIERTA |

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
| R09 | T09 |
| R10 | T10, T11, T12 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 10 |
| Tasks | 12 |
| Tasks abiertas | 12 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
