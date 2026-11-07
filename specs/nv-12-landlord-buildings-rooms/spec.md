# NV-12 — Arrendador, edificios y cuartos

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-12-landlord-buildings-rooms/`) |
| Estado de ejecución | **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-06, RF-13, RF-14, RF-16 a RF-22, RF-45, RF-76, RF-77, RF-78 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-08, RNF-09, RNF-10 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Listings |
| Precondición | NV-10 (cuenta) y NV-11 (universidades con ubicación) desplegadas |
| Relación | Fuente de los cuartos que publicará NV-14; NV-13 (fotos y documentos) y NV-21 (verificación) completan la publicación; NV-16 (chat) y la confirmación de visitas (NV-17 v2) usan al dueño del cuarto |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-07):** se completa al nivel de NV-11, NV-15 y NV-17 antes de su aprobación: contrato para Mobile (§6.1); acciones como sub-recursos (`/rooms/generate`, `/rooms/{id}/pause`) igual que `/visits/{id}/cancel`, en lugar de `:accion`; códigos 403, 413, 415, 429 y 503; `code` en 409 (M-BE-061); concurrencia optimista con `version` en el cuerpo; servicios con el mismo catálogo cerrado de NV-11 (entrada de Smart Match); límites de edificios y cuartos. La publicación de v1 envía el edificio a revisión con datos completos y al menos un cuarto listo; el requisito de documentos se activa con NV-13 y la aprobación llega con NV-21. NV-12 no migra `properties`: la migración de favoritos y visitas se decide en NV-14. Los eventos de dominio salen de v1 hasta que exista un consumidor.

## 1. Contexto

Cualquier usuario puede publicar alojamientos desde su misma cuenta activando el modo arrendador. El propietario registra primero el edificio (datos compartidos, ubicación y servicios) y después genera los cuartos por piso, los configura y los envía a revisión cuando al menos uno está listo. Hoy solo existe la colección provisional `properties` de NV-07, sin dueño ni estados; por eso no se pueden confirmar visitas ni abrir chats con un arrendador.

## 2. Objetivo

Exponer el alta de arrendador y la gestión de edificios y cuartos propios con estados claros, sin publicar nada que no haya pasado revisión.

## 3. Precondiciones

1. NV-10 y NV-11 desplegadas (universidades con coordenadas).
2. Validación del contrato §6.1 con el responsable de Mobile (pantallas de modo arrendador).
3. Autorización «EJECUTA».

## 4. Alcance

1. Alta y consulta del perfil de arrendador (RF-06).
2. Crear, consultar, editar y borrar edificios propios (RF-13, RF-16, RF-17, RF-20, RF-21).
3. Generar cuartos por piso con datos base (RF-76, RF-77).
4. Editar, duplicar, copiar datos, pausar, reanudar y borrar cuartos (RF-14, RF-18 a RF-21, RF-45).
5. Lista de pasos y envío a revisión (RF-78).
6. Tablero Mis alojamientos con totales (RF-22).
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Fotos y documentos del inmueble: NV-13.
- Aprobación del edificio (Verificado) y moderación: NV-21.
- Búsqueda pública de cuartos y migración de `properties`, favoritos y visitas: NV-14.
- Estado Rentado y contratos: NV-18. Cobro de rentas (RES-03).
- Eventos de dominio (`BuildingPublicationRequested`, `RoomStatusChanged`…): se agregan cuando exista un consumidor (NV-14, NV-21).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/landlord/profile` | Autenticado | Activa el modo arrendador | 201, 400, 401, 409, 413, 415, 429, 503 |
| GET | `/api/v1/landlord/profile` | Autenticado | Perfil de arrendador propio | 200, 401, 404, 429, 503 |
| POST | `/api/v1/landlord/buildings` | Arrendador | Crea un edificio en `draft` | 201, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/landlord/buildings` | Arrendador | Mis alojamientos con totales (RF-22) | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/landlord/buildings/{id}` | Dueño | Detalle del edificio | 200, 400, 401, 403, 404, 429, 503 |
| PATCH | `/api/v1/landlord/buildings/{id}` | Dueño | Edita el edificio (con `version`) | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| DELETE | `/api/v1/landlord/buildings/{id}?version=N` | Dueño | Borra el edificio y sus cuartos | 204, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/buildings/{id}/rooms/generate` | Dueño | Crea cuartos por piso con datos base | 201, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/landlord/buildings/{id}/rooms` | Dueño | Cuartos del edificio (paginado) | 200, 400, 401, 403, 404, 429, 503 |
| PATCH | `/api/v1/landlord/rooms/{id}` | Dueño | Edita un cuarto (con `version`) | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| DELETE | `/api/v1/landlord/rooms/{id}?version=N` | Dueño | Borra un cuarto | 204, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/rooms/{id}/duplicate` | Dueño | Duplica el cuarto con el siguiente número del piso | 201, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/rooms/{id}/copy-from` | Dueño | Copia datos de otro cuarto del mismo edificio | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| POST | `/api/v1/landlord/rooms/{id}/pause` | Dueño | Pausa un cuarto (`available` → `paused`) | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/rooms/{id}/resume` | Dueño | Reanuda un cuarto (`paused` → `available`) | 200, 400, 401, 403, 404, 409, 429, 503 |
| GET | `/api/v1/landlord/buildings/{id}/publication` | Dueño | Pasos pendientes para enviar a revisión | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/landlord/buildings/{id}/publish` | Dueño | Envía el edificio a revisión (NV-21) | 202, 400, 401, 403, 404, 409, 429, 503 |

- Perfil de arrendador: `{ fullName (2-120), phone (E.164 de México: +52 y 10 dígitos), rentalMode (individual|agency), rfc (obligatorio y validado si es agency; 12 caracteres persona moral), acceptedTermsVersion, createdAt }`. `acceptedTermsVersion` debe ser la versión vigente configurada (400 si no).
- Edificio: `{ id, type, name, address: { street, number, neighborhood, city, state, postalCode }, location: { lat, lng }, nearestUniversity: { id, code, name }, distanceToUniversityKm, walkingMinutes, allowedTenants, services: [..], rules, status, roomsTotal, roomsAvailable, version, createdAt, updatedAt }`. `type`: `rooms_building`, `house_rooms`, `full_apartment`. `allowedTenants`: `women`, `men`, `any`. `services` (sin repetidos): `wifi`, `water`, `electricity`, `gas`, `hot_water`, `laundry`, `kitchen`, `furnished`, `parking`, `security`, `cleaning` (mismo catálogo que NV-11). `rules` hasta 2000 caracteres. `nearestUniversityId` debe existir en el catálogo.
- Cuarto: `{ id, buildingId, number, floor, price, currency: "MXN", sizeM2, type, bathroom, furniture: [..], availableFrom, minContract, status, version, createdAt, updatedAt }`. `price` entero de 500 a 50 000 MXN; `sizeM2` de 4 a 200; `type`: `single`, `double`, `shared`; `bathroom`: `private`, `shared`; `furniture` (sin repetidos): `bed`, `desk`, `chair`, `closet`, `dresser`, `tv`, `fridge`, `microwave`; `availableFrom` fecha `YYYY-MM-DD`; `minContract`: `four_months`, `six_months`, `annual`.
- `distanceToUniversityKm` (1 decimal) y `walkingMinutes` (a 5 km/h, redondeo hacia arriba) los calcula el servidor con haversine; nunca vienen del cliente.
- Estados del edificio: `draft`, `in_review`, `verified`. Estados del cuarto: `setup` (sin precio o datos obligatorios), `ready`, `in_review`, `available`, `paused`, `rented`. `status`, `version`, `ownerId` y los campos calculados no se aceptan en la entrada (400).
- Concurrencia optimista: `PATCH` exige `version` en el cuerpo y `DELETE` en la query; si no coincide → 409 `version_conflict`. Cada escritura incrementa `version`.
- `rooms/generate`: `{ floors (1-30), roomsPerFloor: [n por piso, 1-30], numbering: "floor_prefix" (101, 102, 201…), base: { price?, sizeM2?, type?, bathroom?, furniture?, availableFrom?, minContract? } }`; máximo 200 cuartos por edificio (409 `rooms_limit`); los números existentes no se duplican (409 `room_number_taken`).
- `publication` responde `{ canPublish, steps: [{ id, done }] }` con los pasos `building_data`, `location`, `services`, `room_ready` y, cuando exista NV-13, `documents`. `publish` responde 202 con el edificio en `in_review`; si falta algo, 409 `publication_incomplete` con los pasos pendientes en `detail`.
- Respuestas con `Cache-Control: no-store`. Los listados usan el sobre de paginación de NV-01 (edificios por `createdAt` desc; cuartos por `floor`, `number`).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/landlord/profile
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId`/`ownerId` del cliente)
HEADERS      Content-Type: application/json
REQUEST      { fullName, phone, rentalMode, rfc?, acceptedTermsVersion }
RESPONSE     201 perfil de arrendador
STATUS CODES 201, 400, 401, 409 code=landlord_exists, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/profile
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId`/`ownerId` del cliente)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 perfil (404 si aún no es arrendador)
STATUS CODES 200, 401, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings
METHOD       POST
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      Content-Type: application/json
REQUEST      { type, name, address, location, nearestUniversityId, allowedTenants, services, rules? }
RESPONSE     201 edificio (status draft, version 1)
STATUS CODES 201, 400, 401, 403, 404 universidad, 409 code=buildings_limit, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [edificio con roomsTotal y roomsAvailable], page, pageSize, totalItems, totalPages, summary: { buildings, rooms, available, occupancyPercent, monthlyIncome } }
STATUS CODES 200, 400, 401, 403, 429, 503
PAGINATION   Sí: sobre de NV-01; orden createdAt desc, id desc
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}
METHOD       GET | PATCH | DELETE
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene); ajeno = 404
HEADERS      PATCH: Content-Type: application/json
REQUEST      PATCH: campos a cambiar + version; DELETE: ?version=N
RESPONSE     200 edificio | 204
STATUS CODES 200, 204, 400, 401, 403, 404, 409 code=version_conflict|building_has_rented_rooms, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/rooms/generate
METHOD       POST
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      Content-Type: application/json
REQUEST      { floors, roomsPerFloor: [..], numbering: "floor_prefix", base: {..} }
RESPONSE     201 { items: [cuarto] }
STATUS CODES 201, 400, 401, 403, 404, 409 code=rooms_limit|room_number_taken|building_in_review, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/rooms?page=1&pageSize=50
METHOD       GET
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [cuarto], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   Sí: sobre de NV-01; orden floor asc, number asc
```

```text
ENDPOINT     /api/v1/landlord/rooms/{id}
METHOD       PATCH | DELETE
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene); ajeno = 404
HEADERS      PATCH: Content-Type: application/json
REQUEST      PATCH: campos a cambiar + version; DELETE: ?version=N
RESPONSE     200 cuarto | 204
STATUS CODES 200, 204, 400, 401, 403, 404, 409 code=version_conflict|room_rented|room_number_taken, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/rooms/{id}/duplicate | /copy-from | /pause | /resume
METHOD       POST
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene); ajeno = 404
HEADERS      copy-from: Content-Type: application/json
REQUEST      copy-from: { sourceRoomId, version }; las demás sin cuerpo
RESPONSE     201 cuarto nuevo (duplicate) | 200 cuarto
STATUS CODES 200, 201, 400, 401, 403, 404, 409 code=room_state|rooms_limit|version_conflict, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/publication
METHOD       GET
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 { canPublish, steps: [{ id, done }] }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/publish
METHOD       POST
AUTH         Bearer JWT + perfil de arrendador (403 `landlord_required` si no lo tiene)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     202 edificio en in_review (sus cuartos ready pasan a in_review)
STATUS CODES 202, 400, 401, 403, 404, 409 code=publication_incomplete|building_state, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Listings** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `landlord_profiles`, `buildings`, `rooms` |
| Usa (puertos de otros módulos) | Catalog `IUniversityQuery` (NV-08) para validar la universidad y calcular la distancia |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `landlord_profiles`: `userId` (único), `fullName`, `phone`, `rentalMode`, `rfc`, `acceptedTermsVersion`, `acceptedTermsAt`, `createdAt`.
- `buildings`: `ownerId`, datos de la §6, `location` GeoJSON con índice `2dsphere`, `distanceToUniversityKm`, `walkingMinutes`, `status`, `version`, `createdAt`, `updatedAt`; índice `{ ownerId, createdAt: -1 }`.
- `rooms`: `buildingId`, `ownerId`, datos del cuarto, `status`, `version`; índice único `{ buildingId, number }` e índices `{ ownerId, status }` y `{ status, price }`. Los servicios se leen del edificio (no se copian).

## 9. Reglas de negocio

1. Todo endpoint `/landlord/*` salvo el perfil exige un perfil de arrendador (403 `landlord_required`) y filtra por `ownerId = sub`; un edificio o cuarto ajeno responde 404.
2. Edificio: `draft` → `in_review` (publish) → `verified` (NV-21); NV-21 también puede regresarlo a `draft` con correcciones. Editar dirección, ubicación o tipo de un edificio `verified` lo regresa a `in_review`.
3. Cuarto: `setup` → `ready` (cuando tiene precio, tamaño, tipo, baño, fecha y contrato mínimo) → `in_review` (al publicar el edificio) → `available` (NV-21) ⇄ `paused`; `available` → `rented` (NV-18). Solo cuartos `available` de edificios `verified` serán públicos (NV-14).
4. Transiciones no listadas → 409 `room_state` o `building_state`. No se genera ni se borra en un edificio `in_review` (409 `building_in_review`).
5. No se borra un cuarto `rented` ni un edificio con cuartos `rented` (409).
6. Límites: 20 edificios por arrendador, 200 cuartos por edificio, 30 pisos, 30 cuartos por piso.
7. Distancia y minutos a pie se recalculan al cambiar la ubicación o la universidad.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Ningún endpoint acepta `ownerId`, `userId`, `status`, `version` calculada ni campos calculados (asignación masiva → 400).
- Dirección exacta, teléfono, RFC y nombre completo solo salen en endpoints del dueño; nunca en logs (RNF-02).
- Validación estricta de RFC (persona moral, 12 caracteres con dígito verificador) y teléfono (E.164 de México).
- Rate limit en Redis: 60 lecturas y 30 escrituras por minuto por usuario; `rooms/generate` 10 por hora; Redis caído → 503.
- Pruebas con dos arrendadores y un usuario sin perfil: ninguno lee ni modifica lo del otro (404) y el usuario sin perfil recibe 403.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Usuario sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Conflicto de versión, estado, límite o número de cuarto | 409 | Conflicto |
| Cuerpo mayor a 8 KB | 413 | Solicitud demasiado grande |
| Contenido distinto de `application/json` | 415 | Tipo de contenido no admitido |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-12-R01 | Alta de arrendador | `POST /landlord/profile` activa el modo una sola vez (409 `landlord_exists`); valida teléfono, RFC si es `agency` y la versión de términos vigente; `GET` devuelve el perfil o 404. | F1 | T01 |
| NV-12-R02 | Gestión de edificios | Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo, catálogo de servicios y universidad existente; límite de 20; borrado bloqueado con cuartos `rented`. | F1 | T02 |
| NV-12-R03 | Generación de cuartos | `rooms/generate` crea cuartos numerados por piso (101, 102, 201…) con datos base; respeta 200 por edificio y no duplica números. | F1 | T03 |
| NV-12-R04 | Edición y acciones de cuartos | Editar, duplicar (siguiente número del piso), copiar datos de otro cuarto del mismo edificio, pausar, reanudar y borrar respetan estados y versiones. | F1 | T04 |
| NV-12-R05 | Máquinas de estados | Transiciones válidas de edificio y cuarto en el dominio, `setup` → `ready` automático al completar datos; transiciones inválidas 409 con `code`; prueba de cada transición. | F1 | T05 |
| NV-12-R06 | Cálculo de distancia | Distancia (1 decimal) y minutos a pie (5 km/h) a la universidad elegida, recalculados al cambiar ubicación o universidad; pruebas con coordenadas del seed. | F2 | T06 |
| NV-12-R07 | Persistencia y concurrencia | Índices únicos y geoespacial; `version` incrementada en cada escritura; dos ediciones simultáneas con la misma versión: una gana y la otra recibe 409. | F2 | T07 |
| NV-12-R08 | Publicación y tablero | `publication` lista pasos; `publish` pasa a `in_review` solo si se cumplen (409 `publication_incomplete` si no); el tablero devuelve edificios, cuartos, disponibles, ocupación e ingreso mensual de cuartos `rented`. | F2 | T08 |
| NV-12-R09 | Autorización y límites | Pruebas con dos arrendadores y un usuario sin perfil (404 ajeno, 403 sin perfil); asignación masiva 400; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`; teléfono, RFC y dirección fuera de logs. | F3 | T09 |
| NV-12-R10 | Contrato para Mobile y despliegue | Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (alta, edificio, generar, editar, pausar, publicar, ajeno 404). | F4 | T10 |
| NV-12-R11 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T11, T12, T13 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Rutas de acciones | Sub-recursos (`/rooms/generate`, `/rooms/{id}/pause`, `/buildings/{id}/publish`) en lugar de `:accion` | APROBADO (M-BE-064) |
| Rol de arrendador | Existencia del perfil en `landlord_profiles`, consultada en cada solicitud (sin claim nuevo en el JWT) | APROBADO (M-BE-064) |
| Concurrencia | `version` obligatoria en el cuerpo de `PATCH` y en la query de `DELETE` | APROBADO (M-BE-064) |
| Publicación en v1 | Envía a `in_review` con datos completos y un cuarto `ready`; documentos con NV-13; aprobación con NV-21 | APROBADO (M-BE-064) |
| Migración de NV-07 | NV-12 no toca `properties`, favoritos ni visitas; la migración se decide en NV-14 | APROBADO (M-BE-064) |
| Catálogos | Servicios iguales a NV-11; tipos, baños, muebles y contrato mínimo de la §6, a confirmar con Mobile | APROBADO (M-BE-064) |
| Límites | 20 edificios, 200 cuartos, 30 pisos y 30 cuartos por piso | APROBADO (M-BE-064) |
| Términos del arrendador | Versión vigente configurable (`LandlordTerms:Version`); el texto lo publica la app | APROBADO (M-BE-064) |
| Eventos de dominio | Fuera de v1 hasta que exista un consumidor | APROBADO (M-BE-064) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Tres colecciones pequeñas y un índice `2dsphere`; un comando Redis por solicitud |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-12-RK01 | Exponer dirección exacta o teléfono | Privacidad y seguridad del propietario | Proyecciones solo para el dueño, prueba de logs y NV-14 con ubicación aproximada |
| NV-12-RK02 | Estados inconsistentes | Cuartos visibles sin verificar | Máquinas de estados en el dominio y pruebas de cada transición |
| NV-12-RK03 | Generación masiva abusiva | Saturación de la base | Límites de pisos y cuartos y rate limit de `generate` |
| NV-12-RK04 | Ediciones simultáneas desde dos dispositivos | Datos perdidos | Concurrencia optimista con `version` |
| NV-12-RK05 | Expectativa de publicar sin NV-13/NV-21 | Edificios sin llegar a la búsqueda | La app muestra `in_review` como "En revisión"; NV-13 y NV-21 siguen en el orden acordado |

## 16. Dependencias

- NV-10 y NV-11 (universidades con coordenadas).
- NV-13 (fotos y documentos) para el paso `documents`.
- NV-21 (revisión) para `verified` y `available`.
- NV-14 (búsqueda pública y migración de `properties`).
- NV-18 (`rented`).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 11 |
| Fases del plan | 4 |
| Tasks | 13 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
