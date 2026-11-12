# NV-18 — Cierre de trato con QR y renta

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-12 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-18-deal-closing-rental/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-81, RF-82, RF-83, RF-84; RES-03 y RES-04 (sin pagos ni contrato formal) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09, RNF-17, RNF-18 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Rentals |
| Precondición | NV-12, NV-14, NV-16 y NV-17 1.3.0 desplegadas |
| Relación | Cierra el flujo de NV-16 (solicitud aceptada) y NV-17 (visita); marca el cuarto `rented` (NV-12); habilita reseñas (NV-19) y notificaciones (NV-20) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-12):** se adapta a lo ya desplegado. El convenio nace de una **solicitud aceptada** de NV-16 (`requestId`), así que el arrendador nunca escribe ni ve el id del estudiante; el cuarto es el de NV-12/NV-14 (`available` → `rented` → `available`). Crear el convenio ya entrega el QR y el código de respaldo, como en el prototipo (una sola pantalla «Generar QR»). El código vive dentro del convenio (sin colección `closing_codes`) y aceptar es un cambio condicional del convenio más el cambio de estado del cuarto con compensación, como NV-16, en lugar de una transacción multi-documento. Las rutas usan subrutas (`/code`, `/accept`) como NV-16 y NV-17. Mi renta da el contacto por el chat de NV-16 (`conversationId`), sin teléfono. Al rentarse el cuarto, las demás solicitudes se rechazan con `not_available` y las visitas activas de otros estudiantes se cancelan.

## 1. Contexto

El prototipo ya tiene las pantallas «Cerrar trato (QR)», «Escanear QR de renta» y «Mi renta», pero hoy solo funcionan en un dispositivo porque no hay backend. El arrendador elige con quién cierra (un estudiante con solicitud aceptada en NV-16), captura renta, depósito, inicio, día de pago y duración, y genera un QR con un código de respaldo que vence en 10 minutos. El estudiante lo escanea o escribe el código, revisa el convenio y las reglas de la casa, y lo acepta; en ese momento el cuarto pasa a Rentado para ambos (RF-82) y deja de aparecer en la búsqueda (NV-14). TheHouse no cobra rentas ni firma contratos (RES-03, RES-04): solo registra el convenio.

## 2. Objetivo

Registrar en el servidor el convenio entre arrendador y estudiante, confirmado con un código de un solo uso, que marque el cuarto como rentado, y dar al estudiante su vista de Mi renta y al arrendador el fin de renta.

## 3. Precondiciones

1. NV-12, NV-14, NV-16 y NV-17 1.3.0 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. El arrendador crea el convenio desde una solicitud aceptada de NV-16 y recibe el QR y el código de respaldo (10 minutos).
2. El arrendador vuelve a generar el código si venció, consulta sus convenios (para ver cuándo el estudiante aceptó) y cancela uno pendiente.
3. El estudiante revisa el convenio con el código o el QR y lo acepta; el cuarto pasa a `rented`.
4. Al aceptar: las demás solicitudes activas del cuarto se rechazan (`not_available`) y las visitas activas de otros estudiantes a ese cuarto se cancelan.
5. Mi renta del estudiante: convenio, próximo pago calculado, dirección exacta, ubicación, reglas de la casa y chat con el arrendador.
6. Fin de renta por el arrendador: el cuarto vuelve a `available` y el convenio queda como historial.
7. Puerto `IRentalHistoryQuery` (convenios `active` o `ended` por estudiante y cuarto) para la elegibilidad de reseñas de NV-19.
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Pagos, cobranza, depósitos o recordatorios de pago dentro de la app (RES-03); el botón «Pagar» del prototipo queda fuera.
- Firma electrónica o contrato formal (RES-04).
- Teléfono o correo del arrendador en Mi renta: el contacto es el chat de NV-16.
- Editar los términos de un convenio: se cancela el pendiente y se crea otro.
- Fin de renta pedido por el estudiante o automático al cumplirse la duración.
- Notificaciones push o por correo: NV-20. Reseñas: NV-19. Reportar un problema: NV-21.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/landlord/agreements` | Arrendador (dueño del cuarto de la solicitud) | Crea el convenio `pending` desde `requestId` y entrega el código | 201, 400, 401, 403, 404, 409, 429, 503 |
| GET | `/api/v1/landlord/agreements` | Arrendador | Mis convenios; filtros `status` y `roomId` | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/landlord/agreements/{id}` | Arrendador (dueño) | Detalle; sirve para saber si el estudiante ya aceptó | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/landlord/agreements/{id}/code` | Arrendador (dueño) | Genera un código nuevo e invalida el anterior | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/agreements/{id}/cancel` | Arrendador (dueño) | Cancela un convenio `pending` | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/agreements/{id}/end` | Arrendador (dueño) | Termina una renta `active`; el cuarto vuelve a `available` | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/agreements/preview` | Autenticado (estudiante del convenio) | Revisa el convenio con `{ code }` o `{ qrToken }` sin consumirlo | 200, 400, 401, 404, 410, 429, 503 |
| POST | `/api/v1/agreements/accept` | Autenticado (estudiante del convenio) | Acepta con `{ code \| qrToken, acceptedTerms: true }`; el cuarto pasa a `rented` | 200, 400, 401, 404, 409, 410, 429, 503 |
| GET | `/api/v1/users/me/rental` | Autenticado (estudiante) | Mi renta activa | 200, 401, 404, 429, 503 |

- Crear: `{ requestId, monthlyRent, deposit, startDate, durationMonths, paymentDay }`. `requestId` es una solicitud `accepted` de NV-16 a un cuarto del arrendador; el estudiante y el cuarto salen de ella. `monthlyRent` entero MXN 500–100000 (la app lo prellena con el precio del cuarto); `deposit` entero MXN 0–2 × `monthlyRent` (prototipo: sin depósito o 1 mes); `startDate` fecha `YYYY-MM-DD` entre hoy − 30 días y hoy + 90 días (`America/Mexico_City`); `durationMonths` 1–24 (prototipo: 4, 6 o 12); `paymentDay` 1–28. `endDate` = `startDate` + `durationMonths` lo calcula el servidor.
- Convenio del arrendador: `{ id, room: { id, buildingId, title, city }, student: { name, university }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, status: "pending" | "active" | "ended" | "cancelled", closingCode: { qrToken, backupCode, expiresAt } | null, createdAt, updatedAt, activatedAt, endedAt, cancelledAt }`. `closingCode` solo aparece en la respuesta de crear y de `/code` (el código en claro no se guarda); en el detalle y el listado es `null`. Nunca incluye teléfono, correo, apellidos ni ids de usuario del estudiante.
- `qrToken`: 32 bytes aleatorios en base64url, opaco, sin datos personales; la app dibuja el QR con ese texto. `backupCode`: 8 caracteres de `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (sin I, O, 0 ni 1); la app puede mostrarlo como `XXXX-XXXX` y el servidor ignora guiones, espacios y mayúsculas/minúsculas. Ambos vencen a los 10 minutos.
- Vista previa del estudiante: `{ id, room: { id, buildingId, title, city }, building: { name, rules }, landlord: { name }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, codeExpiresAt }`. `landlord.name` es solo el nombre, sin apellidos, como en NV-17. No consume el código.
- Aceptar: `{ code }` o `{ qrToken }` (exactamente uno) y `acceptedTerms: true` (la casilla «Acepto el convenio y las reglas de la casa»); responde la renta de `/users/me/rental`.
- Mi renta: `{ agreementId, room: { id, buildingId, title, city }, building: { name, rules }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, nextPayment: { dueDate, amount } | null, address: { street, number, neighborhood, city, state, postalCode }, location: { lat, lng }, landlord: { name }, conversationId, activatedAt }`. `nextPayment.dueDate` es la siguiente fecha con día `paymentDay` desde hoy (y no antes de `startDate`) mientras no pase `endDate`; `amount` = `monthlyRent`. Solo informativo: no se registra si se pagó. Sin renta activa → 404 `no_active_rental`.
- Fin de renta: `{ reason?: "contract_finished" | "early_termination" | "other", note? ≤ 300 }`; responde el convenio del arrendador con `status: ended`.
- Códigos nuevos: 409 `request_state` (la solicitud no está aceptada), 409 `room_unavailable` (cuarto no publicado), 409 `agreement_open` (el cuarto ya tiene un convenio pendiente o activo), 409 `agreement_state` (transición inválida), 404 `code_invalid` (código inexistente o de otro estudiante), 410 `code_expired`, 410 `code_used`, 429 `too_many_attempts`, 404 `no_active_rental`, 403 `landlord_required`.
- Todas las respuestas con `no-store`.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/landlord/agreements
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { requestId, monthlyRent, deposit, startDate: "2026-12-01", durationMonths, paymentDay }
RESPONSE     201 convenio del arrendador con closingCode { qrToken, backupCode, expiresAt }
STATUS CODES 201, 400, 401, 403 code=landlord_required, 404 solicitud, 409 code=request_state | room_unavailable | agreement_open, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/agreements?status=pending&roomId=..&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      status (pending|active|ended|cancelled), roomId, page, pageSize
RESPONSE     200 { items: [{ id, room: { id, buildingId, title, city }, student: { name, university }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, status: "pending" | "active" | "ended" | "cancelled", closingCode: { qrToken, backupCode, expiresAt } | null, createdAt, updatedAt, activatedAt, endedAt, cancelledAt }], page, pageSize, totalItems, totalPages } (closingCode = null)
STATUS CODES 200, 400, 401, 403, 429, 503
PAGINATION   Sí: orden createdAt desc, id desc
```

```text
ENDPOINT     /api/v1/landlord/agreements/{id}
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 convenio del arrendador (closingCode = null); la pantalla «Esperando que lo escanee…» consulta este endpoint hasta ver status = active
STATUS CODES 200, 400, 401, 403, 404 ajeno, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/agreements/{id}/code
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 convenio del arrendador con closingCode nuevo
STATUS CODES 200, 400, 401, 403, 404, 409 code=agreement_state, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/agreements/{id}/cancel
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 convenio del arrendador (cancelled)
STATUS CODES 200, 400, 401, 403, 404, 409 code=agreement_state, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/agreements/{id}/end
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { reason?: contract_finished|early_termination|other, note? }
RESPONSE     200 convenio del arrendador (ended)
STATUS CODES 200, 400, 401, 403, 404, 409 code=agreement_state, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/agreements/preview
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { code: "K7PM-2QXA" } o { qrToken }
RESPONSE     200 { id, room: { id, buildingId, title, city }, building: { name, rules }, landlord: { name }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, codeExpiresAt }
STATUS CODES 200, 400, 401, 404 code=code_invalid, 410 code=code_expired | code_used, 429 code=too_many_attempts, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/agreements/accept
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { code | qrToken, acceptedTerms: true }
RESPONSE     200 Mi renta
STATUS CODES 200, 400, 401, 404 code=code_invalid, 409 code=agreement_state | request_state | room_unavailable, 410 code=code_expired | code_used, 429 code=too_many_attempts, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/rental
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { agreementId, room: { id, buildingId, title, city }, building: { name, rules }, terms: { monthlyRent, deposit, startDate, durationMonths, endDate, paymentDay, currency: "MXN" }, nextPayment: { dueDate, amount } | null, address: { street, number, neighborhood, city, state, postalCode }, location: { lat, lng }, landlord: { name }, conversationId, activatedAt }
STATUS CODES 200, 401, 404 code=no_active_rental, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Rentals** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `agreements` |
| Usa (puertos de otros módulos) | Messaging `IRentalRequestRepository` (NV-16) para la solicitud aceptada, el chat y rechazar las demás del cuarto; Listings `IRoomRepository`, `IBuildingRepository` e `ILandlordProfileRepository` (NV-12) para el cuarto, su `version`, el edificio y el arrendador; Visits `IVisitRepository` (NV-17) para cancelar las visitas activas del cuarto; Profile `IProfileRepository` (NV-11) para el nombre y la universidad del estudiante |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `agreements` (colección nueva del módulo Rentals): `requestId`, `roomId`, `buildingId`, `ownerId`, `studentId`, `conversationId`, términos (`monthlyRent`, `deposit`, `startDate`, `durationMonths`, `endDate`, `paymentDay`), `status` (`pending`, `active`, `ended`, `cancelled`), `code` embebido `{ codeHash, qrTokenHash, expiresAt, usedAt, failedAttempts }` (solo hash, nunca el código en claro), `endReason`, `endNote`, `history[]` con actor, `createdAt`, `updatedAt`, `activatedAt`, `endedAt`, `cancelledAt`.
- Índices: único parcial `{ roomId }` para convenios `pending` o `active` (un convenio abierto por cuarto); único parcial `{ code.codeHash }` y `{ code.qrTokenHash }` mientras están sin usar; `{ ownerId, createdAt }` para el listado; `{ studentId, status }` para Mi renta.
- Sin migraciones: los cuartos ya tienen el estado `rented` (NV-12) y las solicitudes y visitas solo cambian de estado con sus reglas actuales.
- Dirección, ubicación, nombre del edificio y reglas se leen del edificio (NV-12) al responder; no se copian al convenio. Los términos sí se guardan en el convenio y no cambian si después cambia el precio del cuarto.

## 9. Reglas de negocio

1. Crear: la solicitud es del arrendador y está `accepted`; el cuarto está `available` y su edificio `verified`; el cuarto no tiene otro convenio `pending` o `active` → 409 `agreement_open`.
2. Código: uno vigente por convenio; crear o pedir `/code` genera uno nuevo e invalida el anterior; vence a los 10 minutos; es de un solo uso. Solo se puede pedir sobre un convenio `pending`.
3. Revisar y aceptar: solo el estudiante del convenio. Un código válido usado por otra cuenta responde 404 `code_invalid` y suma un fallo al convenio; con 5 fallos el código se invalida. Además, 5 códigos fallidos de una misma cuenta en 15 minutos → 429 `too_many_attempts` durante 15 minutos.
4. Aceptar (bajo un candado de Redis por cuarto): se vuelve a verificar que la solicitud sigue `accepted` y el cuarto `available`; el convenio pasa de `pending` a `active` y consume el código en un solo cambio condicional; después el cuarto pasa a `rented` con su `version`. Si el cuarto ya cambió, el convenio se regresa a `pending` con el código invalidado y responde 409 `room_unavailable`. Dos aceptaciones simultáneas: solo una gana (RNF-18).
5. Después de activar: las solicitudes `pending` o `accepted` de otros estudiantes a ese cuarto pasan a `rejected` con motivo `not_available`, y sus visitas activas a ese cuarto se cancelan por el arrendador con motivo `not_available`; si alguno de estos pasos falla, se registra y se reintenta sin deshacer la renta (es idempotente).
6. Mi renta: el convenio `active` del estudiante; la dirección exacta y la ubicación solo salen mientras está `active` (RNF-02).
7. Fin de renta: solo el arrendador y solo sobre `active`; el convenio pasa a `ended` y el cuarto de `rented` a `available` con su `version`. Cancelar: solo sobre `pending` e invalida el código vigente.
8. Mientras el cuarto está `rented` no admite pausar, reanudar ni borrar (reglas de NV-12 sin cambio) y no aparece en la búsqueda (NV-14).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: el arrendador solo ve y cambia convenios de sus cuartos y el estudiante solo revisa y acepta los suyos; lo ajeno responde 404.
- El código y el token del QR se guardan solo como SHA-256; la comparación es por hash con índice y en tiempo constante cuando se compara en memoria; nunca aparecen en logs.
- Fuerza bruta: rate limit de `preview` y `accept` 10/min por usuario y 30/min por IP; bloqueo por cuenta tras 5 códigos fallidos en 15 minutos y del código tras 5 fallos; con 32^8 combinaciones y 10 minutos de vida, adivinarlo no es práctico (RNF-17).
- El arrendador ve el nombre del perfil y la universidad del estudiante, nunca su correo, teléfono ni id; el estudiante ve solo el nombre del arrendador.
- Dirección exacta solo en Mi renta con convenio `active`; nunca en la vista previa ni en vistas del arrendador.
- Entradas con campos desconocidos → 400 (sin `studentId`, `roomId` ni `status` del cliente); rate limit 60/min por usuario en el resto; Redis caído → 503.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Código inexistente o de otra cuenta; sin renta activa | 404 | Recurso no encontrado (`code`: `code_invalid`, `no_active_rental`) |
| Solicitud no aceptada, cuarto no publicado, convenio abierto o transición inválida | 409 | Conflicto (`code`: `request_state`, `room_unavailable`, `agreement_open`, `agreement_state`) |
| Código vencido o ya usado | 410 | Recurso no disponible (`code`: `code_expired`, `code_used`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Demasiados códigos fallidos | 429 | Demasiadas solicitudes (`code`: `too_many_attempts`) |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-18-R01 | Convenio desde una solicitud aceptada | El arrendador crea el convenio solo con solicitud `accepted` de su cuarto, cuarto `available` y edificio `verified`; valida montos, fechas, duración y día de pago; calcula `endDate`; 409 `request_state`, `room_unavailable` y `agreement_open`; lo ajeno 404; sin modo arrendador 403. | F1 | T01 |
| NV-18-R02 | Código de cierre seguro | Crear y `/code` entregan `qrToken` y `backupCode` que vencen a los 10 minutos; solo se guarda su hash; un código nuevo invalida el anterior; el código se acepta sin guiones ni distinción de mayúsculas. | F1 | T02 |
| NV-18-R03 | Revisar y aceptar | `preview` devuelve los términos y las reglas sin consumir el código; `accept` exige `acceptedTerms: true`, activa el convenio, consume el código y marca el cuarto `rented`; 410 vencido o usado; 404 de otra cuenta; prueba de concurrencia con dos aceptaciones y con un cambio del cuarto a mitad. | F2 | T03 |
| NV-18-R04 | Efectos al rentar | Las demás solicitudes activas del cuarto quedan `rejected` con `not_available` y las visitas activas de otros estudiantes a ese cuarto quedan canceladas por el arrendador; el cuarto deja de salir en la búsqueda; los pasos se pueden repetir sin duplicar efectos. | F2 | T04 |
| NV-18-R05 | Mi renta | Devuelve el convenio `active` con próximo pago calculado, dirección exacta, ubicación, reglas y `conversationId`; sin renta activa 404 `no_active_rental`; tras terminar la renta ya no sale la dirección. | F2 | T05 |
| NV-18-R06 | Convenios del arrendador, cancelar y fin de renta | Listado y detalle por estado y cuarto sin el código; cancelar solo `pending`; terminar solo `active` con motivo opcional, el cuarto vuelve a `available`; historial conservado; puerto de historial de rentas para NV-19. | F2 | T06 |
| NV-18-R07 | Protección contra fuerza bruta y privacidad | Rate limits y bloqueos por cuenta y por código verificados; hashes sin código en claro en base de datos ni logs; vistas sin datos de contacto; Redis caído 503. | F3 | T07 |
| NV-18-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (crear, revisar con código, aceptar, Mi renta, cuarto fuera de la búsqueda, terminar y cuarto de nuevo disponible). | F4 | T08 |
| NV-18-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Origen del convenio | Desde `requestId` de una solicitud aceptada de NV-16; el estudiante y el cuarto salen de ella | APROBADO (M-BE-070) |
| Atomicidad | Cambio condicional del convenio + estado del cuarto con `version` y compensación, bajo candado de Redis por cuarto (patrón de NV-16, M-BE-068); sin transacción multi-documento. Alternativa: transacción de MongoDB (Atlas M0 la soporta) con una unidad de trabajo nueva | APROBADO (M-BE-070) |
| Código | Embebido en el convenio (sin colección `closing_codes`); crear ya entrega el QR, como el prototipo | APROBADO (M-BE-070) |
| Contacto en Mi renta | Solo el chat de NV-16 (`conversationId`); sin teléfono ni correo (cambia la propuesta de 1.0.0) | APROBADO (M-BE-070) |
| Otras solicitudes y visitas del cuarto | Se rechazan con `not_available` y se cancelan al rentarse; sin enum nuevo para Mobile | APROBADO (M-BE-070) |
| Eventos | Sin bus de eventos todavía: el caso de uso llama a los puertos de Listings, Messaging y Visits; NV-20 agregará `AgreementActivated` y `AgreementEnded` | APROBADO (M-BE-070) |
| Fin de renta | Solo el arrendador, sin fin automático por fecha; el estudiante ve `endDate` | APROBADO (M-BE-070) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva (`agreements`) con pocos documentos e índices; un candado y contadores en Redis |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-18-RK01 | Adivinar el código de respaldo | Renta suplantada | 8 caracteres de 32 símbolos, 10 minutos, bloqueo por cuenta y por código y rate limit |
| NV-18-RK02 | Doble aceptación o cuarto que cambia a mitad | Dos rentas o cuarto inconsistente | Candado por cuarto, cambio condicional, `version` del cuarto, compensación e índice único parcial; pruebas de concurrencia |
| NV-18-RK03 | Dirección expuesta sin renta | Seguridad del arrendador | Solo en Mi renta con convenio `active`; prueba que lo verifica |
| NV-18-RK04 | Falla al cerrar otras solicitudes o visitas | Estudiantes esperando un cuarto rentado | Pasos idempotentes con reintento y registro; la búsqueda ya no muestra el cuarto |
| NV-18-RK05 | Arrendador que no termina la renta | Cuarto rentado de más | El arrendador ve `endDate`; NV-20 podrá recordarlo |

## 16. Dependencias

- NV-12 (cuarto y estado `rented`), NV-14 (búsqueda), NV-16 (solicitud aceptada y chat) y NV-17 1.3.0 (visitas).
- NV-19 (reseñas tras renta) y NV-20 (notificaciones).

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
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T11 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
