# NV-16 — Contacto, solicitudes y chat

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-16-contact-chat-requests/`) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-34, RF-35 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-03 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Messaging |
| Precondición | NV-11, NV-12, NV-14, NV-17 1.2.0 y NV-21 desplegadas |
| Relación | Una solicitud aceptada habilita el cierre de trato (NV-18); la visita es de NV-17; las notificaciones serán de NV-20; los reportes, de NV-21 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-10):** se adapta a lo ya desplegado. Las solicitudes se hacen sobre los cuartos publicados de NV-14 (`roomId`); el arrendador se identifica por su perfil de NV-12 y su suspensión por NV-21. El horario propuesto **no se duplica**: la visita sigue siendo de NV-17 y la solicitud muestra la visita activa del estudiante para ese cuarto. Se agregan cancelar por el estudiante, marcar como leído e idempotencia del envío de mensajes (`clientMessageId`). Acciones como sub-recursos (`/accept`, `/reject`, `/cancel`) en lugar de `:accept`. Chat por consulta incremental con cursor y `ETag`; tiempo real (SignalR) y eventos de dominio fuera de v1. La confirmación de la visita por el arrendador y la dirección exacta (RNF-02) quedan para NV-17 1.3.0; cerrar trato es NV-18.

## 1. Contexto

Con NV-14 el estudiante ya encuentra cuartos y con NV-17 pide visitas, pero no puede preguntarle nada al arrendador ni saber si le interesa rentarle. RF-34 pide contactarlo con preguntas rápidas y un mensaje, y conversar por chat sin compartir el teléfono; RF-35 pide que el arrendador vea las solicitudes por estado y las acepte o rechace con motivo, y que aceptar lleve a cerrar trato. El arrendador ya tiene perfil y cuartos (NV-12) y la moderación ya puede suspender cuentas (NV-21).

## 2. Objetivo

Permitir que un estudiante envíe una solicitud de interés por un cuarto publicado, que el arrendador la acepte o rechace con motivo, y que ambos conversen por chat privado sin exponer teléfono ni correo.

## 3. Precondiciones

1. NV-11, NV-12, NV-14, NV-17 1.2.0 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Crear una solicitud de interés con preguntas rápidas y mensaje (RF-34).
2. Bandeja del estudiante: listar, ver y cancelar sus solicitudes.
3. Bandeja del arrendador: listar por estado y cuarto, ver, aceptar y rechazar con motivo (RF-35).
4. Conversación privada por solicitud con mensajes por consulta incremental (RF-34).
5. Contador de no leídos y marcar como leído.
6. Límites contra spam y bloqueo de cuentas suspendidas (RNF-03).
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Programar visitas: ya es NV-17; la solicitud solo muestra la visita activa.
- Confirmar la visita por el arrendador y mostrar la dirección exacta (RNF-02): NV-17 1.3.0.
- Cerrar trato y marcar el cuarto como rentado: NV-18.
- Notificaciones push o por correo: NV-20.
- Tiempo real (WebSockets o SignalR), adjuntos, audio y videollamadas.
- Reportar un mensaje: se reporta la cuenta con NV-21.
- Eventos de dominio (`RequestCreated`, `MessageSent`): fuera de v1.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/rooms/{roomId}/requests` | Autenticado | Crea la solicitud y su conversación | 201, 400, 401, 404, 409, 422, 429, 503 |
| GET | `/api/v1/users/me/requests` | Autenticado | Solicitudes enviadas, filtrables por `status` | 200, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/requests/{id}` | Autenticado (dueño) | Detalle de una solicitud enviada | 200, 400, 401, 404, 429, 503 |
| POST | `/api/v1/users/me/requests/{id}/cancel` | Autenticado (dueño) | Cancela una solicitud `pending` o `accepted` | 200, 400, 401, 404, 409, 429, 503 |
| GET | `/api/v1/landlord/requests` | Arrendador | Solicitudes recibidas por `status` y `roomId` | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/landlord/requests/{id}` | Arrendador (dueño del cuarto) | Detalle de una solicitud recibida | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/landlord/requests/{id}/accept` | Arrendador (dueño del cuarto) | Acepta una solicitud `pending` | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/requests/{id}/reject` | Arrendador (dueño del cuarto) | Rechaza con `reason` y `note` opcional | 200, 400, 401, 403, 404, 409, 429, 503 |
| GET | `/api/v1/conversations` | Autenticado | Conversaciones del usuario con no leídos | 200, 400, 401, 429, 503 |
| GET | `/api/v1/conversations/{id}/messages` | Participante | Mensajes después de un cursor `after` | 200, 304, 400, 401, 404, 429, 503 |
| POST | `/api/v1/conversations/{id}/messages` | Participante | Envía `{ text, clientMessageId }` | 201, 200, 400, 401, 404, 409, 429, 503 |
| POST | `/api/v1/conversations/{id}/read` | Participante | Marca como leído hasta `lastMessageId` | 204, 400, 401, 404, 429, 503 |

- Solicitud: `{ id, status, room: { id, buildingId, title, city }, quickQuestions: [..], message, student: { name, university } | null, landlord: { name }, visit: { id, scheduledAt, status } | null, conversationId, rejection: { reason, note } | null, roomAvailable, createdAt, updatedAt }`. Crear: `{ quickQuestions: [..] (0-5 del catálogo `available_now`, `services_included`, `deposit`, `minimum_contract`, `visit_possible`, `pets_allowed`, `roommates`), message (1-500) }`; al menos una pregunta o un mensaje.
- `student` lo ve solo el arrendador: `name` del perfil de NV-11 (o `null`) y nombre de la universidad; `landlord.name` es el primer nombre del perfil de NV-12. Nunca teléfono, correo, apellidos ni ids de usuario.
- `visit` es la visita activa (`requested`) del estudiante para ese cuarto según NV-17, o `null`; el arrendador ve fecha y estado, no el mensaje de la visita.
- Rechazo: `reason` (`not_available`, `profile_mismatch`, `other`) y `note` opcional (≤ 300); `other` exige `note`.
- Listados con el sobre de NV-01 (`page`, `pageSize` ≤ 50), orden `updatedAt` desc e id; `status` = `pending`, `accepted`, `rejected`, `cancelled`.
- Conversación: `{ id, requestId, room: { id, title }, counterpart: { name, role }, status: "open" | "closed", lastMessageAt, unreadCount }`; mensaje: `{ id, senderRole: "student" | "landlord", text, createdAt, mine }`.
- `messages`: `after` (id del último mensaje conocido, opcional) y `limit` (1-50, por defecto 50); orden ascendente; responde `{ items, hasMore, pollAfterSeconds: 10 }` con `ETag` del último mensaje; con `If-None-Match` igual y sin mensajes nuevos → 304.
- Enviar: `text` 1-1000 caracteres y `clientMessageId` (UUID) obligatorio; repetir el mismo `clientMessageId` devuelve 200 con el mismo mensaje (doble toque); conversación cerrada → 409 `code=conversation_closed`.
- Todas las respuestas con `no-store`.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/rooms/{roomId}/requests
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { quickQuestions: [..], message }
RESPONSE     201 { id, status, room: { id, buildingId, title, city }, quickQuestions: [..], message, student: { name, university } | null, landlord: { name }, visit: { id, scheduledAt, status } | null, conversationId, rejection: { reason, note } | null, roomAvailable, createdAt, updatedAt }
STATUS CODES 201, 400, 401, 404 cuarto no publicado, 409 code=active_request_exists | active_requests_limit, 422 code=own_room, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/requests?status=pending&page=1&pageSize=20 y /users/me/requests/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      status, page, pageSize | id
RESPONSE     200 sobre de NV-01 con solicitudes | 200 solicitud
STATUS CODES 200, 400, 401, 404 ajena, 429, 503
PAGINATION   Sí: orden updatedAt desc, id
```

```text
ENDPOINT     /api/v1/users/me/requests/{id}/cancel
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 solicitud (cancelled)
STATUS CODES 200, 401, 404, 409 code=request_state, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/requests?status=pending&roomId=..&page=1 y /landlord/requests/{id}
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      status, roomId, page, pageSize | id
RESPONSE     200 sobre de NV-01 con solicitudes | 200 solicitud
STATUS CODES 200, 400, 401, 403 sin perfil de arrendador, 404 ajena, 429, 503
PAGINATION   Sí
```

```text
ENDPOINT     /api/v1/landlord/requests/{id}/accept | /reject
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      accept: sin cuerpo; reject: { reason, note }
RESPONSE     200 solicitud
STATUS CODES 200, 400, 401, 403, 404, 409 code=request_state | room_unavailable, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/conversations?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [{ id, requestId, room: { id, title }, counterpart: { name, role }, status: "open" | "closed", lastMessageAt, unreadCount }], ... }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden lastMessageAt desc, id
```

```text
ENDPOINT     /api/v1/conversations/{id}/messages?after=..&limit=50
METHOD       GET
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      after, limit; If-None-Match opcional
RESPONSE     200 { items: [{ id, senderRole: "student" | "landlord", text, createdAt, mine }], hasMore, pollAfterSeconds } con ETag | 304
STATUS CODES 200, 304, 400, 401, 404, 429, 503
PAGINATION   Cursor `after`
```

```text
ENDPOINT     /api/v1/conversations/{id}/messages
METHOD       POST
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      { text (1-1000), clientMessageId (UUID) }
RESPONSE     201 { id, senderRole: "student" | "landlord", text, createdAt, mine } | 200 si se repite clientMessageId
STATUS CODES 201, 200, 400, 401, 404, 409 code=conversation_closed | participant_suspended, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/conversations/{id}/read
METHOD       POST
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      { lastMessageId }
RESPONSE     204
STATUS CODES 204, 400, 401, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Messaging** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `rental_requests`, `conversations`, `messages` |
| Usa (puertos de otros módulos) | Favorites `RoomCatalog` (NV-15 1.2.0) para validar cuartos publicados; Listings `IRoomRepository`, `IBuildingRepository` e `ILandlordProfileRepository` (NV-12) para el dueño y su nombre; Profile `IProfileRepository` (NV-11) para el nombre y la universidad del estudiante; Visits `IVisitRepository` (NV-17) para la visita activa; Moderation `IAccountSuspensionQuery` (NV-21) para bloquear cuentas suspendidas |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `rental_requests`: `roomId`, `buildingId`, `studentId`, `ownerId`, `quickQuestions`, `message`, `status`, `rejection { reason, note }`, `conversationId`, `createdAt`, `updatedAt`; índice único parcial `{ studentId, roomId }` cuando `status` es `pending` o `accepted`; índices `{ ownerId, status, updatedAt }` y `{ studentId, updatedAt }`.
- `conversations`: `requestId` (único), `roomId`, `studentId`, `ownerId`, `status` (`open`, `closed`), `lastMessageAt`, `lastMessageId`, `lastReadMessageId` por participante; índices `{ studentId, lastMessageAt }` y `{ ownerId, lastMessageAt }`.
- `messages`: `conversationId`, `senderId`, `senderRole`, `text`, `clientMessageId`, `createdAt`; índice `{ conversationId, _id }` y único `{ conversationId, senderId, clientMessageId }`.
- El contador de no leídos se calcula con los mensajes del otro participante posteriores a `lastReadMessageId`, con tope de 99.

## 9. Reglas de negocio

1. Solo cuartos publicados (NV-14) aceptan solicitudes nuevas; uno no publicado responde 404.
2. Una sola solicitud activa (`pending` o `accepted`) por estudiante y cuarto → 409 `active_request_exists`; máximo 10 solicitudes activas por estudiante → 409 `active_requests_limit`.
3. El arrendador no puede solicitar su propio cuarto → 422 `own_room`.
4. Transiciones: `pending` → `accepted` | `rejected` (arrendador); `pending` | `accepted` → `cancelled` (estudiante); cualquier otra → 409 `request_state`. Aceptar exige que el cuarto siga publicado → si no, 409 `room_unavailable`.
5. La conversación nace con la solicitud y se cierra al rechazarla o cancelarla; cerrada se puede leer pero no escribir.
6. Si cualquiera de los dos participantes está suspendido (NV-21) no se puede crear solicitud ni enviar mensajes → 409 `participant_suspended`; la consulta se hace en cada escritura.
7. Los mensajes son inmutables y se guardan como texto plano; la app no los interpreta como HTML.
8. Si el cuarto deja de estar publicado, las solicitudes existentes siguen visibles con `roomAvailable: false`.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: el estudiante solo ve sus solicitudes, el arrendador solo las de sus cuartos y cada conversación solo sus dos participantes; lo ajeno responde 404.
- Teléfono, correo, apellidos e ids de usuario nunca aparecen en solicitudes ni en el chat (RNF-02).
- Rate limit: 10 solicitudes nuevas al día por estudiante, 30 mensajes por minuto por usuario, 12 consultas de mensajes por minuto por conversación y 60/min por usuario en el resto.
- El contenido de solicitudes y mensajes no se registra en logs.
- Asignación masiva bloqueada: el cuerpo no acepta `status`, `studentId`, `ownerId` ni `senderId`.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Estado o regla de negocio en conflicto | 409 | Conflicto (`code`: `active_request_exists`, `active_requests_limit`, `request_state`, `room_unavailable`, `conversation_closed`, `participant_suspended`) |
| Solicitar el propio cuarto | 422 | Solicitud no procesable (`code=own_room`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-16-R01 | Crear solicitud | Valida preguntas del catálogo y mensaje; crea solicitud y conversación en la misma operación; 404 cuarto no publicado; 409 activa o límite; 422 propio cuarto; 409 participante suspendido. | F1 | T01 |
| NV-16-R02 | Bandeja del estudiante y cancelación | Lista, detalle y cancelación de sus solicitudes con la visita activa de NV-17; transiciones válidas; lo ajeno 404. | F1 | T02 |
| NV-16-R03 | Bandeja del arrendador, aceptar y rechazar | Lista por estado y cuarto, detalle, aceptar y rechazar con motivo; solo el dueño del cuarto; 409 en transiciones inválidas o cuarto no publicado. | F1 | T03 |
| NV-16-R04 | Chat por consulta incremental | Mensajes ordenados con cursor `after`, `hasMore` y `ETag`/304; envío idempotente por `clientMessageId`; conversación cerrada 409. | F2 | T04 |
| NV-16-R05 | No leídos y lectura | `unreadCount` por conversación y `read` hasta un mensaje; nunca cuenta los propios mensajes. | F2 | T05 |
| NV-16-R06 | Persistencia e índices | Índice único parcial de solicitudes activas, único de `clientMessageId` y de lectura de mensajes por conversación; prueba de doble toque concurrente. | F2 | T06 |
| NV-16-R07 | Privacidad y abuso | Ninguna respuesta incluye teléfono, correo, apellidos ni ids de usuario; rate limits con 429; Redis caído 503; cuentas suspendidas no escriben; contenido fuera de logs. | F3 | T07 |
| NV-16-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure con un estudiante y un arrendador de prueba (solicitar, aceptar, rechazar, cancelar, chatear, 304 y no leídos). | F4 | T08 |
| NV-16-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Horario propuesto | No se duplica: se muestra la visita activa de NV-17 | APROBADO (M-BE-068) |
| Tiempo real | Consulta incremental con cursor, `ETag` y `pollAfterSeconds: 10`; SignalR con backplane Redis como evolución | APROBADO (M-BE-068) |
| Catálogo de preguntas rápidas | `available_now`, `services_included`, `deposit`, `minimum_contract`, `visit_possible`, `pets_allowed`, `roommates` | APROBADO (M-BE-068) |
| Nombres visibles | Primer nombre del arrendador (NV-12) y nombre del perfil del estudiante (NV-11) con su universidad | APROBADO (M-BE-068) |
| Límites | 10 solicitudes nuevas al día, 10 activas, 30 mensajes/min, 12 consultas/min por conversación | APROBADO (M-BE-068) |
| Retención de mensajes | Mientras exista la cuenta; se anonimizan cuando exista la eliminación de cuenta | APROBADO (M-BE-068) |
| Confirmación de visita y dirección exacta | NV-17 1.3.0, coordinada con Mobile | PENDIENTE |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Mensajes pequeños; la consulta incremental cada 10 s con 304 y un comando Redis por consulta; se mide el consumo de comandos en la prueba de Azure |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-16-RK01 | Lectura de conversaciones ajenas | Privacidad | Filtro por participante en cada consulta y pruebas BOLA |
| NV-16-RK02 | Spam a arrendadores | Abuso | 10 solicitudes al día, 10 activas, rate limits y reporte de cuenta (NV-21) |
| NV-16-RK03 | Consulta incremental costosa | Comandos de Redis y CPU | Cursor, 304, `pollAfterSeconds` y límite por conversación |
| NV-16-RK04 | Compartir teléfono dentro del texto | Contacto fuera de la app | Aviso en la app; la API nunca lo agrega por su cuenta |
| NV-16-RK05 | Duplicar el horario con NV-17 | Datos inconsistentes | La visita vive solo en NV-17 |

## 16. Dependencias

- NV-11, NV-12, NV-14, NV-17 1.2.0 y NV-21.
- NV-17 1.3.0 (confirmación de visita y dirección).
- NV-18 (cierre de trato) y NV-20 (notificaciones).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
