# NV-21 — Verificación, reportes y moderación — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T14; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; asignar roles en Production requiere autorización aparte) |
| Estado de ejecución | **✅ COMPLETA — T01–T14 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 14 |
| Tasks completadas | 14 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Roles y asignación auditada** — Requisito: NV-21-R01 · Fase: F1 · Aceptación: `staff_roles` consultado en cada solicitud; `--grant-role`/`--revoke-role` por correo con registro en la bitácora y protección de Production; sin rol 403 `role_required`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T02 · Cola y detalle de revisión** — Requisito: NV-21-R02 · Fase: F1 · Aceptación: `GET /admin/buildings` filtra por estado y pagina; el detalle trae edificio, cuartos y arrendador sin teléfono ni correo. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Decisiones de revisión** — Requisito: NV-21-R03 · Fase: F1 · Aceptación: Aprobar, pedir correcciones y rechazar con motivo y `version`, con sus efectos en cuartos; propio 403 `own_building`; estado inválido 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T04 · Verificación para el dueño** — Requisito: NV-21-R04 · Fase: F1 · Aceptación: `GET /landlord/buildings/{id}/verification` devuelve estado y último motivo; ajeno 404. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T05 · Reportes** — Requisito: NV-21-R05 · Fase: F1 · Aceptación: Crear reportes de edificio, cuarto o cuenta con catálogo de motivos; objetivo inexistente 404; sin duplicados abiertos (409); no a uno mismo. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T06 · Resolución de reportes** — Requisito: NV-21-R06 · Fase: F2 · Aceptación: Cola por estado; resolver con `dismiss`, `suspend_building` o `suspend_user` aplica la acción y cierra el reporte. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [x] **T07 · Suspensión de edificios** — Requisito: NV-21-R07 · Fase: F2 · Aceptación: `verified` ⇄ `suspended` con motivo; otros estados 409. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [x] **T08 · Suspensión de cuentas** — Requisito: NV-21-R08 · Fase: F2 · Aceptación: Bloquea login y refresh con 403 `account_suspended`, revoca refresh tokens y suspende edificios verificados; reactivación; no a `admin`. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.
- [x] **T09 · Bitácora inmutable** — Requisito: NV-21-R09 · Fase: F2 · Aceptación: Cada acción administrativa escribe un evento; `GET /admin/audit` paginado; no existe ruta ni método para modificarla. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02, T03, T04, T05.

## F3 — Seguridad, autorización y límites

- [x] **T10 · Autorización y límites** — Requisito: NV-21-R10 · Fase: F3 · Aceptación: Pruebas con usuario sin rol, revisor, admin y dueño; rate limits con 429; Redis caído 503; `no-store`; motivos de reporte fuera de logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.

## F4 — Pruebas y evidencia

- [x] **T11 · Contrato para Mobile y despliegue** — Requisito: NV-21-R11 · Fase: F4 · Aceptación: Contrato de la §6.1 entregado a Mobile; roles asignados en Production con autorización; prueba HTTP real en Azure (aprobar y rechazar, reporte, suspensión) incluido resolver el edificio de prueba de NV-12. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- [x] **T12 · Pruebas unitarias y de integración** — Requisito: NV-21-R12 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- [x] **T13 · Pruebas de seguridad** — Requisito: NV-21-R12 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- [x] **T14 · Evidencia y revisión final** — Requisito: NV-21-R12 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T12, T13.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F1 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F1 | COMPLETA |
| T05 | R05 | F1 | COMPLETA |
| T06 | R06 | F2 | COMPLETA |
| T07 | R07 | F2 | COMPLETA |
| T08 | R08 | F2 | COMPLETA |
| T09 | R09 | F2 | COMPLETA |
| T10 | R10 | F3 | COMPLETA |
| T11 | R11 | F4 | COMPLETA |
| T12 | R12 | F4 | COMPLETA |
| T13 | R12 | F4 | COMPLETA |
| T14 | R12 | F4 | COMPLETA |

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
| Tasks abiertas | 0 |
| Tasks completadas | 14 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — T01–T14 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
