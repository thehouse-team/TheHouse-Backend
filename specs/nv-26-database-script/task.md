# NV-26 — Script de la base de datos para la entrega — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-26 |
| Documento | `task.md` |
| Versión | 1.0.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo y EJECUTA NV-26»: aprobación de 1.0.0 con las decisiones de la §13 y «EJECUTA» de T01–T07; rutas `scripts/db/`, `TheHouse.IntegrationTests/` y `specs/nv-26-database-script/`; sin cambios en el código de la API ni comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.0.0: T01–T07 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0 |
| Plan | `plan.md` 1.0.0 |
| Tasks totales | 7 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Script de esquema** — Requisito: NV-26-R01 · Fase: F1 · Aceptación: `01-schema.mongodb.js` crea las 31 colecciones, todos los índices con nombre y opciones del código y los 2 buckets de GridFS; repetirlo no falla. · Evidencia: archivos de `scripts/db/` y comando de exportación. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Script de catálogos** — Requisito: NV-26-R02 · Fase: F1 · Aceptación: `02-catalogs.mongodb.js` carga 10 universidades, 12 propiedades y 60 lugares con *upsert*, generado desde los datos del código; sin datos personales. · Evidencia: archivos de `scripts/db/` y comando de exportación. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Alineación con el código** — Requisito: NV-26-R03 · Fase: F2 · Aceptación: Prueba automática que compara índices y colecciones del código con los del script, en ambos sentidos. · Evidencia: prueba de alineación y ejecución real contra MongoDB. · Depende de: T01, T02.
- [ ] **T04 · Ejecución real** — Requisito: NV-26-R04 · Fase: F2 · Aceptación: El script corre contra el MongoDB efímero (el mismo del CI) y se verifican colecciones, índices y conteos de catálogos; documentado en `scripts/db/README.md`. · Evidencia: prueba de alineación y ejecución real contra MongoDB. · Depende de: T01, T02.

## F4 — Pruebas y evidencia

- [ ] **T05 · Pruebas automáticas** — Requisito: NV-26-R05 · Fase: F4 · Aceptación: Prueba de alineación código ↔ script en ambos sentidos, prueba de idempotencia (ejecutar dos veces) y prueba de que los catálogos no contienen correos, teléfonos ni datos de usuarios; todas en el CI con MongoDB aislado. · Evidencia: TRX del run de CI. · Depende de: T01, T02, T03, T04.
- [ ] **T06 · Revisión de seguridad** — Requisito: NV-26-R05 · Fase: F4 · Aceptación: Sin cadenas de conexión, usuarios ni contraseñas en `scripts/db/`; Gitleaks limpio; el script no imprime la conexión. · Evidencia: Gitleaks del CI y revisión manual. · Depende de: T01, T02, T03, T04.
- [ ] **T07 · Evidencia y revisión final** — Requisito: NV-26-R05 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados (colecciones, índices y conteos), hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T05, T06.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F4 | ABIERTA |
| T06 | R05 | F4 | ABIERTA |
| T07 | R05 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05, T06, T07 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 5 |
| Tasks | 7 |
| Tasks abiertas | 7 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.0.0: T01–T07 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
