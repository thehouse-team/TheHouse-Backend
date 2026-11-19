# NV-20 — Notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `spec.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-18 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T08; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-20-notifications/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T04 completas; T05–T08 esperan CI, despliegue, secreto de Firebase y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | Requisito del curso §16 (notificaciones push de los procesos principales); RF-75 (bandeja) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-09 |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |
| Módulo | Notifications |
| Precondición | NV-20 1.1.0 desplegada; proyecto de Firebase de la app Android (Mobile) y su cuenta de servicio |
| Relación | Agrega un canal al puerto `INotificationSink` de NV-20; los eventos siguen naciendo en Messaging (NV-16, incluido el chat de compañero de NV-22), Visits (NV-17), Rentals (NV-18) y Moderation (NV-21); NV-10 1.2.0 borra los dispositivos al eliminar la cuenta |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.2.0-borrador (2026-11-17):** la versión 1.1.0 (bandeja en la app, M-BE-071) quedó completa y desplegada (ver `evidence.md`). El curso pide **notificaciones push para los procesos principales** (§16 Móvil), así que esta versión agrega el canal push con **Firebase Cloud Messaging (FCM)** sobre la misma bandeja: cada notificación que ya se guarda en la bandeja también se envía al teléfono. La app registra su token de FCM por instalación y lo borra al cerrar sesión. El envío es posterior a guardar y nunca deshace la acción principal; la bandeja sigue siendo la fuente de verdad. No se agregan correo ni SMS. Requiere una decisión M-BE nueva (proveedor FCM y un secreto nuevo) que reemplaza la parte «sin push» de M-BE-071.

## 1. Contexto

Hoy las notificaciones solo existen en la bandeja: el usuario se entera de una solicitud, una visita o un mensaje hasta que abre la app. El curso pide push para los procesos principales y Mobile ya tiene FCM integrado; falta que el backend sepa a qué dispositivos enviar y que mande el aviso. M-BE-071 dejó el push fuera hasta elegir proveedor.

## 2. Objetivo

Que cada notificación de la bandeja llegue también como push a los teléfonos donde el usuario tiene la sesión abierta, sin exponer contenido privado en la pantalla de bloqueo y sin que una falla de FCM afecte la acción que la originó.

## 3. Precondiciones

1. NV-20 1.1.0 desplegada.
2. Proyecto de Firebase de la app Android y una cuenta de servicio con permiso solo para enviar mensajes de FCM.
3. Decisión M-BE: FCM como proveedor y el secreto `FIREBASE_SERVICE_ACCOUNT` en la Container App (fuera de Terraform, M-BE-038).
4. Validación del contrato §6.1 con el responsable de Mobile.
5. Autorización «EJECUTA».

## 4. Alcance

1. Registro, actualización y borrado del token de FCM por instalación de la app.
2. Envío push de los 16 tipos de la bandeja (solicitudes, mensajes de chat y de compañero, visitas, convenios y sistema).
3. Textos de push en español sin contenido privado (sin el texto de los mensajes).
4. Limpieza de tokens inválidos que reporta FCM y de los dispositivos al cerrar sesión, al suspender y al eliminar la cuenta.
5. Contrato para Mobile y despliegue verificado en Azure con un teléfono real.

## 5. Fuera de alcance

- Correo y SMS (RF-70 sigue BLOCKED por COST-001).
- iOS y web push.
- Preferencias de push por categoría en el backend (el usuario puede silenciar la app desde Android).
- Garantía de entrega: si el envío falla, la notificación sigue en la bandeja.
- Envíos masivos o de marketing.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/users/me/devices/{installationId}` | Usuario | Registrar o actualizar el token de FCM de esta instalación | 200, 201, 400, 401, 409, 429, 503 |
| DELETE | `/api/v1/users/me/devices/{installationId}` | Usuario | Borrar el dispositivo (antes del logout) | 204, 401, 404, 429, 503 |
| GET | `/api/v1/users/me/devices` | Usuario | Dispositivos registrados (sin tokens) | 200, 401, 429, 503 |

- `installationId` es un UUID que la app genera una vez por instalación. Cuerpo del PUT: `{ fcmToken, platform: "android", appVersion, locale }`; respuesta `{ installationId, platform, appVersion, registeredAt, updatedAt }` (201 la primera vez, 200 después). El token de FCM nunca se devuelve.
- La app llama al PUT después del login, al abrir la app y cuando FCM rota el token (`onNewToken`); y al DELETE antes del logout. Máximo 5 dispositivos por usuario: el sexto reemplaza al más antiguo.
- Si el mismo token de FCM ya está en otra cuenta (teléfono compartido), se mueve a la cuenta actual; la anterior deja de recibir push en ese teléfono.
- Mensaje push: `notification { title, body } + data { notificationId, type, category, targetKind, targetId }`. El título y el cuerpo usan los mismos textos cortos de la bandeja; `message_received` dice «Nuevo mensaje de Ana» sin el texto. Al tocarlo, la app abre el destino con `targetKind`/`targetId` y marca leída la notificación con NV-20 1.1.0.
- Android: prioridad alta solo para `message_received`, `visit_confirmed`, `visit_cancelled` y `request_accepted`; el resto normal. Canal de Android por categoría (`requests`, `visits`, `system`).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/devices/{installationId}
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { fcmToken, platform: "android", appVersion, locale }
RESPONSE     201 o 200 { installationId, platform, appVersion, registeredAt, updatedAt }
STATUS CODES 200, 201, 400, 401, 409, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/devices/{installationId}
METHOD       DELETE
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     204
STATUS CODES 204, 401, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/devices
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { items: [{ installationId, platform, appVersion, registeredAt, updatedAt }] }
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     Push de FCM (no es una ruta)
METHOD       —
AUTH         Firebase (cuenta de servicio en el backend)
HEADERS      —
REQUEST      —
RESPONSE     notification { title, body } + data { notificationId, type, category, targetKind, targetId }
STATUS CODES —
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
| Colecciones propias | `notifications`, `push_devices` |
| Usa (puertos de otros módulos) | Listings `IRoomRepository`, `IBuildingRepository` e `ILandlordProfileRepository` (NV-12) para el título del cuarto y el nombre del arrendador; Profile `IProfileRepository` (NV-11) para el nombre del estudiante |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | `Llamadas al puerto `INotificationSink` desde Messaging (NV-16 y el chat de NV-22), Visits (NV-17), Rentals (NV-18) y Moderation (NV-21)`, `Puerto `IAccountDeletionParticipant` de Identity (NV-10 1.2.0) para borrar bandeja y dispositivos al eliminar la cuenta` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `push_devices` (colección nueva de Notifications): `userId`, `installationId`, `fcmToken`, `platform`, `appVersion`, `locale`, `registeredAt`, `updatedAt`, `lastErrorAt`.
- Índices: único `{ userId, installationId }`; único `{ fcmToken }` (un token pertenece a una sola cuenta); `{ updatedAt }` con TTL de 270 días (FCM considera inactivo un token sin uso por mucho tiempo).
- El envío lee los dispositivos del usuario al guardar la notificación; no hay cola persistente: si la réplica se reinicia antes de enviar, la notificación sigue en la bandeja.

## 9. Reglas de negocio

1. Toda notificación guardada en la bandeja se envía como push a los dispositivos del destinatario; nunca al actor que la provocó.
2. El envío corre después de guardar, fuera de la respuesta HTTP (cola en memoria acotada); un error de FCM se registra sin datos personales y no cambia la respuesta.
3. Token que FCM reporta como `UNREGISTERED` o inválido → se borra el dispositivo; otros errores → se reintenta una vez con espera exponencial.
4. La app borra su dispositivo antes del logout; si no lo hace, el siguiente usuario que registre el mismo token en ese teléfono se queda con él. La eliminación de cuenta (NV-10 1.2.0) borra todos los dispositivos del usuario mediante `IAccountDeletionParticipant`.
5. Mensajes agrupados por conversación: un push por mensaje nuevo, con `collapseKey` por conversación para que Android muestre solo el último.
6. Con dos réplicas cada notificación se envía una sola vez, porque la envía la réplica que la guardó.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Autenticación a FCM con la cuenta de servicio por OAuth 2.0 (JWT firmado con RS256 con la criptografía de .NET, sin paquete nuevo); el token de acceso se guarda en memoria y se renueva antes de vencer.
- Secreto `FIREBASE_SERVICE_ACCOUNT` solo en la Container App; nunca en el repositorio, en Terraform, en logs ni en respuestas. La cuenta de servicio solo tiene el rol de enviar mensajes de FCM.
- Los tokens de FCM son datos personales: no se devuelven, no se registran en logs y solo se usan para enviar.
- BOLA: un usuario solo ve y borra sus dispositivos; `installationId` ajeno → 404.
- Push sin contenido privado: ni el texto de los mensajes, ni direcciones, ni teléfonos.
- Rate limit: 30 registros por hora por usuario; Redis caído → 503.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Token de FCM con formato inválido | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| `installationId` ya usado por otra cuenta | 409 | Conflicto (`installation_conflict`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-20-R01 | Registro de dispositivos | PUT idempotente por instalación, máximo 5, un token por cuenta, DELETE y lista sin tokens; limpieza al logout, suspensión y eliminación. | F1 | T01 |
| NV-20-R02 | Envío push de la bandeja | Los 16 tipos se envían a los dispositivos del destinatario con título, cuerpo y datos de destino; nunca al actor; agrupados por conversación. | F2 | T02 |
| NV-20-R03 | Cliente de FCM | OAuth 2.0 con la cuenta de servicio, renovación del token de acceso, API HTTP v1, borrado de tokens inválidos y un reintento; un fallo nunca rompe la acción principal. | F2 | T03 |
| NV-20-R04 | Privacidad, seguridad y límites | Sin contenido privado en push, sin tokens en logs ni respuestas, BOLA, rate limit, secreto solo en la Container App. | F3 | T04 |
| NV-20-R05 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; secreto cargado por Victor; desplegado por el CD; prueba real en Azure con un teléfono: solicitud, mensaje y visita llegan como push. | F4 | T05 |
| NV-20-R06 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T06, T07, T08 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Proveedor | Firebase Cloud Messaging, API HTTP v1, del proyecto de Firebase que ya usa Mobile; reemplaza la parte «sin push» de M-BE-071 | APROBADO (M-BE-078) |
| Credencial | Cuenta de servicio con permiso solo de envío, como secreto `FIREBASE_SERVICE_ACCOUNT` en la Container App (fuera de Terraform) | APROBADO (M-BE-078) |
| Paquetes | Sin `FirebaseAdmin`: JWT RS256 y HTTP con lo que trae .NET | APROBADO (M-BE-078) |
| Entrega | Mejor esfuerzo después de guardar, cola en memoria; la bandeja es la fuente de verdad | APROBADO (M-BE-078) |
| Alcance | Los 16 tipos actuales; sin preferencias por categoría en el backend | APROBADO (M-BE-078) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Firebase Cloud Messaging | $0 | FCM no cobra por mensaje |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`push_devices`) |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-20-RK01 | Fuga del secreto de Firebase | Envío de push falsos | Solo en la Container App, permiso mínimo, rotación documentada |
| NV-20-RK02 | Push en la pantalla de bloqueo | Exponer conversaciones | Textos sin contenido privado |
| NV-20-RK03 | Réplica reiniciada antes de enviar | Push perdido | La bandeja lo conserva; aceptado en el piloto |
| NV-20-RK04 | Teléfono compartido | Push a la cuenta equivocada | Un token pertenece a una sola cuenta y el logout borra el dispositivo |

## 16. Dependencias

- Proyecto de Firebase y cuenta de servicio (Mobile y Victor).
- NV-06 (logout) y NV-21 (suspensión) para borrar dispositivos; NV-10 1.2.0 para la eliminación de cuenta.

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 6 |
| Fases del plan | 4 |
| Tasks | 8 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 4 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T04 completas; T05–T08 esperan CI, despliegue, secreto de Firebase y Azure** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
