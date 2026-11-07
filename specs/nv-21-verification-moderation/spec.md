# NV-21 — Verificación, reportes y moderación

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T14; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; asignar roles en Production requiere autorización aparte) |
| Estado de ejecución | **✅ COMPLETA — T01–T14 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-37 (revisión), RF-38, RF-39, RF-40, RF-42, RF-43, RF-44, RF-79 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-03, RNF-19 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Moderation |
| Precondición | NV-10 (sesión) y NV-12 (edificios y cuartos) desplegadas |
| Relación | Aprueba los edificios que NV-12 envía a revisión y pasa sus cuartos a `available`, base de la búsqueda de NV-14; NV-13 agregará los documentos a la revisión |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-07):** se adapta a NV-12 1.1.0 ya desplegada y se completa al nivel de las specs anteriores: contrato para Mobile (§6.1) de los endpoints que usa la app y contrato interno de `/admin/*`; acciones como sub-recursos; roles en la colección `staff_roles` consultada en cada solicitud (sin claim en el JWT, igual que el rol de arrendador, M-BE-064), asignados solo por un comando de línea auditado; decisiones con `version` y `code`; estados nuevos del edificio `rejected` y `suspended`; suspensión de cuentas que bloquea login y refresh y revoca los refresh tokens. Los enlaces temporales a documentos llegan con NV-13; los reportes de mensajes y reseñas, con NV-16 y NV-19. Los eventos de dominio salen de v1: Moderation usa los puertos de Listings e Identity directamente.

## 1. Contexto

NV-12 ya permite que un arrendador envíe su edificio a revisión, pero nadie puede aprobarlo: no existen roles administrativos y los cuartos se quedan en `in_review`. Además, la app necesita que los usuarios reporten publicaciones o cuentas sospechosas y que el equipo pueda suspenderlas. En producción ya hay un edificio de prueba en revisión que solo esta spec puede resolver.

## 2. Objetivo

Dar al equipo de TheHouse roles de revisión y administración para aprobar, pedir correcciones o rechazar edificios, atender reportes y suspender contenido o cuentas, con bitácora inmutable.

## 3. Precondiciones

1. NV-10 y NV-12 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile (reportes y estado de verificación).
3. Autorización «EJECUTA»; asignar roles en Production requiere autorización aparte.

## 4. Alcance

1. Roles `reviewer` y `admin` y su asignación por comando de línea auditado.
2. Cola y detalle de revisión de edificios; aprobar, pedir correcciones y rechazar con motivo.
3. Estado de verificación visible para el dueño.
4. Reportes de usuarios sobre edificios, cuartos y cuentas; cola y resolución.
5. Suspensión y reactivación de edificios y de cuentas.
6. Bitácora de auditoría de solo inserción y su consulta.
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Panel web de administración: los endpoints `/admin/*` se usan con un cliente HTTP mientras no exista.
- Documentos y enlaces temporales (RNF-19): con NV-13.
- Reportes de mensajes y reseñas: con NV-16 y NV-19.
- Detección automática de fraude con ML.
- Eventos de dominio (`BuildingReviewDecided`, `UserSuspended`, `ContentHidden`): se agregan cuando exista un consumidor.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/admin/buildings?status=in_review` | Revisor | Cola de edificios por estado | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/admin/buildings/{id}` | Revisor | Detalle para revisar: edificio, cuartos y arrendador | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/admin/buildings/{id}/approve` | Revisor | Aprueba: `verified` y cuartos `available` | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/buildings/{id}/request-changes` | Revisor | Regresa a `draft` con motivo | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/buildings/{id}/reject` | Revisor | Rechaza con motivo (`rejected`) | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/landlord/buildings/{id}/verification` | Dueño | Estado de revisión y último motivo | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/reports` | Autenticado | Reporta un edificio, cuarto o cuenta | 201, 400, 401, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/admin/reports?status=open` | Admin | Reportes por estado | 200, 400, 401, 403, 429, 503 |
| POST | `/api/v1/admin/reports/{id}/resolve` | Admin | Resuelve un reporte con una acción | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/buildings/{id}/suspend` | Admin | Suspende un edificio verificado | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/buildings/{id}/unsuspend` | Admin | Reactiva un edificio suspendido | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/users/{id}/suspend` | Admin | Suspende una cuenta | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/admin/users/{id}/unsuspend` | Admin | Reactiva una cuenta | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/admin/audit` | Admin | Bitácora paginada | 200, 400, 401, 403, 429, 503 |

- Decisiones de revisión: `{ reason, version }`; `reason` de 5 a 500 caracteres, obligatorio salvo en `approve`; `version` es la del edificio leída en el detalle (409 `version_conflict` si cambió).
- Detalle de revisión: el edificio completo de NV-12 (incluida la dirección), sus cuartos y del arrendador solo `fullName`, `rentalMode` y `rfc`; nunca el teléfono ni el correo.
- `verification` (dueño): `{ status, lastDecision: { decision, reason, decidedAt } | null }`; no revela quién revisó.
- Reporte: `{ targetType (building|room|user), targetId, reason (fake|fraud|inappropriate|spam|other), details (opcional, ≤ 500) }` → 201 `{ id, status: "open", createdAt }`. Un mismo usuario no repite reporte abierto del mismo objetivo (409 `report_exists`); objetivo inexistente → 404; no se puede reportar a uno mismo (400).
- Resolver reporte: `{ action (dismiss|suspend_building|suspend_user), reason }`; la acción se aplica al objetivo del reporte y queda en la bitácora.
- Suspensiones: `{ reason }`. Edificio: solo `verified` → `suspended` y de regreso (409 `building_state`). Cuenta: bloquea login y refresh (403 `account_suspended`), revoca sus refresh tokens y suspende sus edificios verificados; el access token vigente expira en 15 minutos como máximo.
- Bitácora: `{ items: [{ id, actorId, action, targetType, targetId, reason, createdAt }], page, pageSize, totalItems, totalPages }`, orden `createdAt` desc.
- Respuestas con `Cache-Control: no-store`; errores con `code` (M-BE-061).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/reports
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Content-Type: application/json
REQUEST      { targetType, targetId, reason, details? }
RESPONSE     201 { id, status: "open", createdAt }
STATUS CODES 201, 400, 401, 404 objetivo, 409 code=report_exists, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/verification
METHOD       GET
AUTH         Bearer JWT + perfil de arrendador; ajeno = 404
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 { status, lastDecision: { decision, reason, decidedAt } | null }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/auth/login | /api/v1/auth/refresh
METHOD       POST
AUTH         Sin cambios de contrato
HEADERS      —
REQUEST      igual que NV-05/NV-10
RESPONSE     igual; una cuenta suspendida recibe 403 code=account_suspended
STATUS CODES se agrega 403 code=account_suspended
PAGINATION   No
```

```text
ENDPOINT     /api/v1/admin/* (panel interno)
METHOD       GET | POST
AUTH         Bearer JWT + rol `reviewer` o `admin` (403 `role_required` si no lo tiene); reportes, suspensiones y bitácora: Bearer JWT + rol `admin` (403 `role_required` si no lo tiene)
HEADERS      —
REQUEST      ver §6
RESPONSE     ver §6
STATUS CODES 200, 400, 401, 403 code=role_required|own_building, 404, 409 code=building_state|version_conflict|user_state, 413, 415, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Moderation** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `staff_roles`, `verification_reviews`, `reports`, `user_suspensions`, `audit_events` |
| Usa (puertos de otros módulos) | Listings `IBuildingRepository` e `IRoomRepository` (NV-12) para decisiones y suspensiones; Identity `IUserRepository` e `IRefreshTokenStore` (NV-10) para cuentas y revocación |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `staff_roles`: `userId` (único), `roles[]` (`reviewer`, `admin`), `grantedAt`, `grantedBy`.
- `verification_reviews`: `buildingId`, `reviewerId`, `decision` (`approved`, `changes_requested`, `rejected`), `reason`, `createdAt`; índice `{ buildingId, createdAt: -1 }`.
- `reports`: `reporterId`, `targetType`, `targetId`, `reason`, `details`, `status` (`open`, `resolved`, `dismissed`), `resolution`, `createdAt`, `resolvedAt`; índice único parcial `{ reporterId, targetType, targetId }` con `status = open`.
- `user_suspensions`: `userId` (único), `reason`, `suspendedBy`, `createdAt`.
- `audit_events`: `actorId`, `action`, `targetType`, `targetId`, `reason`, `createdAt`; solo inserción; índice `{ createdAt: -1 }`.
- `buildings` (NV-12) agrega los estados `rejected` y `suspended`.

## 9. Reglas de negocio

1. Aprobar: `in_review` → `verified` y sus cuartos `in_review` → `available`. Pedir correcciones: → `draft` y cuartos `in_review` → `ready`. Rechazar: → `rejected` (terminal; el dueño solo puede borrarlo) y cuartos `in_review` → `ready`. Otros estados → 409 `building_state`.
2. Un revisor no decide sobre sus propios edificios (403 `own_building`).
3. Suspender un edificio `verified` lo pasa a `suspended` y deja de ser público; reactivarlo lo regresa a `verified`.
4. Suspender una cuenta: bloquea login y refresh, revoca sus refresh tokens y suspende sus edificios verificados; reactivarla no reactiva los edificios (se hace uno por uno). No se suspende a un `admin` (409 `user_state`).
5. Un usuario no repite un reporte abierto del mismo objetivo; resolverlo lo cierra como `resolved` o `dismissed`.
6. Toda decisión, suspensión, resolución y asignación de rol escribe en `audit_events` dentro de la misma operación de negocio; la aplicación no expone actualización ni borrado de la bitácora.
7. Los roles solo se asignan o retiran con `--grant-role`/`--revoke-role` (correo y rol) desde el contenedor, con `--allow-production-role-change` en Production y autorización explícita; cada cambio queda en la bitácora.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Rutas `/admin/*` verifican el rol en cada solicitud (OWASP API5, autorización a nivel de función); sin rol → 403 `role_required`.
- El rol se lee de `staff_roles` en cada solicitud: retirarlo surte efecto de inmediato.
- El detalle de revisión no expone teléfono ni correo del arrendador; la verificación del dueño no revela al revisor.
- Rate limit en Redis: 120 solicitudes por minuto por usuario en `/admin/*`; reportes 10 por día y 60 por minuto por usuario; Redis caído → 503.
- Motivos y detalles de reportes no se registran en logs; la bitácora guarda el motivo de las decisiones.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin rol administrativo, revisar un edificio propio o cuenta suspendida | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Estado del edificio o de la cuenta, versión o reporte repetido | 409 | Conflicto |
| Cuerpo mayor a 2 KB | 413 | Solicitud demasiado grande |
| Contenido distinto de `application/json` | 415 | Tipo de contenido no admitido |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-21-R01 | Roles y asignación auditada | `staff_roles` consultado en cada solicitud; `--grant-role`/`--revoke-role` por correo con registro en la bitácora y protección de Production; sin rol 403 `role_required`. | F1 | T01 |
| NV-21-R02 | Cola y detalle de revisión | `GET /admin/buildings` filtra por estado y pagina; el detalle trae edificio, cuartos y arrendador sin teléfono ni correo. | F1 | T02 |
| NV-21-R03 | Decisiones de revisión | Aprobar, pedir correcciones y rechazar con motivo y `version`, con sus efectos en cuartos; propio 403 `own_building`; estado inválido 409. | F1 | T03 |
| NV-21-R04 | Verificación para el dueño | `GET /landlord/buildings/{id}/verification` devuelve estado y último motivo; ajeno 404. | F1 | T04 |
| NV-21-R05 | Reportes | Crear reportes de edificio, cuarto o cuenta con catálogo de motivos; objetivo inexistente 404; sin duplicados abiertos (409); no a uno mismo. | F1 | T05 |
| NV-21-R06 | Resolución de reportes | Cola por estado; resolver con `dismiss`, `suspend_building` o `suspend_user` aplica la acción y cierra el reporte. | F2 | T06 |
| NV-21-R07 | Suspensión de edificios | `verified` ⇄ `suspended` con motivo; otros estados 409. | F2 | T07 |
| NV-21-R08 | Suspensión de cuentas | Bloquea login y refresh con 403 `account_suspended`, revoca refresh tokens y suspende edificios verificados; reactivación; no a `admin`. | F2 | T08 |
| NV-21-R09 | Bitácora inmutable | Cada acción administrativa escribe un evento; `GET /admin/audit` paginado; no existe ruta ni método para modificarla. | F2 | T09 |
| NV-21-R10 | Autorización y límites | Pruebas con usuario sin rol, revisor, admin y dueño; rate limits con 429; Redis caído 503; `no-store`; motivos de reporte fuera de logs. | F3 | T10 |
| NV-21-R11 | Contrato para Mobile y despliegue | Contrato de la §6.1 entregado a Mobile; roles asignados en Production con autorización; prueba HTTP real en Azure (aprobar y rechazar, reporte, suspensión) incluido resolver el edificio de prueba de NV-12. | F4 | T11 |
| NV-21-R12 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T12, T13, T14 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Roles | Colección `staff_roles` consultada en cada solicitud, sin claim en el JWT | APROBADO (M-BE-065) |
| Asignación de roles | Comando `--grant-role`/`--revoke-role` vía `az containerapp exec`, con `--allow-production-role-change` y autorización explícita; auditado | APROBADO (M-BE-065) |
| Rutas de acciones | Sub-recursos (`/approve`, `/request-changes`, `/reject`, `/suspend`, `/resolve`) | APROBADO (M-BE-065) |
| Rechazo | Estado `rejected` terminal; para reintentar se crea otro edificio | APROBADO (M-BE-065) |
| Suspensión de cuentas | Bloquea login y refresh y revoca refresh tokens; el access token vigente expira en 15 minutos | APROBADO (M-BE-065) |
| Objetivos de reporte en v1 | `building`, `room` y `user`; mensajes y reseñas con NV-16 y NV-19 | APROBADO (M-BE-065) |
| Documentos | La revisión de documentos y los enlaces temporales llegan con NV-13 | APROBADO (M-BE-065) |
| Retención de reportes | 1 año; la limpieza automática llega en una versión posterior | APROBADO (M-BE-065) |
| Eventos de dominio | Fuera de v1; Moderation llama a los puertos de Listings e Identity | APROBADO (M-BE-065) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Cinco colecciones pequeñas; un comando Redis por solicitud |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-21-RK01 | Escalada de privilegios | Acceso administrativo indebido | Roles fuera de la API, comprobados en cada solicitud y auditados |
| NV-21-RK02 | Revisor que aprueba lo propio | Fraude | Regla `own_building` y bitácora |
| NV-21-RK03 | Suspensión con sesión activa | Acceso hasta 15 minutos | Refresh revocado y bloqueo de login; vigencia corta del access token (NV-05) |
| NV-21-RK04 | Reportes abusivos | Saturación de la cola | Límite diario por usuario y sin duplicados abiertos |
| NV-21-RK05 | Bitácora alterada | Pérdida de trazabilidad | Solo inserción desde la aplicación; prueba que no existe ruta de cambio |

## 16. Dependencias

- NV-10 (sesión y refresh) y NV-12 (edificios y cuartos).
- NV-13 (documentos en la revisión).
- NV-14 (búsqueda pública de cuartos `available` de edificios `verified`).
- NV-16 y NV-19 (reportes de mensajes y reseñas).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 12 |
| Fases del plan | 4 |
| Tasks | 14 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — T01–T14 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
