# NV-01 — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-01 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 18 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas y deben permanecer así durante la planificación.
2. Una task solo se completa después de cumplir su aceptación, producir evidencia y pasar revisión.
3. Escribir este documento no completa ninguna task.
4. Cada task declara al menos un requisito; no hay tasks sin requisito.
5. Una task no autoriza rutas, código o pruebas fuera de su alcance.
6. La dependencia entre tasks no concede autorización transitiva.
7. Cualquier cambio material exige revisar y reaprobar spec, plan y task.

## F1 — Composición y configuración

- [ ] **T01 · Composition root y registro por capas** — Requisito: NV-01-R01 · Fase: F1 · Aceptación: mapa de registros por capa/módulo, lifetimes justificados, sin service locator y con validación de dependencias al inicio. · Evidencia: diseño revisado, diff autorizado y prueba de arranque. · Depende de: ninguna.
- [ ] **T02 · Opciones tipadas y validación de configuración** — Requisito: NV-01-R02 · Fase: F1 · Aceptación: opciones enlazadas a tipos, validación al inicio, fallo rápido y placeholders sin secretos. · Evidencia: inventario de opciones, matriz de entornos y prueba con configuración inválida. · Depende de: T01.

## F2 — Errores, validación y excepciones

- [ ] **T03 · Catálogo y fábrica de Problem Details** — Requisito: NV-01-R03 · Fase: F2 · Aceptación: errores esperados con `application/problem+json`, campos estables, código coherente y extensión de correlación, sin fugas internas. · Evidencia: catálogo, pruebas de integración y revisión de respuestas. · Depende de: T01.
- [ ] **T04 · Base de validación de entrada** — Requisito: NV-01-R04 · Fase: F2 · Aceptación: reglas en el límite HTTP, 400 consistente, límites de tamaño y ausencia de lógica de negocio en controllers. · Evidencia: pruebas unitarias de validadores y pruebas negativas. · Depende de: T03.
- [ ] **T12 · Manejo global de excepciones, timeout y cancelación** — Requisito: NV-01-R12 · Fase: F2 · Aceptación: clasificación única de errores conocidos/desconocidos, timeout y cancelación definidos, logs con correlación y respuesta sin detalles internos. · Evidencia: matriz de excepciones y pruebas de integración. · Depende de: T03, T04.

## F3 — Contrato y evolución

- [ ] **T05 · Estrategia de versionado y versión inicial** — Requisito: NV-01-R05 · Fase: F3 · Aceptación: decisión registrada y justificada, versión inicial identificable, política de cambios incompatibles y deprecación. · Evidencia: registro de decisión y revisión de rutas/contrato. · Depende de: T01.
- [ ] **T06 · Documento OpenAPI verificable** — Requisito: NV-01-R06 · Fase: F3 · Aceptación: operaciones, esquemas, errores, seguridad y ejemplos coherentes, sin datos sensibles, con generación reproducible. · Evidencia: OpenAPI generado, lint/comparación y revisión de ejemplos. · Depende de: T05.

## F4 — Operación y protección

- [ ] **T07 · Health checks de liveness y readiness** — Requisito: NV-01-R07 · Fase: F4 · Aceptación: liveness independiente de dependencias, readiness con dependencias necesarias, timeouts y códigos coherentes, sin credenciales. · Evidencia: pruebas de integración y revisión de configuración. · Depende de: T01, T02.
- [ ] **T08 · Correlation ID validado y propagado** — Requisito: NV-01-R08 · Fase: F4 · Aceptación: aceptación/generación con formato y longitud limitados, propagación a logs/trazas, eco en respuesta y tratamiento como entrada no confiable. · Evidencia: pruebas de integración con valores válidos e inválidos. · Depende de: T01.
- [ ] **T09 · Política de compresión de respuestas** — Requisito: NV-01-R09 · Fase: F4 · Aceptación: negociación `Accept-Encoding`, algoritmos y tipos permitidos, umbral, exclusión de respuestas sensibles y compatibilidad HTTPS. · Evidencia: matriz de compresión y pruebas de integración. · Depende de: T01.
- [ ] **T10 · Rate limiting local con semántica 429** — Requisito: NV-01-R10 · Fase: F4 · Aceptación: políticas por endpoint/identidad/IP, 429 con `Retry-After`, health checks excluidos y alcance local documentado. · Evidencia: pruebas de límites y revisión de cabeceras. · Depende de: T01, T08.

## F5 — Salida y límites modulares

- [ ] **T11 · Contrato de paginación acotada** — Requisito: NV-01-R11 · Fase: F5 · Aceptación: estrategia declarada, orden estable, máximo, metadatos y criterio de continuación; sin respuestas ilimitadas. · Evidencia: contrato revisado y pruebas de límites. · Depende de: T04.
- [ ] **T13 · Reglas y pruebas de fronteras modulares** — Requisito: NV-01-R13 · Fase: F5 · Aceptación: contratos públicos, internals protegidos, ausencia de ciclos y reglas comprobables; sin módulos funcionales nuevos. · Evidencia: pruebas de arquitectura y documento de fronteras. · Depende de: T01.

## F6 — Verificación y evidencia

- [ ] **T14 · Pruebas de contrato de errores, versión y OpenAPI** — Requisitos: NV-01-R03, NV-01-R05, NV-01-R06 · Fase: F6 · Aceptación: Problem Details, versión y OpenAPI coinciden con el contrato revisado. · Evidencia: reporte de pruebas y diff de contrato. · Depende de: T03, T05, T06.
- [ ] **T15 · Pruebas de integración de operación y protección** — Requisitos: NV-01-R07, NV-01-R08, NV-01-R09, NV-01-R10 · Fase: F6 · Aceptación: salud, correlación, compresión y rate limiting se comportan según lo especificado. · Evidencia: reporte de integración y respuestas capturadas. · Depende de: T07, T08, T09, T10.
- [ ] **T16 · Pruebas negativas de validación y paginación** — Requisitos: NV-01-R04, NV-01-R11 · Fase: F6 · Aceptación: entradas inválidas, tamaños excesivos y páginas fuera de límite producen errores controlados. · Evidencia: casos negativos y reporte. · Depende de: T04, T11.
- [ ] **T17 · Revisión de seguridad de configuración, errores, cabeceras y límites** — Requisitos: NV-01-R02, NV-01-R03, NV-01-R08, NV-01-R10, NV-01-R12 · Fase: F6 · Aceptación: no hay secretos, fugas internas, confianza indebida en cabeceras ni límites eludibles trivialmente. · Evidencia: informe de revisión con hallazgos. · Depende de: T02, T03, T08, T10, T12.
- [ ] **T18 · Paquete de evidencia y revisión final** — Requisitos: NV-01-R01, NV-01-R02, NV-01-R03, NV-01-R04, NV-01-R05, NV-01-R06, NV-01-R07, NV-01-R08, NV-01-R09, NV-01-R10, NV-01-R11, NV-01-R12, NV-01-R13 · Fase: F6 · Aceptación: cada requisito tiene evidencia o brecha declarada y la revisión emite PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: matriz de cobertura y acta. · Depende de: T01 a T17.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F3 | ABIERTA |
| T06 | R06 | F3 | ABIERTA |
| T07 | R07 | F4 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R09 | F4 | ABIERTA |
| T10 | R10 | F4 | ABIERTA |
| T11 | R11 | F5 | ABIERTA |
| T12 | R12 | F2 | ABIERTA |
| T13 | R13 | F5 | ABIERTA |
| T14 | R03, R05, R06 | F6 | ABIERTA |
| T15 | R07, R08, R09, R10 | F6 | ABIERTA |
| T16 | R04, R11 | F6 | ABIERTA |
| T17 | R02, R03, R08, R10, R12 | F6 | ABIERTA |
| T18 | R01, R02, R03, R04, R05, R06, R07, R08, R09, R10, R11, R12, R13 | F6 | ABIERTA |

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
| R13 | T13, T18 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Tasks | 18 |
| Tasks abiertas | 18 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado | **⛔ NO EJECUTAR** |

Ninguna task puede ejecutarse hasta que exista aprobación humana de `spec.md`, `plan.md` y `task.md`, con versiones, IDs, rutas y restricciones explícitas.
