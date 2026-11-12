# NV-20 — Notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-75 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Notifications |
| Precondición | NV-16, NV-17 1.3.0, NV-18 1.1.0 y NV-21 desplegadas |
| Relación | Recibe avisos de NV-16 (solicitudes y chat), NV-17 (visitas), NV-18 (convenios) y NV-21 (revisión de edificios); el menú de la app muestra el contador de no leídas (RF-74) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-12):** se adapta a lo ya desplegado (NV-16, NV-17 1.3.0, NV-18 1.1.0 y NV-21). Como todavía no hay bus de eventos (M-BE-070), los casos de uso que ya existen llaman a un puerto `INotificationSink` después de guardar su cambio; si la notificación falla, la acción principal no se deshace. El catálogo de tipos queda cerrado y cada uno con su destinatario y su categoría (`requests`, `visits`, `system`, como las pestañas del prototipo). Los mensajes de chat se agrupan por conversación y nunca copian el texto del mensaje. Las rutas usan subrutas (`/read`, `/read-all`) como NV-16. Push (FCM), correo y el aviso de baja de precio de un favorito quedan fuera de esta versión.

## 1. Contexto

Hoy el estudiante y el arrendador solo se enteran de una solicitud aceptada, una visita confirmada, un convenio aceptado o un edificio aprobado si abren cada pantalla. RF-75 pide una bandeja de notificaciones con filtro de no leídas y la opción de marcarlas como leídas; el prototipo tiene la pantalla para el arrendador (pestañas Todas, Solicitudes, Visitas y Sistema, con «Marcar leídas») y para el estudiante (Todas y Sin leer). Las acciones que generan los avisos ya existen en NV-16, NV-17, NV-18 y NV-21.

## 2. Objetivo

Que cada usuario tenga una bandeja de notificaciones con contador de no leídas, alimentada por las acciones de solicitudes, chat, visitas, convenios y revisión de edificios, sin exponer datos personales.

## 3. Precondiciones

1. NV-16, NV-17 1.3.0, NV-18 1.1.0 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Puerto `INotificationSink` y su implementación: crea la notificación del catálogo para el destinatario correcto, de forma idempotente.
2. Llamadas al puerto desde los casos de uso existentes de NV-16, NV-17, NV-18 y NV-21 (después de guardar).
3. Bandeja paginada con filtros `unread` y `category`.
4. Contador de no leídas (total y por categoría) con `ETag` y 304.
5. Marcar una como leída y marcar todas (o todas de una categoría).
6. Agrupación de mensajes de chat: una notificación no leída por conversación con su contador.
7. Retención de 90 días.
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Notificaciones push (FCM), correo y SMS: requieren un proveedor nuevo y una decisión M-BE aparte.
- Aviso de baja de precio de un favorito (texto del prototipo): versión posterior.
- Preferencias para silenciar tipos de notificación.
- Borrar notificaciones: vencen solas a los 90 días.
- Bus de eventos u outbox: se agregará cuando haya más consumidores.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me/notifications` | Autenticado | Bandeja; filtros `unread` y `category` | 200, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/notifications/unread-count` | Autenticado | No leídas: total y por categoría; `ETag` y 304 | 200, 304, 401, 429, 503 |
| POST | `/api/v1/users/me/notifications/{id}/read` | Autenticado (dueño) | Marca una como leída (idempotente) | 204, 400, 401, 404, 429, 503 |
| POST | `/api/v1/users/me/notifications/read-all` | Autenticado | Marca todas, o las de `{ category }`, como leídas | 204, 400, 401, 429, 503 |

- Notificación: `{ id, type, category: "requests" | "visits" | "system", title, body, target: { kind: "request" | "conversation" | "visit" | "agreement" | "rental" | "building", id }, count, read, createdAt, updatedAt }`. `title` y `body` ya vienen en español; `count` es 1 salvo en mensajes agrupados; `target` indica a qué pantalla lleva.
- Catálogo cerrado (tipo → destinatario, categoría, destino): `request_received` → arrendador, requests, request; `request_cancelled` → arrendador, requests, request; `request_accepted` → estudiante, requests, request; `request_rejected` → estudiante, requests, request (incluye el rechazo automático al rentarse el cuarto); `message_received` → el otro participante, requests, conversation; `visit_requested` → arrendador, visits, visit; `visit_confirmed` → estudiante, visits, visit; `visit_rescheduled` → estudiante, visits, visit; `visit_cancelled` → la otra parte, visits, visit; `agreement_activated` → arrendador, requests, agreement; `agreement_ended` → estudiante, requests, rental; `building_approved`, `building_changes_requested`, `building_rejected`, `building_suspended`, `building_unsuspended` → arrendador, system, building.
- Textos: nombre del perfil sin apellidos y título del cuarto (`Casa Azul · Cuarto 101`), fecha y hora en `America/Mexico_City`; nunca dirección exacta, teléfono, correo, texto del chat, notas de rechazo o de moderación, ni ids de usuario.
- Bandeja: `unread=true` (opcional), `category` (`requests`, `visits`, `system`), `page`, `pageSize` ≤ 50; orden `updatedAt` desc e id.
- Contador: `{ unread, requests, visits, system }`. Responde `ETag` débil y `Cache-Control: private, no-cache`; con `If-None-Match` igual responde 304 sin cuerpo. La app puede consultarlo cada 30 segundos o al volver a primer plano.
- Marcar todas: cuerpo opcional `{ category }`; sin cuerpo marca todas.
- El resto de las respuestas lleva `no-store`.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/notifications?unread=true&category=requests&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      unread (true|false), category (requests|visits|system), page, pageSize
RESPONSE     200 { items: [{ id, type, category: "requests" | "visits" | "system", title, body, target: { kind: "request" | "conversation" | "visit" | "agreement" | "rental" | "building", id }, count, read, createdAt, updatedAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden updatedAt desc, id desc
```

```text
ENDPOINT     /api/v1/users/me/notifications/unread-count
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      If-None-Match: <ETag> (opcional)
REQUEST      —
RESPONSE     200 { unread, requests, visits, system } con ETag | 304
STATUS CODES 200, 304, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/notifications/{id}/read
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     204
STATUS CODES 204, 400, 401, 404 ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/notifications/read-all
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo o { category }
RESPONSE     204
STATUS CODES 204, 400, 401, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Notifications** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `notifications` |
| Usa (puertos de otros módulos) | Listings `IRoomRepository`, `IBuildingRepository` e `ILandlordProfileRepository` (NV-12) para el título del cuarto y el nombre del arrendador; Profile `IProfileRepository` (NV-11) para el nombre del estudiante |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | Llamadas al puerto `INotificationSink` desde Messaging (NV-16), Visits (NV-17), Rentals (NV-18) y Moderation (NV-21); sin bus de eventos todavía (M-BE-070) |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `notifications` (colección nueva del módulo Notifications): `userId`, `type`, `category`, `title`, `body`, `targetKind`, `targetId`, `dedupeKey`, `count`, `unread`, `readAt`, `createdAt`, `updatedAt`.
- Índices: `{ userId, updatedAt: -1 }` para la bandeja; `{ userId, unread, category }` para el contador; único parcial `{ userId, dedupeKey }` en no leídas (idempotencia y agrupación); TTL de 90 días sobre `createdAt`.
- `dedupeKey` identifica el hecho: por ejemplo `request_accepted:<requestId>`, `visit_rescheduled:<visitId>:<updatedAt>`, `message:<conversationId>`. Repetir el mismo hecho no crea otra notificación; un mensaje nuevo en una conversación con aviso no leído suma `count` y actualiza `updatedAt`.
- Sin migraciones: las acciones anteriores a NV-20 no generan notificaciones retroactivas.

## 9. Reglas de negocio

1. Cada caso de uso emisor llama al puerto solo después de guardar su cambio; si crear la notificación falla (MongoDB caído), la acción principal responde igual y se registra una advertencia sin datos personales.
2. El destinatario sale del recurso (dueño del cuarto o estudiante de la solicitud, visita o convenio), nunca del cliente; quien hizo la acción no se notifica a sí mismo.
3. Mensajes: mientras la notificación de una conversación siga sin leer, cada mensaje nuevo suma `count`; al leerla, el siguiente mensaje crea otra. Leer los mensajes en el chat (NV-16 `/read`) también marca leída la notificación de esa conversación.
4. Marcar como leída es idempotente; marcar una ajena responde 404.
5. Las notificaciones vencen a los 90 días.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: cada consulta filtra por el `sub`; una notificación ajena responde 404.
- Textos sin datos sensibles (RNF-02): sin dirección exacta, teléfono, correo, texto del chat ni notas; prueba que lo verifica para cada tipo.
- El contador usa `ETag` con `Cache-Control: private, no-cache` para que ninguna caché compartida lo guarde (constitution §23).
- Rate limit 60/min por usuario en la bandeja y las marcas, y 30/min en el contador; Redis caído → 503.
- Entradas con campos desconocidos → 400.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-20-R01 | Generación desde las acciones | Cada tipo del catálogo se crea para el destinatario correcto cuando ocurre su acción en NV-16, NV-17, NV-18 o NV-21; repetir el hecho no duplica; quien actúa no se notifica; si MongoDB falla la acción principal no se deshace. | F1 | T01 |
| NV-20-R02 | Textos sin datos sensibles | Cada tipo produce `title` y `body` en español con nombre sin apellidos y título del cuarto; ninguno contiene dirección, teléfono, correo, texto del chat, notas ni ids. | F1 | T02 |
| NV-20-R03 | Bandeja y filtros | Lista paginada por `updatedAt` desc con filtros `unread` y `category`; validación de parámetros. | F2 | T03 |
| NV-20-R04 | Marcar como leídas | Una (idempotente; ajena 404) o todas, opcionalmente por categoría; leer el chat marca la notificación de esa conversación. | F2 | T04 |
| NV-20-R05 | Contador eficiente | Total y por categoría con índice; `ETag` débil, `private, no-cache` y 304 cuando no cambió. | F2 | T05 |
| NV-20-R06 | Agrupación de mensajes y retención | Una notificación no leída por conversación con `count`; TTL de 90 días. | F2 | T06 |
| NV-20-R07 | Privacidad, autorización y límites | Pruebas con dos usuarios; rate limits 429; Redis caído 503; campos desconocidos 400. | F3 | T07 |
| NV-20-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitud, aceptación, mensaje agrupado, visita confirmada, convenio, contador con 304 y marcar leídas). | F4 | T08 |
| NV-20-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Canal | Solo bandeja en la app (RF-75); push con FCM, correo y SMS fuera hasta una decisión M-BE con proveedor | PROPUESTO |
| Emisión | Puerto `INotificationSink` llamado por los casos de uso tras guardar; si falla, se registra y la acción principal no se deshace (sin outbox ni bus de eventos todavía) | PROPUESTO |
| Catálogo | 16 tipos cerrados con destinatario y categoría (`requests`, `visits`, `system`) como las pestañas del prototipo | PROPUESTO |
| Mensajes | Agrupados por conversación con `count`; sin texto del mensaje (el prototipo mostraba una cita) | PROPUESTO |
| Contador | `ETag` débil con `private, no-cache` y 304; la app consulta cada 30 segundos o al volver a primer plano | PROPUESTO |
| Retención | 90 días con TTL; sin borrar a mano | PROPUESTO |
| Baja de precio de favoritos | Fuera de 1.1.0; versión posterior | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva con TTL de 90 días; sin proveedores nuevos |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-20-RK01 | Notificación perdida si MongoDB falla justo después de la acción | Aviso que no llega | Advertencia registrada; la información sigue en la pantalla de origen; outbox en una versión posterior |
| NV-20-RK02 | Fuga de datos en los textos | Privacidad | Plantillas fijas por tipo y prueba sin dirección, teléfono, correo ni texto del chat |
| NV-20-RK03 | Notificaciones duplicadas | Ruido | `dedupeKey` con índice único parcial |
| NV-20-RK04 | Sondeo del contador muy frecuente | Costo en M0 | `ETag` y 304, rate limit 30/min e índice por usuario y categoría |

## 16. Dependencias

- NV-16, NV-17 1.3.0, NV-18 1.1.0 y NV-21 (emisores).
- RF-74 (menú con contador) en Mobile.

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
