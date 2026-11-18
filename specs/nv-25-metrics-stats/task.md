# NV-25 — Estadísticas del arrendador — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T09; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-25-metrics-stats/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 9 |
| Tasks completadas | 9 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Registro de vistas** — Requisito: NV-25-R01 · Fase: F1 · Aceptación: Una vista por persona, cuarto o edificio y día con `actorHash` de sal diaria; sin vistas del dueño; dos réplicas no duplican; si falla, el detalle público responde igual. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Resumen del arrendador** — Requisito: NV-25-R02 · Fase: F1 · Aceptación: Totales del rango, periodo anterior y serie diaria con conversión; solo datos propios; rango validado (90 días). · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T03 · Estadísticas por cuarto** — Requisito: NV-25-R03 · Fase: F2 · Aceptación: Vistas, favoritos, solicitudes, visitas, convenios, conversión y calificación por cuarto; paginado y ordenado; filtro por edificio propio. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [x] **T04 · Sugerencias por reglas** — Requisito: NV-25-R04 · Fase: F2 · Aceptación: Las 6 reglas documentadas, cada una con una prueba que la dispara y otra que no; máximo 20; textos en español con números del arrendador. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [x] **T05 · Privacidad, autorización y límites** — Requisito: NV-25-R05 · Fase: F3 · Aceptación: Sin datos de usuarios en ninguna respuesta; otro arrendador o estudiante 403/404; 429; Redis caído 503 en estadísticas. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04.

## F4 — Pruebas y evidencia

- [x] **T06 · Contrato para Mobile y despliegue** — Requisito: NV-25-R06 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (vistas de un estudiante, favorito, visita y solicitud reflejados en el resumen, por cuarto y en sugerencias). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05.
- [x] **T07 · Pruebas unitarias y de integración** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06.
- [x] **T08 · Pruebas de seguridad** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06.
- [x] **T09 · Evidencia y revisión final** — Requisito: NV-25-R07 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T07, T08.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F2 | COMPLETA |
| T04 | R04 | F2 | COMPLETA |
| T05 | R05 | F3 | COMPLETA |
| T06 | R06 | F4 | COMPLETA |
| T07 | R07 | F4 | COMPLETA |
| T08 | R07 | F4 | COMPLETA |
| T09 | R07 | F4 | COMPLETA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06 |
| R07 | T07, T08, T09 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 9 |
| Tasks abiertas | 0 |
| Tasks completadas | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
