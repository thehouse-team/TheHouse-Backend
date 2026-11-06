# NV-12 — Arrendador, edificios y cuartos — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 11 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Alta de arrendador** — Requisito: NV-12-R01 · Fase: F1 · Aceptación: Activa el modo arrendador una sola vez por cuenta (409 si ya existe); valida RFC si es inmobiliaria y registra la aceptación de términos. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Gestión de edificios** — Requisito: NV-12-R02 · Fase: F1 · Aceptación: Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo y servicios; borrado bloqueado con cuartos Rentados. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Generación y edición de cuartos** — Requisito: NV-12-R03 · Fase: F1 · Aceptación: `rooms:generate` crea cuartos numerados por piso con datos base; edición, duplicado, copia de datos, pausa y eliminación respetan los estados. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T04 · Máquina de estados** — Requisito: NV-12-R04 · Fase: F1 · Aceptación: Transiciones válidas de edificio y cuarto implementadas en el dominio; transiciones inválidas 409; pruebas de cada transición. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T05 · Cálculo de distancia** — Requisito: NV-12-R05 · Fase: F2 · Aceptación: Distancia en km y minutos a pie a la universidad elegida, recalculados al cambiar la ubicación o la universidad. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Persistencia e índices** — Requisito: NV-12-R06 · Fase: F2 · Aceptación: Colecciones con índices únicos y geoespaciales; concurrencia optimista con `version`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.
- [ ] **T07 · Publicación y tablero** — Requisito: NV-12-R07 · Fase: F2 · Aceptación: `publication` lista pasos pendientes; `:publish` pasa a En revisión solo si se cumplen; Mis alojamientos devuelve totales, libres, ocupación e ingreso mensual. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04.

## F3 — Seguridad, autorización y límites

- [ ] **T08 · Autorización del dueño** — Requisito: NV-12-R08 · Fase: F3 · Aceptación: Ningún endpoint permite leer o modificar edificios o cuartos ajenos; pruebas con dos arrendadores. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07.

## F4 — Pruebas y evidencia

- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-12-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-12-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-12-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

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
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
