# NV-11 — Perfil, universidad y preferencias — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-11 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-11-profile-preferences/`; el seed de coordenadas en Production requiere autorización aparte, M-BE-043) |
| Estado de ejecución | **✅ COMPLETA — T01–T11 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 11 |
| Tasks completadas | 11 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Perfil propio** — Requisito: NV-11-R01 · Fase: F1 · Aceptación: `GET`/`PATCH /users/me/profile`; `name` de 1 a 80 caracteres; campos no permitidos 400; perfil inexistente responde 200 con valores vacíos. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Selección de universidad** — Requisito: NV-11-R02 · Fase: F1 · Aceptación: `PUT /users/me/university` valida que el id exista (404 si no; 400 si el formato es inválido), es idempotente y se refleja en `GET /users/me/profile`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Preferencias de búsqueda** — Requisito: NV-11-R03 · Fase: F1 · Aceptación: `PUT` valida rangos, catálogos y repetidos; rechaza `quickSetupCompleted` y `version` del cliente; incrementa `version`; deriva `quickSetupCompleted`; `GET` sin datos devuelve los valores por defecto. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T04 · Búsqueda de universidades por texto** — Requisito: NV-11-R04 · Fase: F2 · Aceptación: Encuentra por siglas, nombre o ciudad, parcial y sin distinguir acentos ni mayúsculas (`tula`, `UAEH`, `tepeji`); máximo 20; `q` fuera de 2–60 o combinado con coordenadas 400. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T05 · Búsqueda por cercanía** — Requisito: NV-11-R05 · Fase: F2 · Aceptación: Con `lat`/`lng` devuelve las universidades más cercanas con `distanceKm`, ordenadas; las 10 del seed tienen coordenadas verificadas y cargadas con seed autorizado. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T06 · Persistencia e índices** — Requisito: NV-11-R06 · Fase: F2 · Aceptación: Documento único por usuario en perfil y preferencias; índice `2dsphere` y `searchKey` en universidades; `GET /universities` y `/universities/{id}` sin cambios de contrato. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [x] **T07 · Autorización, límites y privacidad** — Requisito: NV-11-R07 · Fase: F3 · Aceptación: Sin `userId` del cliente; pruebas con dos usuarios; rate limit 429 con `Retry-After`; Redis caído 503; `no-store`; las coordenadas y el nombre no aparecen en logs (prueba que lo verifica). · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [x] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-11-R08 · Fase: F4 · Aceptación: Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (perfil, universidad, búsqueda por texto y cercanía, preferencias). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [x] **T09 · Pruebas unitarias y de integración** — Requisito: NV-11-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [x] **T10 · Pruebas de seguridad** — Requisito: NV-11-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [x] **T11 · Evidencia y revisión final** — Requisito: NV-11-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

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
| T08 | R08 | F4 | COMPLETA |
| T09 | R09 | F4 | COMPLETA |
| T10 | R09 | F4 | COMPLETA |
| T11 | R09 | F4 | COMPLETA |

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
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — T01–T11 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
