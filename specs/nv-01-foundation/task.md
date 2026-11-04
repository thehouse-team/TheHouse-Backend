# NV-01 — Task list U2

| Campo | Valor |
| --- | --- |
| ID | NV-01 |
| Documento | `task.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución U2 | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** (T13 excluida) |
| Commit validado | `024d5f7` — GitHub Actions / Ubuntu, `U2 Linux Validation` |
| Evidencia de cierre | `evidence.md` — revisión documental 2026-11-03 |
| Spec | `spec.md` 1.1.0-u2 |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks totales | 18 |
| Tasks U2 | 17 |
| Tasks futuras (no U2) | 1 |
| Tasks completadas | 17 |

## Reglas de esta lista

1. La preparación documental original dejó todas las tasks abiertas; el cierre posterior solo marca aceptación cumplida con evidencia y revisión, no planificación.
2. Una task solo se completa después de cumplir su aceptación, producir evidencia y pasar revisión.
3. Solo se ejecutan las tasks marcadas **Alcance: U2** y autorizadas en Fase B.
4. Cada task declara al menos un requisito; no hay tasks sin requisito.
5. Una task no autoriza rutas, código o pruebas fuera de su alcance.
6. La dependencia entre tasks no concede autorización transitiva.
7. La aprobación documental no autoriza ejecución conforme a M-BE-027.

## F1 — Composición y configuración

- [x] **T01 · Composition root y registro por capas** — Requisito: NV-01-R01 · Fase: F1 · **Alcance: U2** · Aceptación: mapa de registros por capa/módulo, lifetimes justificados, sin service locator, validación de dependencias al inicio y boundaries de MongoDB/Redis/JWT sin repositorios ni conexiones. · Evidencia: diseño revisado, diff autorizado y prueba de arranque. · Depende de: ninguna.
- [x] **T02 · Opciones tipadas y validación de configuración** — Requisito: NV-01-R02 · Fase: F1 · **Alcance: U2** · Aceptación: opciones enlazadas a tipos, validación al inicio, fallo rápido, placeholders sin secretos y grupos para MongoDB, Redis y JWT sin abrir conexiones. · Evidencia: inventario de opciones, matriz de entornos y prueba con configuración inválida. · Depende de: T01.

## F2 — Errores, validación y excepciones

- [x] **T03 · Catálogo y fábrica de Problem Details** — Requisito: NV-01-R03 · Fase: F2 · **Alcance: U2** · Aceptación: errores esperados con `application/problem+json`, campos estables, código coherente y extensión de correlación, sin fugas internas. · Evidencia: catálogo, pruebas de integración y revisión de respuestas. · Depende de: T01.
- [x] **T04 · Base de validación de entrada** — Requisito: NV-01-R04 · Fase: F2 · **Alcance: U2** · Aceptación: reglas en el límite HTTP, 400 consistente, límites de tamaño y ausencia de lógica de negocio en controllers. · Evidencia: pruebas unitarias de validadores y pruebas negativas. · Depende de: T03.
- [x] **T12 · Manejo global de excepciones, timeout y cancelación** — Requisito: NV-01-R12 · Fase: F2 · **Alcance: U2** · Aceptación: clasificación única de errores conocidos/desconocidos, timeout y cancelación definidos, logs con correlación y respuesta sin detalles internos. · Evidencia: matriz de excepciones y pruebas de integración. · Depende de: T03, T04.

## F3 — Contrato y evolución

- [x] **T05 · Estrategia de versionado y versión inicial** — Requisito: NV-01-R05 · Fase: F3 · **Alcance: U2** · Aceptación: decisión registrada y justificada, versión `/api/v1` identificable, política de cambios incompatibles y deprecación. · Evidencia: registro de decisión y revisión de rutas/contrato. · Depende de: T01.
- [x] **T06 · Documento OpenAPI verificable** — Requisito: NV-01-R06 · Fase: F3 · **Alcance: U2** · Aceptación: operaciones, esquemas, errores, seguridad y ejemplos coherentes, sin datos sensibles, con generación reproducible. · Evidencia: OpenAPI generado, lint/comparación y revisión de ejemplos. · Depende de: T05.

## F4 — Operación y protección

- [x] **T07 · Health checks de liveness y readiness** — Requisito: NV-01-R07 · Fase: F4 · **Alcance: U2** · Aceptación: liveness independiente de dependencias, readiness con dependencias necesarias y boundaries de MongoDB/Redis, timeouts y códigos coherentes, sin credenciales. · Evidencia: pruebas de integración y revisión de configuración. · Depende de: T01, T02.
- [x] **T08 · Correlation ID validado y propagado** — Requisito: NV-01-R08 · Fase: F4 · **Alcance: U2** · Aceptación: aceptación/generación con formato y longitud limitados, propagación a logs/trazas, eco en respuesta y tratamiento como entrada no confiable. · Evidencia: pruebas de integración con valores válidos e inválidos. · Depende de: T01.
- [x] **T09 · Política de compresión de respuestas** — Requisito: NV-01-R09 · Fase: F4 · **Alcance: U2** · Aceptación: negociación `Accept-Encoding`, algoritmos y tipos permitidos, umbral, exclusión de respuestas sensibles y compatibilidad HTTPS. · Evidencia: matriz de compresión y pruebas de integración. · Depende de: T01.
- [x] **T10 · Rate limiting foundation con semántica 429** — Requisito: NV-01-R10 · Fase: F4 · **Alcance: U2** · Aceptación: políticas por endpoint/identidad/IP, 429 con `Retry-After`, health checks excluidos y alcance local documentado; la coordinación distribuida se difiere a NV-03/NV-05. · Evidencia: pruebas de límites y revisión de cabeceras. · Depende de: T01, T08.

## F5 — Salida y límites modulares

- [x] **T11 · Contrato de paginación acotada** — Requisito: NV-01-R11 · Fase: F5 · **Alcance: U2** · Aceptación: estrategia declarada, orden estable, máximo 50, metadatos y criterio de continuación; sin respuestas ilimitadas y coherente con NV-07/NV-08. · Evidencia: contrato revisado y pruebas de límites. · Depende de: T04.
- [ ] **T13 · Reglas y pruebas de fronteras modulares** — Requisito: NV-01-R13 · Fase: F5 · **Alcance: Futuro (no U2)** · Aceptación: contratos públicos, internals protegidos, ausencia de ciclos y reglas comprobables; sin módulos funcionales nuevos. · Evidencia: pruebas de arquitectura y documento de fronteras. · Depende de: T01.

## F6 — Verificación y evidencia

- [x] **T14 · Pruebas de contrato de errores, versión y OpenAPI** — Requisitos: NV-01-R03, NV-01-R05, NV-01-R06 · Fase: F6 · **Alcance: U2** · Aceptación: Problem Details, versión y OpenAPI coinciden con el contrato revisado. · Evidencia: reporte de pruebas y diff de contrato. · Depende de: T03, T05, T06.
- [x] **T15 · Pruebas de integración de operación y protección** — Requisitos: NV-01-R07, NV-01-R08, NV-01-R09, NV-01-R10 · Fase: F6 · **Alcance: U2** · Aceptación: salud, correlación, compresión y rate limiting se comportan según lo especificado. · Evidencia: reporte de integración y respuestas capturadas. · Depende de: T07, T08, T09, T10.
- [x] **T16 · Pruebas negativas de validación y paginación** — Requisitos: NV-01-R04, NV-01-R11 · Fase: F6 · **Alcance: U2** · Aceptación: entradas inválidas, tamaños excesivos y páginas fuera de límite producen errores controlados. · Evidencia: casos negativos y reporte. · Depende de: T04, T11.
- [x] **T17 · Revisión de seguridad de configuración, errores, cabeceras y límites** — Requisitos: NV-01-R02, NV-01-R03, NV-01-R08, NV-01-R10, NV-01-R12 · Fase: F6 · **Alcance: U2** · Aceptación: no hay secretos, fugas internas, confianza indebida en cabeceras ni límites eludibles trivialmente. · Evidencia: informe de revisión con hallazgos. · Depende de: T02, T03, T08, T10, T12.
- [x] **T18 · Paquete de evidencia y revisión final** — Requisitos: NV-01-R01, NV-01-R02, NV-01-R03, NV-01-R04, NV-01-R05, NV-01-R06, NV-01-R07, NV-01-R08, NV-01-R09, NV-01-R10, NV-01-R11, NV-01-R12 · Fase: F6 · **Alcance: U2** · Aceptación: cada requisito U2 tiene evidencia o brecha declarada y la revisión emite PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: matriz de cobertura y acta. · Depende de: T01 a T12 y T14 a T17.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01 | F1 | U2 | COMPLETADA |
| T02 | R02 | F1 | U2 | COMPLETADA |
| T03 | R03 | F2 | U2 | COMPLETADA |
| T04 | R04 | F2 | U2 | COMPLETADA |
| T05 | R05 | F3 | U2 | COMPLETADA |
| T06 | R06 | F3 | U2 | COMPLETADA |
| T07 | R07 | F4 | U2 | COMPLETADA |
| T08 | R08 | F4 | U2 | COMPLETADA |
| T09 | R09 | F4 | U2 | COMPLETADA |
| T10 | R10 | F4 | U2 | COMPLETADA |
| T11 | R11 | F5 | U2 | COMPLETADA |
| T12 | R12 | F2 | U2 | COMPLETADA |
| T13 | R13 | F5 | Futuro | ABIERTA |
| T14 | R03, R05, R06 | F6 | U2 | COMPLETADA |
| T15 | R07, R08, R09, R10 | F6 | U2 | COMPLETADA |
| T16 | R04, R11 | F6 | U2 | COMPLETADA |
| T17 | R02, R03, R08, R10, R12 | F6 | U2 | COMPLETADA |
| T18 | R01, R02, R03, R04, R05, R06, R07, R08, R09, R10, R11, R12 | F6 | U2 | COMPLETADA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T18 |
| R02 | T02, T17, T18 |
| R03 | T03, T14, T17, T18 |
| R04 | T04, T16, T18 |
| R05 | T05, T14, T18 |
| R06 | T06, T14, T18 |
| R07 | T07, T15, T18 |
| R08 | T08, T15, T17, T18 |
| R09 | T09, T15, T18 |
| R10 | T10, T15, T17, T18 |
| R11 | T11, T16, T18 |
| R12 | T12, T17, T18 |
| R13 | T13 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Tasks | 18 |
| Tasks U2 | 17 |
| Tasks futuras (no U2) | 1 |
| Tasks abiertas | 1 (T13, futura; 0 pendientes U2) |
| Tasks completadas | 17 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución U2 | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** (T13 excluida) |

La aprobación documental original no completó ninguna task. El cierre registra la ejecución de Fase B previamente autorizada y la validación CI de `024d5f7`, con revisión documental de aceptación y seguridad en `evidence.md`; no autoriza ejecutar T13 ni otras specs.

## Seguimiento histórico de Fase B — 2026-11-03, previo al cierre CI

La instrucción humana de la sesión de implementación autorizó NV-01 `1.1.0-u2`, T01–T12 y T14–T18, exclusivamente Foundation y sus pruebas/evidencia. El registro posterior y las rutas afectadas están en `evidence.md`; no constituye autorización de otra spec. T13 no se ejecutó.

El intento local quedó BLOCKED por Windows Application Control (`0x800711C7`), no por un defecto funcional demostrado de NV-01. En ese momento ninguna task se cerró ni se hizo commit/push. Ese bloqueo de evidencia integrada quedó superado para el cierre U2 mediante la validación posterior en GitHub Actions / Ubuntu; no se alteró la política de seguridad Windows.

## Cierre documental con CI — 2026-11-03

El usuario confirmó `024d5f7`, workflow `U2 Linux Validation`, GitHub Actions / Ubuntu: Restore PASS, Build PASS, Run tests PASS, workflow SUCCESS y artifact `u2-test-results`. Se usa esa confirmación como fuente del resultado remoto, junto con la revisión en lectura del commit, workflow, implementación y suites existentes; no se afirma una descarga independiente del artifact ni se inventan métricas o run IDs.

Se verificó la aceptación de T01–T12 y T14–T18 contra R01–R12, fases y evidencia; se cierran esas 17 tasks. SDD y seguridad: **PASS WITH WARNINGS**, sin hallazgos bloqueantes dentro de Foundation (detalle, responsables y acciones en `evidence.md`). Configuración inválida y compression negotiation: resueltas. Forwarded headers: configuración segura; solo la validación del ingress real de Azure queda en NV-03, fuera del cierre NV-01.

T13 permanece **[ ] ABIERTA, NO EJECUTADA**, fuera de U2. NV-02 permanece **⛔ NO EJECUTAR**. Sin cambios de código, `.csproj`, workflow o `global.json`; sin nuevas ejecuciones, commit/push ni operaciones sobre stashes. No se avanza a NV-04.

**NV-01 Foundation 1.1.0-u2: ✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**
