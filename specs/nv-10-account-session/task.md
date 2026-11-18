# NV-10 — Sesión: refresh token y cuenta propia — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `task.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T09; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-10-account-session/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T05 completas; T06–T09 esperan CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks totales | 9 |
| Tasks completadas | 5 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Vista previa y bloqueos** — Requisito: NV-10-R01 · Fase: F1 · Aceptación: La vista previa cuenta lo que se borra o cancela y lista los bloqueos (`active_rental`, `staff_account`); coincide con lo que hace la eliminación. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T03 · Sesión siempre viva** — Requisito: NV-10-R03 · Fase: F1 · Aceptación: Sin tope absoluto: una sesión usada cada menos de 30 días no vence; 30 días sin uso, logout, reuso, suspensión o eliminación la cierran; tokens existentes sin migración. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [x] **T04 · Aviso de privacidad y términos** — Requisito: NV-10-R04 · Fase: F1 · Aceptación: `GET /legal` con versiones y URLs; aceptación registrada; `/users/me` con `legal` y `legalUpToDate`; versión distinta → 409. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T02 · Eliminación de cuenta** — Requisito: NV-10-R02 · Fase: F2 · Aceptación: Contraseña y `ELIMINAR`; cada dato de la tabla de reglas queda borrado, cancelado, retirado o anonimizado (una prueba por módulo); pasos idempotentes; un fallo deja la sesión viva y repetir termina; al final 401 en toda ruta, refresh y login. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T03, T04.

## F3 — Seguridad, autorización y límites

- [x] **T05 · Seguridad, privacidad y límites** — Requisito: NV-10-R05 · Fase: F3 · Aceptación: Reautenticación, rate limit, sin PII en logs, archivos borrados del almacenamiento, otra cuenta intacta, Redis caído 503. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T03, T04, T02.

## F4 — Pruebas y evidencia

- [ ] **T06 · Contrato para Mobile y despliegue** — Requisito: NV-10-R06 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure: cuenta de prueba con perfil, favorito, solicitud y visita, eliminada, y la contraparte ve «Usuario eliminado». · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T03, T04, T02, T05.
- [ ] **T07 · Pruebas unitarias y de integración** — Requisito: NV-10-R07 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas de seguridad** — Requisito: NV-10-R07 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T09 · Evidencia y revisión final** — Requisito: NV-10-R07 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T07, T08.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F2 | COMPLETA |
| T03 | R03 | F1 | COMPLETA |
| T04 | R04 | F1 | COMPLETA |
| T05 | R05 | F3 | COMPLETA |
| T06 | R06 | F4 | ABIERTA |
| T07 | R07 | F4 | ABIERTA |
| T08 | R07 | F4 | ABIERTA |
| T09 | R07 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06 |
| R07 | T07, T08, T09 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Tasks | 9 |
| Tasks abiertas | 4 |
| Tasks completadas | 5 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T05 completas; T06–T09 esperan CI, despliegue y Azure** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
