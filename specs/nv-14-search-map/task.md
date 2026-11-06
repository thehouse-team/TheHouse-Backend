# NV-14 — Búsqueda, filtros y mapa — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Búsqueda con filtros** — Requisito: NV-14-R01 · Fase: F1 · Aceptación: Combina filtros de precio, tipo, modalidad, servicios y distancia; orden estable; paginación con máximo 50; solo cuartos publicables. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Mapa de rentas** — Requisito: NV-14-R02 · Fase: F1 · Aceptación: Marcadores dentro de un `bbox` válido, máximo 300, con precio y distancia a la universidad. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Conteo por presupuesto** — Requisito: NV-14-R03 · Fase: F1 · Aceptación: `rooms/count` devuelve el total de cuartos cercanos dentro del presupuesto con los mismos criterios de publicación. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Detalle con sesión** — Requisito: NV-14-R04 · Fase: F1 · Aceptación: Invitado recibe 401; con sesión devuelve el detalle público sin dirección exacta. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T05 · Búsqueda de colonias y ciudades** — Requisito: NV-14-R05 · Fase: F2 · Aceptación: Coincidencia sin acentos ni mayúsculas, máximo 10 resultados y lista vacía con 200 si no hay coincidencias. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Índices y rendimiento** — Requisito: NV-14-R06 · Fase: F2 · Aceptación: Índices geoespaciales y compuestos; prueba de carga básica que cumple p95 < 500 ms con datos de seed. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Ubicación aproximada y anti-extracción** — Requisito: NV-14-R07 · Fase: F3 · Aceptación: La ubicación aproximada es estable y está a 150–300 m de la real; límites de `bbox`, `pageSize` y rate limit verificados. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-14-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-14-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-14-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F1 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
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
