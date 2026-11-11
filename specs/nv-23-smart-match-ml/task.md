# NV-23 — Smart Match — compatibilidad con el modelo de TheHouse-ML — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-23-smart-match-ml/`; el paquete `Microsoft.ML.OnnxRuntime` de la T06 requiere una autorización que lo nombre, y la T06 y la T07 esperan el release de `TheHouse-ML`) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |
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

- [ ] **T01 · Perfil y traducción de catálogos** — Requisito: NV-23-R01 · Fase: F1 · Aceptación: El perfil se arma desde NV-11 o desde el cuerpo para invitados con las mismas validaciones; la traducción NV-11 → NV-12 de la §9 tiene una prueba por valor. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Modelo base explicable** — Requisito: NV-23-R02 · Fase: F1 · Aceptación: `baseline-1.0.0` con los pesos de la §9: desglose en puntos que suma el total, restricciones duras, mascotas neutral; determinista; casos de prueba propios con resultados esperados. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Puntajes por cuarto** — Requisito: NV-23-R03 · Fase: F1 · Aceptación: `scores` con hasta 50 cuartos, con token o como invitado; ids no publicados como `unavailable` sin revelar existencia; 422 con perfil incompleto. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Recomendaciones ordenadas** — Requisito: NV-23-R04 · Fase: F2 · Aceptación: Lista paginada sobre los candidatos de NV-14, orden `score` desc y id, descartes al final, no elegibles fuera; con y sin cuenta. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · Retroalimentación y descartes** — Requisito: NV-23-R05 · Fase: F2 · Aceptación: Feedback con `modelVersion` y reemplazo de la opinión anterior; descartes con TTL de 30 días y opción de deshacer; idempotentes. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Carga del modelo de TheHouse-ML** — Requisito: NV-23-R06 · Fase: F2 · Aceptación: `OnnxCompatibilityModel` carga el paquete de entrega, verifica SHA-256 y esquema y hace la prueba de arranque; cualquier falla usa el modelo base y lo registra; desactivado por configuración. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Paridad con TheHouse-ML** — Requisito: NV-23-R07 · Fase: F3 · Aceptación: Los casos dorados de ML-04 dan el mismo desglose en `baseline-1.0.0` y los de ML-06 el mismo puntaje en ONNX dentro de la tolerancia publicada. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Rendimiento y límites** — Requisito: NV-23-R08 · Fase: F3 · Aceptación: p95 < 300 ms para 50 cuartos (RNF-14) y < 500 ms para recomendaciones con 500 candidatos en Azure; rate limits con 429; Redis caído 503. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T09 · Minimización y privacidad** — Requisito: NV-23-R09 · Fase: F3 · Aceptación: Prueba de que el cálculo no lee campos personales ni ubicación real; el desglose no expone pesos internos del modelo entrenado. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T10 · Contrato para Mobile y despliegue** — Requisito: NV-23-R10 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (scores con y sin cuenta, recomendaciones, feedback y descartes). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- [ ] **T11 · Pruebas unitarias y de integración** — Requisito: NV-23-R11 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T12 · Pruebas de seguridad** — Requisito: NV-23-R11 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T13 · Evidencia y revisión final** — Requisito: NV-23-R11 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T11, T12.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F1 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
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
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
