# NV-15 — Favoritos — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `task.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-15-favorites-compare/`; la migración `--migrate-to-rooms` en Production requiere autorización aparte) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas; T07–T10 esperan CI, migración y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 6 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Favoritos de cuartos publicados** — Requisito: NV-15-R01 · Fase: F1 · Aceptación: `PUT` con un cuarto publicado responde 204 una sola vez; cuarto no publicado o inexistente 404; id inválido 400; límite 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Quitar favorito** — Requisito: NV-15-R02 · Fase: F1 · Aceptación: `DELETE` responde 204 exista o no; después el cuarto no aparece en el listado ni en `ids`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Listado e ids con datos de cuartos** — Requisito: NV-15-R03 · Fase: F1 · Aceptación: Listado con los campos del §6 y `ids` con `roomIds`; cuartos no publicados omitidos sin borrarse. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T04 · Transición desde propiedades** — Requisito: NV-15-R04 · Fase: F2 · Aceptación: Un id de propiedad se traduce a su cuarto en `PUT`/`DELETE`; las respuestas incluyen `propertyId`/`propertyIds`; `/properties` agrega `Deprecation` y `Link`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [x] **T05 · Migración de datos** — Requisito: NV-15-R05 · Fase: F2 · Aceptación: `--migrate-to-rooms` convierte favoritos existentes, elimina duplicados, reporta los no mapeados, cambia los índices y es idempotente. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [x] **T06 · Autorización y límites** — Requisito: NV-15-R06 · Fase: F3 · Aceptación: Pruebas con dos usuarios; rate limit 429; Redis caído 503; `no-store`. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Contrato para Mobile y despliegue** — Requisito: NV-15-R07 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; migración autorizada en Production; prueba HTTP real en Azure (agregar por cuarto y por propiedad, listar, ids, quitar). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-15-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-15-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-15-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F2 | COMPLETA |
| T05 | R05 | F2 | COMPLETA |
| T06 | R06 | F3 | COMPLETA |
| T07 | R07 | F4 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R08 | F4 | ABIERTA |
| T10 | R08 | F4 | ABIERTA |

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
| R08 | T08, T09, T10 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Tasks | 10 |
| Tasks abiertas | 4 |
| Tasks completadas | 6 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas; T07–T10 esperan CI, migración y Azure** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
