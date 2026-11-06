# NV-24 — Smart Match — explicaciones con IA generativa — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 8 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Explicación fundamentada** — Requisito: NV-24-R01 · Fase: F1 · Aceptación: Resumen, ventajas y desventajas derivados solo de los hechos de entrada; casos de prueba con hechos fijos. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Validación anti-alucinación** — Requisito: NV-24-R02 · Fase: F1 · Aceptación: Un validador detecta números, servicios o condiciones ausentes de los hechos y cambia a plantilla; pruebas con salidas manipuladas. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Respaldo por plantilla** — Requisito: NV-24-R03 · Fase: F2 · Aceptación: Con el proveedor caído o lento (> 3 s) responde la plantilla con `generatedBy=template`; el puntaje sigue disponible. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Caché y costo** — Requisito: NV-24-R04 · Fase: F2 · Aceptación: Caché por cuarto y versiones con TTL de 24 h; límites que mantienen el uso dentro del plan gratuito. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [ ] **T05 · Protección contra inyección** — Requisito: NV-24-R05 · Fase: F3 · Aceptación: Pruebas con descripciones maliciosas que no llegan al prompt ni alteran la salida. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04.

## F4 — Pruebas y evidencia

- [ ] **T06 · Pruebas unitarias y de integración** — Requisito: NV-24-R06 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05.
- [ ] **T07 · Pruebas de seguridad** — Requisito: NV-24-R06 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05.
- [ ] **T08 · Evidencia y revisión final** — Requisito: NV-24-R06 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T06, T07.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F3 | ABIERTA |
| T06 | R06 | F4 | ABIERTA |
| T07 | R06 | F4 | ABIERTA |
| T08 | R06 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06, T07, T08 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 6 |
| Tasks | 8 |
| Tasks abiertas | 8 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
