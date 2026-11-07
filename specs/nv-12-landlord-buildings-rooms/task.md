# NV-12 — Arrendador, edificios y cuartos — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-12-landlord-buildings-rooms/`) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 13 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Alta de arrendador** — Requisito: NV-12-R01 · Fase: F1 · Aceptación: `POST /landlord/profile` activa el modo una sola vez (409 `landlord_exists`); valida teléfono, RFC si es `agency` y la versión de términos vigente; `GET` devuelve el perfil o 404. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Gestión de edificios** — Requisito: NV-12-R02 · Fase: F1 · Aceptación: Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo, catálogo de servicios y universidad existente; límite de 20; borrado bloqueado con cuartos `rented`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Generación de cuartos** — Requisito: NV-12-R03 · Fase: F1 · Aceptación: `rooms/generate` crea cuartos numerados por piso (101, 102, 201…) con datos base; respeta 200 por edificio y no duplica números. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Edición y acciones de cuartos** — Requisito: NV-12-R04 · Fase: F1 · Aceptación: Editar, duplicar (siguiente número del piso), copiar datos de otro cuarto del mismo edificio, pausar, reanudar y borrar respetan estados y versiones. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T05 · Máquinas de estados** — Requisito: NV-12-R05 · Fase: F1 · Aceptación: Transiciones válidas de edificio y cuarto en el dominio, `setup` → `ready` automático al completar datos; transiciones inválidas 409 con `code`; prueba de cada transición. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T06 · Cálculo de distancia** — Requisito: NV-12-R06 · Fase: F2 · Aceptación: Distancia (1 decimal) y minutos a pie (5 km/h) a la universidad elegida, recalculados al cambiar ubicación o universidad; pruebas con coordenadas del seed. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [ ] **T07 · Persistencia y concurrencia** — Requisito: NV-12-R07 · Fase: F2 · Aceptación: Índices únicos y geoespacial; `version` incrementada en cada escritura; dos ediciones simultáneas con la misma versión: una gana y la otra recibe 409. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [ ] **T08 · Publicación y tablero** — Requisito: NV-12-R08 · Fase: F2 · Aceptación: `publication` lista pasos; `publish` pasa a `in_review` solo si se cumplen (409 `publication_incomplete` si no); el tablero devuelve edificios, cuartos, disponibles, ocupación e ingreso mensual de cuartos `rented`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.

## F3 — Seguridad, autorización y límites

- [ ] **T09 · Autorización y límites** — Requisito: NV-12-R09 · Fase: F3 · Aceptación: Pruebas con dos arrendadores y un usuario sin perfil (404 ajeno, 403 sin perfil); asignación masiva 400; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`; teléfono, RFC y dirección fuera de logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.

## F4 — Pruebas y evidencia

- [ ] **T10 · Contrato para Mobile y despliegue** — Requisito: NV-12-R10 · Fase: F4 · Aceptación: Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (alta, edificio, generar, editar, pausar, publicar, ajeno 404). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- [ ] **T11 · Pruebas unitarias y de integración** — Requisito: NV-12-R11 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T12 · Pruebas de seguridad** — Requisito: NV-12-R11 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T13 · Evidencia y revisión final** — Requisito: NV-12-R11 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T11, T12.

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
| T08 | R08 | F2 | ABIERTA |
| T09 | R09 | F3 | ABIERTA |
| T10 | R10 | F4 | ABIERTA |
| T11 | R11 | F4 | ABIERTA |
| T12 | R11 | F4 | ABIERTA |
| T13 | R11 | F4 | ABIERTA |

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
| R10 | T10 |
| R11 | T11, T12, T13 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 11 |
| Tasks | 13 |
| Tasks abiertas | 13 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
