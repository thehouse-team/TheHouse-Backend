# NV-21 — Verificación, reportes y moderación — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `task.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-19 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; `infra/terraform/` solo hasta el `plan` (el `apply` requiere «OK PARA APPLY»); sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.2.0 T01–T10 (2026-11-19): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 10 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T02 · Moderación de texto antes de guardar** — Requisito: NV-21-R02 · Fase: F1 · Aceptación: Cada campo de la lista del alcance pasa por la revisión antes de guardar; un texto inapropiado responde 422 y no se guarda; uno limpio se guarda como hoy (una prueba por módulo). · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Moderación de imágenes antes de guardar** — Requisito: NV-21-R03 · Fase: F1 · Aceptación: Foto del edificio y de los cuartos revisadas antes de guardar; documentos nunca enviados; imagen grande reducida antes de enviarla. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T01 · Cliente de Content Safety** — Requisito: NV-21-R01 · Fase: F2 · Aceptación: Llamadas de texto e imagen con identidad administrada, umbral 4, tiempo máximo y un reintento; falla cerrado con 503; probado contra un servidor falso. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T02, T03.
- [x] **T04 · Registro de rechazos para el administrador** — Requisito: NV-21-R04 · Fase: F2 · Aceptación: Rechazo registrado sin contenido; vista paginada y filtrable solo para admin o moderador; TTL de 180 días. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T02, T03.

## F3 — Seguridad, autorización y límites

- [x] **T05 · Recurso de Azure con identidad administrada** — Requisito: NV-21-R05 · Fase: F3 · Aceptación: Terraform del recurso F0, la identidad y el rol; plan revisado; apply con «OK PARA APPLY»; costo $0 verificado. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T02, T03, T01, T04.
- [x] **T06 · Seguridad, privacidad y límites** — Requisito: NV-21-R06 · Fase: F3 · Aceptación: Sin llaves ni contenido en logs; documentos excluidos; ruta admin protegida; Content Safety caído o sin cupo → 503 sin guardar. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T02, T03, T01, T04.

## F4 — Pruebas y evidencia

- [x] **T07 · Contrato para Mobile y despliegue** — Requisito: NV-21-R07 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba real en Azure: texto y foto limpios se guardan, inapropiados se rechazan con 422 y aparecen en la vista del administrador. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T02, T03, T01, T04, T05, T06.
- [x] **T08 · Pruebas unitarias y de integración** — Requisito: NV-21-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [x] **T09 · Pruebas de seguridad** — Requisito: NV-21-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [x] **T10 · Evidencia y revisión final** — Requisito: NV-21-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F2 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F2 | COMPLETA |
| T05 | R05 | F3 | COMPLETA |
| T06 | R06 | F3 | COMPLETA |
| T07 | R07 | F4 | COMPLETA |
| T08 | R08 | F4 | COMPLETA |
| T09 | R08 | F4 | COMPLETA |
| T10 | R08 | F4 | COMPLETA |

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
| Tasks abiertas | 0 |
| Tasks completadas | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — 1.2.0 T01–T10 (2026-11-19): PASS WITH WARNINGS, ver `evidence.md`** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
