# NV-24 — Smart Match — explicaciones con TheHouse-GenAI — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `task.md` |
| Versión | 1.2.1 |
| Fecha | 2026-11-24 |
| Estado documental | **✅ APROBADA — 1.2.1** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-24 («Apruebo NV-24 1.2.1 y EJECUTA»: hosting en Container Apps interno, costo $0 con la evidencia del equipo de GenAI del 2026-11-22 y privacidad del nivel gratuito; «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas, `scripts/db/` (regenerado por la prueba de NV-26) y `specs/nv-24-smart-match-genai/`; la clave de servicio la carga Victor como secreto; sin paquetes nuevos) |
| Estado de ejecución | **✅ COMPLETADA — 1.2.1: 11 de 11 (2026-11-25), PASS WITH WARNINGS; explicaciones redactadas por TheHouse-GenAI en producción (prueba real 5/5) con plantilla de respaldo; ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.1 |
| Plan | `plan.md` 1.2.1 |
| Tasks totales | 11 |
| Tasks completadas | 11 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Hechos estructurados** — Requisito: NV-24-R01 · Fase: F1 · Aceptación: Los hechos se arman solo desde NV-23 y los datos públicos del cuarto; prueba de que no incluyen descripción libre, dirección, coordenadas ni datos del usuario. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Ventajas, desventajas y plantilla** — Requisito: NV-24-R02 · Fase: F1 · Aceptación: Ventajas y desventajas por umbral del desglose y resumen por plantilla en español de México, deterministas, sin porcentajes por factor y con máximo de 600 caracteres; casos de prueba fijos. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Endpoints de explicación** — Requisito: NV-24-R03 · Fase: F1 · Aceptación: `explanations/{roomId}` con sesión y `explanations/query` para invitados; 404 cuarto no publicado o no elegible; 422 perfil incompleto. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T04 · Cliente de TheHouse-GenAI** — Requisito: NV-24-R04 · Fase: F2 · Aceptación: `GenAiExplanationClient` con el contrato congelado (camelCase, sin campos extra, sin id del cuarto), `X-Service-Key`, `X-Request-ID`, 8 s de timeout, reintento solo en red/5xx, 429 con `Retry-After` y circuit breaker; mapeo de servicios; probado con un doble de GenAI que reproduce sus respuestas y errores, sin red. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T05 · Validación y degradación segura** — Requisito: NV-24-R05 · Fase: F2 · Aceptación: `generated: false`, respuestas con números, servicios o universidades ausentes, demasiado largas o con formato inválido usan la plantilla; con GenAI caído, lento, desactivado, sin clave o con el circuito abierto responde 200 con `generatedBy=template`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T06 · Caché y límites** — Requisito: NV-24-R06 · Fase: F2 · Aceptación: Caché de 24 h por cuarto, preferencias y versiones; rate limits con 429; Redis caído 503. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [x] **T07 · Protección contra inyección y minimización** — Requisito: NV-24-R07 · Fase: F3 · Aceptación: Pruebas con descripciones maliciosas del arrendador que no llegan a GenAI ni alteran la salida; ningún dato personal en la solicitud a GenAI ni en logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [x] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-24-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD con GenAI desactivado; prueba HTTP real en Azure con plantilla; activar GenAI en Production con la URL, la clave como secreto y la evidencia de costo, y una prueba real adicional con `generatedBy=genai`. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [x] **T09 · Pruebas unitarias y de integración** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [x] **T10 · Pruebas de seguridad** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [x] **T11 · Evidencia y revisión final** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F2 | COMPLETA |
| T05 | R05 | F2 | COMPLETA |
| T06 | R06 | F2 | COMPLETA |
| T07 | R07 | F3 | COMPLETA |
| T08 | R08 | F4 | COMPLETADA |
| T09 | R09 | F4 | COMPLETA |
| T10 | R09 | F4 | COMPLETA |
| T11 | R09 | F4 | COMPLETADA |

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
| Tasks abiertas | 0 |
| Tasks completadas | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA — 1.2.1** |
| Estado de ejecución | **✅ COMPLETADA — 1.2.1: 11 de 11 (2026-11-25), PASS WITH WARNINGS; explicaciones redactadas por TheHouse-GenAI en producción (prueba real 5/5) con plantilla de respaldo; ver `evidence.md`** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
