# NV-23 — Smart Match — compatibilidad con el modelo de TheHouse-ML — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `task.md` |
| Versión | 1.2.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA — 1.2.0** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo NV-23 1.2.0»: decisiones de la §13; Mobile aceptó el cambio de contrato §6.1; la autorización «EJECUTA» de M-BE-068 pasa a T01–T14 con las mismas rutas: `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas, `scripts/db/` (regenerado por la prueba de NV-26) y `specs/nv-23-smart-match-ml/`; la T06 requiere además autorización que nombre `Microsoft.ML.OnnxRuntime`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0 |
| Plan | `plan.md` 1.2.0 |
| Tasks totales | 14 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Perfil y traducción al esquema de ML** — Requisito: NV-23-R01 · Fase: F1 · Aceptación: El perfil se arma desde NV-11 o desde el cuerpo para invitados con las mismas validaciones, el perfil mínimo y los valores por omisión del esquema 2.0.0; la traducción de NV-12 de la §9 tiene una prueba por valor. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Modelo base alineado con ML-04** — Requisito: NV-23-R02 · Fase: F1 · Aceptación: Las 10 features y `baseline-1.0.0` con nombres, curvas, orden y redondeo de ML-04; HC01–HC03 con todos los motivos; desglose de 6 factores; determinista; casos propios con resultados calculados a mano, incluidos los límites de redondeo (por ejemplo 13.35 → 13.4). · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T03 · Puntajes por cuarto** — Requisito: NV-23-R03 · Fase: F1 · Aceptación: `scores` con hasta 50 cuartos, con token o como invitado; ids no publicados como `unavailable` sin revelar existencia; 422 con `missing`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T04 · Recomendaciones ordenadas** — Requisito: NV-23-R04 · Fase: F2 · Aceptación: Lista paginada sobre hasta 500 candidatos de NV-14, no descartados primero, puntaje sin redondear desc y `roomId`; no elegibles fuera; con y sin cuenta. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T05 · Retroalimentación y descartes** — Requisito: NV-23-R05 · Fase: F2 · Aceptación: Feedback con `modelVersion` y reemplazo de la opinión anterior; descartes con TTL de 30 días y opción de deshacer; idempotentes; borrados al eliminar la cuenta (NV-10); colecciones en el script de NV-26. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.
- [ ] **T06 · Carga del modelo de TheHouse-ML** — Requisito: NV-23-R06 · Fase: F2 · Aceptación: `OnnxCompatibilityModel` carga el paquete de entrega, verifica SHA-256 y esquema y hace la prueba de arranque; cualquier falla usa el modelo base y lo registra; desactivado por configuración. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Paridad con TheHouse-ML** — Requisito: NV-23-R07 · Fase: F3 · Aceptación: Los casos dorados de ML-04 (T05) dan el mismo vector de features, el mismo desglose y el mismo `matchScore` en `baseline-1.0.0`, y los de ML-06 el mismo puntaje en ONNX dentro de la tolerancia publicada. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Rendimiento y límites** — Requisito: NV-23-R08 · Fase: F3 · Aceptación: p95 < 300 ms para 50 cuartos (RNF-14) y < 500 ms para recomendaciones con 500 candidatos en Azure; rate limits con 429; Redis caído 503. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T09 · Minimización y privacidad** — Requisito: NV-23-R09 · Fase: F3 · Aceptación: Prueba de que el cálculo no lee campos personales ni ubicación real; el desglose no expone valores de features ni parámetros de un modelo entrenado; las métricas no guardan ids. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T10 · Métricas de Smart Match (RF-66)** — Requisito: NV-23-R10 · Fase: F3 · Aceptación: `GET /admin/smart-match/metrics` con rango de fechas (máximo 90 días), totales, promedio y rangos de puntaje por `modelVersion`, retroalimentación y descartes activos; solo rol `admin`; contadores diarios sin ids; una falla al contar no afecta la respuesta al usuario. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T11 · Contrato para Mobile y despliegue** — Requisito: NV-23-R11 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (scores con y sin cuenta, recomendaciones, feedback, descartes y métricas). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [ ] **T12 · Pruebas unitarias y de integración** — Requisito: NV-23-R12 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- [ ] **T13 · Pruebas de seguridad** — Requisito: NV-23-R12 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- [ ] **T14 · Evidencia y revisión final** — Requisito: NV-23-R12 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T12, T13.

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
| T10 | R10 | F3 | ABIERTA |
| T11 | R11 | F4 | ABIERTA |
| T12 | R12 | F4 | ABIERTA |
| T13 | R12 | F4 | ABIERTA |
| T14 | R12 | F4 | ABIERTA |

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
| R11 | T11 |
| R12 | T12, T13, T14 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 12 |
| Tasks | 14 |
| Tasks abiertas | 14 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA — 1.2.0** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
