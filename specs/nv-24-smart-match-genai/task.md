# NV-24 — Smart Match — explicaciones con TheHouse-GenAI — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-24-smart-match-genai/`; activar `TheHouse-GenAI` en Production requiere las decisiones M-BE de su hosting y de su proveedor) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |
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

- [ ] **T01 · Hechos estructurados** — Requisito: NV-24-R01 · Fase: F1 · Aceptación: Los hechos se arman solo desde NV-23 y los datos públicos del cuarto; prueba de que no incluyen descripción libre, dirección, coordenadas ni datos del usuario. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Ventajas, desventajas y plantilla** — Requisito: NV-24-R02 · Fase: F1 · Aceptación: Ventajas y desventajas por umbral del desglose y resumen por plantilla en español de México, deterministas, sin porcentajes por factor y con máximo de 600 caracteres; casos de prueba fijos. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Endpoints de explicación** — Requisito: NV-24-R03 · Fase: F1 · Aceptación: `explanations/{roomId}` con sesión y `explanations/query` para invitados; 404 cuarto no publicado o no elegible; 422 perfil incompleto. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Cliente de TheHouse-GenAI** — Requisito: NV-24-R04 · Fase: F2 · Aceptación: `GenAiExplanationClient` con el contrato `v1` de la §6, credencial de servicio, 3 s de timeout, un reintento y circuit breaker; probado con un doble de GenAI, sin red. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · Validación y degradación segura** — Requisito: NV-24-R05 · Fase: F2 · Aceptación: Respuestas con números, servicios o universidades ausentes, demasiado largas o con formato inválido usan la plantilla; con GenAI caído, lento, desactivado o con el circuito abierto responde 200 con `generatedBy=template`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Caché y límites** — Requisito: NV-24-R06 · Fase: F2 · Aceptación: Caché de 24 h por cuarto, preferencias y versiones; rate limits con 429; Redis caído 503. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Protección contra inyección y minimización** — Requisito: NV-24-R07 · Fase: F3 · Aceptación: Pruebas con descripciones maliciosas del arrendador que no llegan a GenAI ni alteran la salida; ningún dato personal en la solicitud a GenAI ni en logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-24-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD con GenAI desactivado; prueba HTTP real en Azure con plantilla; activar GenAI en Production solo con sus decisiones M-BE aprobadas y una prueba real adicional. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-24-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

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
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
