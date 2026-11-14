# NV-19 — Reseñas y calificaciones — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-13 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Elegibilidad verificada** — Requisito: NV-19-R01 · Fase: F1 · Aceptación: Visita confirmada pasada o convenio activo o terminado del autor en ese cuarto; el dueño 403 `own_room`; sin estancia 403 `no_stay`; la consulta de elegibilidad responde lo mismo que crear. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Crear y editar** — Requisito: NV-19-R02 · Fase: F1 · Aceptación: 9 criterios de 1 a 5 obligatorios al crear y comentario ≤ 1000; una por autor y cuarto (409 `review_exists`); editar solo el autor durante 30 días (409 `review_locked`); promedios de la reseña con un decimal. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Listado y resumen** — Requisito: NV-19-R03 · Fase: F2 · Aceptación: Reseñas publicadas del cuarto paginadas por fecha; resumen con promedio del cuarto, del arrendador (todas sus reseñas) y por criterio; sin reseñas, promedios `null`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Mis reseñas** — Requisito: NV-19-R04 · Fase: F2 · Aceptación: Lista del autor con su estado, incluidas las ocultas. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T05 · Moderación** — Requisito: NV-19-R05 · Fase: F2 · Aceptación: Reporte con objetivo `review` en NV-21; ocultar y mostrar solo con rol `admin`, motivo y bitácora; una oculta deja de contar en los promedios. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [ ] **T06 · Privacidad, autorización y límites** — Requisito: NV-19-R06 · Fase: F3 · Aceptación: Pruebas con autor, arrendador, tercero y admin; autor solo con primer nombre; rate limits 429; Redis caído 503; campos desconocidos 400. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Contrato para Mobile y despliegue** — Requisito: NV-19-R07 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (no elegible, elegible por renta, crear, listar con resumen, editar, reportar y ocultar). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-19-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-19-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-19-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F3 | ABIERTA |
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
| Tasks abiertas | 10 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
