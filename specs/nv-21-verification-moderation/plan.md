# NV-21 — Verificación, reportes y moderación — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T14; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; asignar roles en Production requiere autorización aparte) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T14 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero roles, bitácora y el comando de asignación; después la revisión de edificios y la vista del dueño; luego reportes y suspensiones (incluido el bloqueo en login y refresh); al final pruebas con los cuatro tipos de usuario, la asignación autorizada de roles en Production y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04, R05 | T01, T02, T03, T04, T05 | Roles y asignación auditada; Cola y detalle de revisión; Decisiones de revisión; Verificación para el dueño; Reportes |
| F2 | Persistencia e integración | R06, R07, R08, R09 | T06, T07, T08, T09 | Resolución de reportes; Suspensión de edificios; Suspensión de cuentas; Bitácora inmutable |
| F3 | Seguridad, autorización y límites | R10 | T10 | Autorización y límites |
| F4 | Pruebas y evidencia | R11, R12 | T11, T12, T13, T14 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Roles y asignación auditada** (R01): `staff_roles` consultado en cada solicitud; `--grant-role`/`--revoke-role` por correo con registro en la bitácora y protección de Production; sin rol 403 `role_required`.
- **T02 · Cola y detalle de revisión** (R02): `GET /admin/buildings` filtra por estado y pagina; el detalle trae edificio, cuartos y arrendador sin teléfono ni correo.
- **T03 · Decisiones de revisión** (R03): Aprobar, pedir correcciones y rechazar con motivo y `version`, con sus efectos en cuartos; propio 403 `own_building`; estado inválido 409.
- **T04 · Verificación para el dueño** (R04): `GET /landlord/buildings/{id}/verification` devuelve estado y último motivo; ajeno 404.
- **T05 · Reportes** (R05): Crear reportes de edificio, cuarto o cuenta con catálogo de motivos; objetivo inexistente 404; sin duplicados abiertos (409); no a uno mismo.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T06 · Resolución de reportes** (R06): Cola por estado; resolver con `dismiss`, `suspend_building` o `suspend_user` aplica la acción y cierra el reporte.
- **T07 · Suspensión de edificios** (R07): `verified` ⇄ `suspended` con motivo; otros estados 409.
- **T08 · Suspensión de cuentas** (R08): Bloquea login y refresh con 403 `account_suspended`, revoca refresh tokens y suspende edificios verificados; reactivación; no a `admin`.
- **T09 · Bitácora inmutable** (R09): Cada acción administrativa escribe un evento; `GET /admin/audit` paginado; no existe ruta ni método para modificarla.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T10 · Autorización y límites** (R10): Pruebas con usuario sin rol, revisor, admin y dueño; rate limits con 429; Redis caído 503; `no-store`; motivos de reporte fuera de logs.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T11 · Contrato para Mobile y despliegue** (R11): Contrato de la §6.1 entregado a Mobile; roles asignados en Production con autorización; prueba HTTP real en Azure (aprobar y rechazar, reporte, suspensión) incluido resolver el edificio de prueba de NV-12.
- **T12 · Pruebas unitarias y de integración** (R12): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T13 · Pruebas de seguridad** (R12): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T14 · Evidencia y revisión final** (R12): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: autorización «EJECUTA».
- T05 depende de: autorización «EJECUTA».
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T01, T02, T03, T04, T05.
- T09 depende de: T01, T02, T03, T04, T05.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- T11 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T12 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- T13 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- T14 depende de: T12, T13.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Roles y asignación auditada | Pruebas automatizadas en CI |
| R02 | Cola y detalle de revisión | Pruebas automatizadas en CI |
| R03 | Decisiones de revisión | Pruebas automatizadas en CI |
| R04 | Verificación para el dueño | Pruebas automatizadas en CI |
| R05 | Reportes | Pruebas automatizadas en CI |
| R06 | Resolución de reportes | Pruebas automatizadas en CI |
| R07 | Suspensión de edificios | Pruebas automatizadas en CI |
| R08 | Suspensión de cuentas | Pruebas automatizadas en CI |
| R09 | Bitácora inmutable | Pruebas automatizadas en CI |
| R10 | Autorización y límites | Pruebas automatizadas en CI |
| R11 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R12 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Rutas `/admin/*` verifican el rol en cada solicitud (OWASP API5, autorización a nivel de función); sin rol → 403 `role_required`.
- El rol se lee de `staff_roles` en cada solicitud: retirarlo surte efecto de inmediato.
- El detalle de revisión no expone teléfono ni correo del arrendador; la verificación del dueño no revela al revisor.
- Rate limit en Redis: 120 solicitudes por minuto por usuario en `/admin/*`; reportes 10 por día y 60 por minuto por usuario; Redis caído → 503.
- Motivos y detalles de reportes no se registran en logs; la bitácora guarda el motivo de las decisiones.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Cinco colecciones pequeñas; un comando Redis por solicitud |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F1 | T04 |
| R05 | F1 | T05 |
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F2 | T08 |
| R09 | F2 | T09 |
| R10 | F3 | T10 |
| R11 | F4 | T11 |
| R12 | F4 | T12, T13, T14 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — T01–T14 autorizadas**.
