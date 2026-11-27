# NV-20 — Notificaciones — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `task.md` |
| Versión | 1.3.0 |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-18 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T08; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-20-notifications/`; sin comandos en Production); 1.3.0: Victor Manuel Jose Jose — 2026-11-27 («Apruebo y EJECUTA» de T09–T12, después de la validación del §6.1 por el responsable de Mobile; mismas rutas, sin paquetes, colecciones ni índices nuevos) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: 7 de 8 tasks (2026-11-19), T05 espera la prueba del push en un teléfono real con Mobile; 1.3.0: T09–T11 implementadas (2026-11-27), T12 espera el despliegue y la prueba en Azure; 10 de 12 tasks** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.3.0 |
| Plan | `plan.md` 1.3.0 |
| Tasks totales | 12 |
| Tasks completadas | 10 |

**Cambio 1.3.0 (2026-11-26) — APROBADO el 2026-11-27: Mobile validó el §6.1 (destinos `room` y `none`, tipo o destino desconocido → bandeja, mismo resolver para bandeja y push) y Victor dio «Apruebo y EJECUTA» para T09–T12:** la revisión de los servicios encontró hechos importantes que hoy no avisan a nadie: el estudiante no se entera de que tiene un convenio por aceptar ni de que se canceló, y el arrendador no se entera de que recibió una reseña. Esta versión agrega **6 tipos** a la bandeja (y por lo tanto al push, regla 1): `agreement_proposed`, `agreement_cancelled`, `review_received`, `review_hidden`, `favorite_unavailable` y `report_resolved` (§9.1), con dos destinos nuevos para Mobile (`room` y `none`). Sin endpoints nuevos, sin colecciones nuevas, sin paquetes y sin costo. Los recordatorios de visita quedan fuera: requieren un proceso programado que corra aunque la API esté en cero réplicas (Container Apps Job y Terraform).

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [x] **T01 · Registro de dispositivos** — Requisito: NV-20-R01 · Fase: F1 · Aceptación: PUT idempotente por instalación, máximo 5, un token por cuenta, DELETE y lista sin tokens; limpieza al logout, suspensión y eliminación. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [x] **T02 · Envío push de la bandeja** — Requisito: NV-20-R02 · Fase: F2 · Aceptación: Los 16 tipos se envían a los dispositivos del destinatario con título, cuerpo y datos de destino; nunca al actor; agrupados por conversación. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01.
- [x] **T03 · Cliente de FCM** — Requisito: NV-20-R03 · Fase: F2 · Aceptación: OAuth 2.0 con la cuenta de servicio, renovación del token de acceso, API HTTP v1, borrado de tokens inválidos y un reintento; un fallo nunca rompe la acción principal. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01.

## F3 — Seguridad, autorización y límites

- [x] **T04 · Privacidad, seguridad y límites** — Requisito: NV-20-R04 · Fase: F3 · Aceptación: Sin contenido privado en push, sin tokens en logs ni respuestas, BOLA, rate limit, secreto solo en la Container App. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03.

## F4 — Pruebas y evidencia

- [ ] **T05 · Contrato para Mobile y despliegue** — Requisito: NV-20-R05 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; secreto cargado por Victor; desplegado por el CD; prueba real en Azure con un teléfono: solicitud, mensaje y visita llegan como push. · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04.
- [x] **T06 · Pruebas unitarias y de integración** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05.
- [x] **T07 · Pruebas de seguridad** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05.
- [x] **T08 · Evidencia y revisión final** — Requisito: NV-20-R06 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T06, T07.

## F5 — Avisos nuevos (1.3.0)

- [x] **T09 · Avisos de convenio** — Requisito: NV-20-R07 · Fase: F5 · Aceptación: `agreement_proposed` y `agreement_cancelled` en el catálogo, con sus textos y destino `rental`; se publican desde `AgreementService` al crear y al cancelar; nunca al arrendador; una vez por convenio; pruebas de integración. · Evidencia: pruebas. · Depende de: T02.
- [x] **T10 · Avisos de reseñas** — Requisito: NV-20-R08 · Fase: F5 · Aceptación: `review_received` al dueño del cuarto al publicar una reseña y `review_hidden` al autor al ocultarla; sin calificación, texto ni motivo; destino `room`; pruebas de integración y de privacidad. · Evidencia: pruebas. · Depende de: T02.
- [x] **T11 · Avisos de favoritos y reportes** — Requisito: NV-20-R09 · Fase: F5 · Aceptación: `favorite_unavailable` a quienes tienen el cuarto en favoritos cuando se renta, pausa, elimina o se suspende su edificio (tope de 200, una vez por cuarto, usuario y día, nunca al arrendador); `report_resolved` a quien reportó sin la sanción; pruebas de integración y de las reglas de arquitectura. · Evidencia: pruebas. · Depende de: T02.
- [ ] **T12 · Contrato 1.3.0 para Mobile y despliegue** — Requisito: NV-20-R10 · Fase: F5 · Aceptación: §6.1 validado por Mobile (destinos `room` y `none`, tipos desconocidos → bandeja); desplegado por el CD; prueba real en Azure de los 6 avisos con cuentas de prueba; evidencia y revisión final. · Evidencia: `evidence.md`. · Depende de: T09, T10, T11.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | COMPLETA |
| T02 | R02 | F2 | COMPLETA |
| T03 | R03 | F2 | COMPLETA |
| T04 | R04 | F3 | COMPLETA |
| T05 | R05 | F4 | ABIERTA |
| T06 | R06 | F4 | COMPLETA |
| T07 | R06 | F4 | COMPLETA |
| T08 | R06 | F4 | COMPLETA |
| T09 | R07 | F5 | COMPLETA |
| T10 | R08 | F5 | COMPLETA |
| T11 | R09 | F5 | COMPLETA |
| T12 | R10 | F5 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06, T07, T08 |
| R07 | T09 |
| R08 | T10 |
| R09 | T11 |
| R10 | T12 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 10 |
| Tasks | 12 |
| Tasks abiertas | 2 |
| Tasks completadas | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: 7 de 8 tasks (2026-11-19), T05 espera la prueba del push en un teléfono real con Mobile; 1.3.0: T09–T11 implementadas (2026-11-27), T12 espera el despliegue y la prueba en Azure; 10 de 12 tasks** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
