# NV-18 — Cierre de trato con QR y renta — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 11 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Convenio desde una solicitud aceptada** — Requisito: NV-18-R01 · Fase: F1 · Aceptación: El arrendador crea el convenio solo con solicitud `accepted` de su cuarto, cuarto `available` y edificio `verified`; valida montos, fechas, duración y día de pago; calcula `endDate`; 409 `request_state`, `room_unavailable` y `agreement_open`; lo ajeno 404; sin modo arrendador 403. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Código de cierre seguro** — Requisito: NV-18-R02 · Fase: F1 · Aceptación: Crear y `/code` entregan `qrToken` y `backupCode` que vencen a los 10 minutos; solo se guarda su hash; un código nuevo invalida el anterior; el código se acepta sin guiones ni distinción de mayúsculas. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Revisar y aceptar** — Requisito: NV-18-R03 · Fase: F2 · Aceptación: `preview` devuelve los términos y las reglas sin consumir el código; `accept` exige `acceptedTerms: true`, activa el convenio, consume el código y marca el cuarto `rented`; 410 vencido o usado; 404 de otra cuenta; prueba de concurrencia con dos aceptaciones y con un cambio del cuarto a mitad. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Efectos al rentar** — Requisito: NV-18-R04 · Fase: F2 · Aceptación: Las demás solicitudes activas del cuarto quedan `rejected` con `not_available` y las visitas activas de otros estudiantes a ese cuarto quedan canceladas por el arrendador; el cuarto deja de salir en la búsqueda; los pasos se pueden repetir sin duplicar efectos. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T05 · Mi renta** — Requisito: NV-18-R05 · Fase: F2 · Aceptación: Devuelve el convenio `active` con próximo pago calculado, dirección exacta, ubicación, reglas y `conversationId`; sin renta activa 404 `no_active_rental`; tras terminar la renta ya no sale la dirección. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T06 · Convenios del arrendador, cancelar y fin de renta** — Requisito: NV-18-R06 · Fase: F2 · Aceptación: Listado y detalle por estado y cuarto sin el código; cancelar solo `pending`; terminar solo `active` con motivo opcional, el cuarto vuelve a `available`; historial conservado; puerto de historial de rentas para NV-19. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [ ] **T07 · Protección contra fuerza bruta y privacidad** — Requisito: NV-18-R07 · Fase: F3 · Aceptación: Rate limits y bloqueos por cuenta y por código verificados; hashes sin código en claro en base de datos ni logs; vistas sin datos de contacto; Redis caído 503. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05, T06.

## F4 — Pruebas y evidencia

- [ ] **T08 · Contrato para Mobile y despliegue** — Requisito: NV-18-R08 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (crear, revisar con código, aceptar, Mi renta, cuarto fuera de la búsqueda, terminar y cuarto de nuevo disponible). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas unitarias y de integración** — Requisito: NV-18-R09 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T10 · Pruebas de seguridad** — Requisito: NV-18-R09 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- [ ] **T11 · Evidencia y revisión final** — Requisito: NV-18-R09 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T09, T10.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F2 | ABIERTA |
| T07 | R07 | F3 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R09 | F4 | ABIERTA |
| T10 | R09 | F4 | ABIERTA |
| T11 | R09 | F4 | ABIERTA |

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
| R09 | T09, T10, T11 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Tasks | 11 |
| Tasks abiertas | 11 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
