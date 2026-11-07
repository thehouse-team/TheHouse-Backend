# NV-14 — Búsqueda, filtros y mapa — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
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

- [ ] **T01 · Búsqueda con filtros** — Requisito: NV-14-R01 · Fase: F1 · Aceptación: Combina precio, tipo de cuarto, baño, tipo de edificio, con quién vivir, servicios y distancia; orden `distance`, `price` o `newest` estable con id; paginación máximo 50; solo cuartos publicables. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Mapa de rentas** — Requisito: NV-14-R02 · Fase: F1 · Aceptación: Marcadores dentro de un `bbox` válido (máximo 0.3° por lado), máximo 300, con precio y distancia; `bbox` inválido 400. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Conteo por presupuesto** — Requisito: NV-14-R03 · Fase: F1 · Aceptación: `rooms/count` devuelve el total con los mismos criterios que la búsqueda. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Detalle con sesión** — Requisito: NV-14-R04 · Fase: F1 · Aceptación: Invitado 401; con sesión devuelve el detalle sin dirección, coordenadas reales ni dueño; cuarto no publicado 404. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T05 · Colonias y ciudades** — Requisito: NV-14-R05 · Fase: F2 · Aceptación: Coincidencia parcial sin acentos ni mayúsculas, máximo 10, lista vacía con 200; seed autorizado desde OpenStreetMap. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Ubicación aproximada** — Requisito: NV-14-R06 · Fase: F2 · Aceptación: Centro de celda estable para el mismo edificio y a menos de 250 m de la ubicación real; distancias calculadas desde la celda; prueba de que dos universidades no permiten triangular más allá de la celda. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T07 · Datos de demostración** — Requisito: NV-14-R07 · Fase: F2 · Aceptación: `--seed-demo-rooms` crea los 12 cuartos `demo` verificados, idempotente, con arrendador de sistema sin acceso y mapeo propiedad → cuarto. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.

## F3 — Seguridad, autorización y límites

- [ ] **T08 · Índices y rendimiento** — Requisito: NV-14-R08 · Fase: F3 · Aceptación: Índices geoespaciales y compuestos; prueba de carga básica con p95 < 500 ms en Azure con los datos de demostración. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Anti-extracción y límites** — Requisito: NV-14-R09 · Fase: F3 · Aceptación: Límites de `pageSize`, `bbox` y marcadores; rate limits con 429; Redis caído 503; ningún campo privado en respuestas públicas. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07.

## F4 — Pruebas y evidencia

- [ ] **T10 · Contrato para Mobile y despliegue** — Requisito: NV-14-R10 · Fase: F4 · Aceptación: Contrato de la §6.1 entregado a Mobile con URL desplegada; seeds autorizados en Production; prueba HTTP real en Azure (búsqueda, mapa, conteo, detalle, colonias). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- [ ] **T11 · Pruebas unitarias y de integración** — Requisito: NV-14-R11 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T12 · Pruebas de seguridad** — Requisito: NV-14-R11 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T13 · Evidencia y revisión final** — Requisito: NV-14-R11 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T11, T12.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F1 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F2 | ABIERTA |
| T08 | R08 | F3 | ABIERTA |
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
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
