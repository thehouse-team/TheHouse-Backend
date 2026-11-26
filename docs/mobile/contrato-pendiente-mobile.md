# Contrato pendiente para Mobile — rutas del backend que la app todavía no consume

| Campo | Valor |
| --- | --- |
| Fecha | 2026-11-26 |
| Backend | `main` en `3d7bd47` (133 operaciones en OpenAPI) |
| Mobile revisado | `main` en `8d2f1a3` (81 anotaciones Retrofit = 80 rutas del backend) |
| Método | Lista de rutas del OpenAPI del backend comparada con las anotaciones `@GET/@POST/@PUT/@PATCH/@DELETE` de `app/src/main` |
| Fuente de verdad | La sección «6.1 Contrato para Mobile» de cada spec del backend (copiada abajo sin cambios) |

Mobile ya consume bien: cuenta y sesión, perfil y preferencias, universidades, catálogo de ejemplo (`/properties`), favoritos, solicitudes y chat, visitas del estudiante, convenios y mi renta, notificaciones (lista y lectura), arrendador (perfil, edificios, cuartos, fotos, documentos, publicación, solicitudes, convenios y estadísticas por cuarto), reportes y Smart Match (puntajes, recomendaciones, explicaciones y descartes).

## Resumen: lo que falta, por prioridad

| Prioridad | Función | Rutas que faltan | Por qué importa | Contrato |
| --- | --- | --- | --- | --- |
| **P1** | **Cuartos reales: búsqueda, mapa, conteo y detalle** | `GET /rooms/search`, `GET /rooms/map`, `GET /rooms/count`, `GET /rooms/{id}`, `GET /places/search` | La app busca en `/properties` (las 12 propiedades de ejemplo): los cuartos que publican los arrendadores **no aparecen** para los estudiantes | §1 |
| **P1** | **Registro del dispositivo para push** | `PUT /users/me/devices/{installationId}`, `DELETE …/{installationId}`, `GET /users/me/devices` | La app obtiene el token de Firebase pero nunca lo manda: **ninguna notificación push puede llegar** (bloquea NV-20 T05) | §2 |
| **P1** | **Eliminar la cuenta desde la app** | `GET /users/me/deletion`, `POST /users/me/deletion` | Requisito de Google Play (borrado dentro de la app) y del punto de privacidad | §3 |
| **P1** | **Aviso legal y aceptación de versiones** | `GET /legal`, `PUT /users/me/legal` | URLs de privacidad, eliminación y términos, y la aceptación cuando cambia una versión | §3 |
| P2 | Reseñas | `GET /rooms/{roomId}/reviews/eligibility`, `POST /rooms/{roomId}/reviews`, `GET /rooms/{roomId}/reviews`, `PATCH /reviews/{id}`, `GET /users/me/reviews` | Spec de Mobile NV-13 en 0/7 | §4 |
| P2 | Visitas del lado del arrendador | `GET /landlord/visits`, `GET /landlord/visits/{id}`, `POST …/confirm`, `POST …/reschedule`, `POST …/cancel` | El estudiante agenda visitas, pero el arrendador no puede confirmarlas desde la app | §5 |
| P2 | Estadísticas del arrendador (resumen y recomendaciones) | `GET /landlord/stats`, `GET /landlord/stats/recommendations` | Ya usan `/landlord/stats/rooms`; faltan el resumen y los consejos | §6 |
| P2 | Compañeros de cuarto | `GET/PUT/DELETE /users/me/roommate-profile`, `GET /roommates`, `GET /roommates/{id}`, `POST /roommates/{id}/contact` | Función completa sin pantalla | §7 |
| P2 | Smart Match: «¿te sirvió?» | `POST /smart-match/feedback` | Retroalimentación para evaluar el modelo de ML | §8 |
| P3 | Fotos y documentos privados | `GET /media/{id}/private` | El arrendador ve sus documentos subidos (`GET /media/{id}` público ya se usa a través de `thumbnailUrl`) | §9 |
| P3 | Buscar universidad por texto o cercanía | `GET /universities/search` | La app lista todas; la búsqueda ayuda en el registro | §10 |
| — | Panel de staff (moderación) | `/admin/*` (17 rutas) | Solo si Mobile hará pantallas para el rol `admin`/`reviewer`; si no, se usan con la API | §11 |
| — | Salud | `/health/live`, `/health/ready` | Solo para Azure; la app no los usa | — |

## Cambios que vienen con P1 (importante)

1. **Ids de cuarto en lugar de ids de propiedad.** Al pasar a `/rooms`, todo lo que hoy usa `propertyId` debe usar `roomId`:
   - **Favoritos** (`PUT/DELETE /users/me/favorites/{roomId}`): el backend acepta todavía los ids de las 12 propiedades de ejemplo (transición de NV-15), pero los cuartos reales **solo** se pueden marcar con su `roomId`. `GET /users/me/favorites/ids` responde `{ roomIds, propertyIds }`.
   - **Visitas** (`POST /visits`) y **solicitudes** (`POST /rooms/{roomId}/requests`, que ya usa `roomId`): usar el `id` que devuelve `/rooms/search`.
   - `/properties` sigue funcionando mientras Mobile migra; se retira en una versión futura cuando Mobile confirme.
2. **Detalle con sesión:** `GET /rooms/{id}` exige token (RF-24); la búsqueda, el mapa y el conteo son públicos.
3. **Ubicación aproximada:** nunca se publica la dirección exacta; `approxLocation` es el centro de una celda de 0.003° (unos 330 m).
4. **Push:** `PUT` después del login, al abrir la app y cuando Firebase rota el token (`onNewToken`); `DELETE` antes del logout. El `installationId` es un UUID que la app genera una vez por instalación (no es el token de FCM).

## Mensaje sugerido

> Revisamos tu app contra el backend: ya consumen 80 de las 133 rutas, muy bien. Para la evaluación faltan sobre todo 4 cosas (P1): buscar y abrir **cuartos reales** (`/rooms/search` y `/rooms/{id}`; hoy usan el catálogo de ejemplo `/properties`), **registrar el dispositivo para push** (sin eso no llega ninguna notificación), **eliminar la cuenta desde la app** (lo pide Google Play) y la **aceptación de los avisos legales**. Abajo va el contrato exacto de cada una, tal como está en las specs del backend.

## 1. Cuartos reales: búsqueda, mapa, conteo y detalle (NV-14)

Fuente: `specs/nv-14-search-map/spec.md` §6.1.
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/rooms/search?universityId=..&maxPrice=3500&services=wifi,water&sort=distance&page=1&pageSize=20
METHOD       GET
AUTH         Público (sin token); rate limit 60/min por IP
HEADERS      —
REQUEST      filtros de la §6
RESPONSE     200 { items: [{ id, buildingId, title, price, currency: "MXN", roomType, bathroom, buildingType, allowedTenants, services: [..], distanceKm, walkingMinutes, approxLocation: { lat, lng }, thumbnailUrl: null, demo }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 304, 400 filtros, 404 universidad, 429, 503
PAGINATION   Sí: sobre de NV-01; orden por `sort` y después id
```

```text
ENDPOINT     /api/v1/rooms/map?universityId=..&bbox=-99.40,19.95,-99.30,20.05
METHOD       GET
AUTH         Público (sin token); rate limit 60/min por IP
HEADERS      —
REQUEST      filtros de la §6 + bbox
RESPONSE     200 { items: [{ id, price, approxLocation, distanceKm }] } (máximo 300)
STATUS CODES 200, 304, 400, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/rooms/count?universityId=..&maxPrice=3500
METHOD       GET
AUTH         Público (sin token); rate limit 60/min por IP
HEADERS      —
REQUEST      filtros de la §6
RESPONSE     200 { count }
STATUS CODES 200, 304, 400, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/rooms/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 detalle (ver §6)
STATUS CODES 200, 400, 401, 404 no publicado, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/places/search?q=centro&universityId=..
METHOD       GET
AUTH         Público (sin token); rate limit 60/min por IP
HEADERS      —
REQUEST      q (2-60), universityId
RESPONSE     200 { items: [{ id, name, type, approxLocation, distanceKm }] } (máximo 10)
STATUS CODES 200, 304, 400, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 2. Registro del dispositivo para push (NV-20)

Fuente: `specs/nv-20-notifications/spec.md` §6.1.
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
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44457497605): 1027/1027 (unitarias 395, integración 484, seguridad 148); prueba HTTP real de dispositivos en Azure 14/14 (2026-11-19); push en teléfono real pendiente con Mobile
```

## 3. Eliminar la cuenta y avisos legales (NV-10)

Fuente: `specs/nv-10-account-session/spec.md` §6.1.
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/deletion
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { canDelete, blockers: [{ code: "active_rental" | "staff_account", count }], effects: { buildings, rooms, upcomingVisits, openRequests, pendingAgreements, conversations, reviews, favorites, roommateProfile } }
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/deletion
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { password, confirmation: "ELIMINAR" }
RESPONSE     204 sin cuerpo; después: 401 en toda ruta autenticada y en refresh
STATUS CODES 204, 400, 401, 403 (password_mismatch), 409 (deletion_blocked), 429, 503 (deletion_incomplete)
PAGINATION   No
```

```text
ENDPOINT     /api/v1/legal
METHOD       GET
AUTH         Pública
HEADERS      —
REQUEST      —
RESPONSE     200 { termsVersion, privacyVersion, landlordTermsVersion, termsUrl, privacyUrl, accountDeletionUrl, updatedAt }
STATUS CODES 200, 429
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/legal
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { termsVersion, privacyVersion }
RESPONSE     200 { termsVersion, privacyVersion, acceptedAt }
STATUS CODES 200, 400, 401, 409 (legal_version_outdated), 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { …campos de 1.1.0, legal: { termsVersion, privacyVersion, acceptedAt } | null, legalUpToDate }
STATUS CODES 200, 401, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44250483818): 1010/1010 (unitarias 389, integración 478, seguridad 143); prueba HTTP real en Azure 27/27 (2026-11-18)
```

## 4. Reseñas (NV-19)

Fuente: `specs/nv-19-reviews/spec.md` §6.1.
> Las rutas `/admin/reviews/{id}/hide|unhide` son del panel de staff (§11).
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/rooms/{roomId}/reviews/eligibility
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      roomId en la ruta
RESPONSE     200 { eligible, basis, reason, reviewId, editableUntil }
STATUS CODES 200, 400, 401, 404 cuarto, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/rooms/{roomId}/reviews
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { landlord: { communication, attention, compliance, respect }, property: { cleanliness, location, value, condition, services }, comment? }
RESPONSE     201 { id, room: { id, buildingId, title, city }, author: { name }, basis: "visit" | "rental", landlord: { communication, attention, compliance, respect }, property: { cleanliness, location, value, condition, services }, landlordRating, propertyRating, comment, edited, createdAt, updatedAt }
STATUS CODES 201, 400, 401, 403 code=own_room | no_stay, 404 cuarto, 409 code=review_exists, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/rooms/{roomId}/reviews?page=1&pageSize=10
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { summary: { property: { average, count }, landlord: { average, count }, criteria: { communication, attention, compliance, respect, cleanliness, location, value, condition, services } }, items: [reseña], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 404 cuarto, 429, 503
PAGINATION   Sí: orden createdAt desc, id desc
```

```text
ENDPOINT     /api/v1/reviews/{id}
METHOD       PATCH
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { landlord?, property?, comment? }
RESPONSE     200 reseña (edited = true)
STATUS CODES 200, 400, 401, 404 ajena, 409 code=review_locked, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/reviews?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [reseña + status], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden createdAt desc, id desc
```

```text
ENDPOINT     /api/v1/admin/reviews/{id}/hide | /unhide
METHOD       POST
AUTH         Bearer JWT con rol `admin` (NV-21)
HEADERS      —
REQUEST      { reason }
RESPONSE     200 { id, status }
STATUS CODES 200, 400, 401, 403 code=role_required, 404, 409 code=review_state, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44179183562): 916/916 (unitarias 347, integración 462, seguridad 107); prueba HTTP real en Azure 25/25 (2026-11-17)
```

## 5. Visitas del lado del arrendador (NV-17)

Fuente: `specs/nv-17-visits/spec.md` §6.1.
> Las rutas del estudiante (`/visits`, `/users/me/visits`, `/visits/{id}/cancel`) ya se consumen; faltan las de `/landlord/visits`.
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/visits/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 { id, room: { id, buildingId, title, city }, property: { id, title, city } | null, scheduledAt, previousScheduledAt | null, timeZone, status: "requested" | "confirmed" | "cancelled", message, landlord: { name }, address: { street, number, neighborhood, city, state, postalCode } | null, location: { lat, lng } | null, cancellation: { by: "student" | "landlord", reason, note } | null, createdAt, updatedAt, confirmedAt, cancelledAt }
STATUS CODES 200, 400, 401, 404 ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/visits?status=confirmed&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      status (requested|confirmed|cancelled), page, pageSize
RESPONSE     200 { items: [visita], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/visits/{id}/cancel
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { reason? }
RESPONSE     200 visita (cancellation.by = student)
STATUS CODES 200, 400, 401, 404, 409 code=visit_state | visit_in_past, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits?from=2026-11-12T00:00:00-06:00&to=2026-11-30T23:59:59-06:00&status=requested&roomId=..
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      from, to (≤ 62 días), status, roomId, page, pageSize
RESPONSE     200 { items: [{ id, room: { id, buildingId, title, city }, scheduledAt, previousScheduledAt | null, timeZone, status, message, student: { name, university }, cancellation | null, createdAt, updatedAt, confirmedAt, cancelledAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403 code=landlord_required, 429, 503
PAGINATION   Sí: orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 visita del arrendador
STATUS CODES 200, 400, 401, 403, 404 ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/confirm
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 visita del arrendador (confirmed)
STATUS CODES 200, 401, 403, 404, 409 code=visit_state | visit_in_past, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/reschedule
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { scheduledAt, note? }
RESPONSE     200 visita del arrendador (confirmed, previousScheduledAt)
STATUS CODES 200, 400, 401, 403, 404, 409 code=visit_state | visit_in_past | student_schedule_overlap, 422 horario, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/cancel
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { reason: not_available|schedule_conflict|other, note? }
RESPONSE     200 visita del arrendador (cancelled, cancellation.by = landlord)
STATUS CODES 200, 400, 401, 403, 404, 409 code=visit_state | visit_in_past, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 43271840026): 830/830 (unitarias 301, integración 449, seguridad 80); prueba HTTP real en Azure 25/25 (2026-11-12)
```

## 6. Estadísticas del arrendador (NV-25)

Fuente: `specs/nv-25-metrics-stats/spec.md` §6.1.
> `/landlord/stats/rooms` ya se consume; faltan `/landlord/stats` y `/landlord/stats/recommendations`.
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/landlord/stats?from=2026-10-18&to=2026-11-16&buildingId=
METHOD       GET
AUTH         Bearer JWT de arrendador (dueño tomado del token; nunca `ownerId` del cliente)
HEADERS      —
REQUEST      from, to (YYYY-MM-DD, máximo 90 días; por defecto los últimos 30), buildingId?
RESPONSE     200 { from, to, buildingId, totals: { views, favorites, requests, acceptedRequests, visits, confirmedVisits, agreements, conversion }, previous: { views, favorites, requests, acceptedRequests, visits, confirmedVisits, agreements, conversion }, daily: [{ date, views, requests }], favoritesNow, updatedAt }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/stats/rooms?from=&to=&buildingId=&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT de arrendador (dueño tomado del token; nunca `ownerId` del cliente)
HEADERS      —
REQUEST      from, to (YYYY-MM-DD, máximo 90 días; por defecto los últimos 30), buildingId?, page, pageSize
RESPONSE     200 { items: [{ roomId, buildingId, buildingName, number, status, price, views, favorites, requests, visits, agreements, conversion, rating: { average, count } | null }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   Sí: orden views desc, requests desc, número
```

```text
ENDPOINT     /api/v1/landlord/stats/recommendations?buildingId=
METHOD       GET
AUTH         Bearer JWT de arrendador (dueño tomado del token; nunca `ownerId` del cliente)
HEADERS      —
REQUEST      buildingId?
RESPONSE     200 { items: [{ code, priority: "high" | "medium" | "low", buildingId, roomId | null, title, detail }] }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44222420020): 981/981 (unitarias 379, integración 472, seguridad 130); prueba HTTP real en Azure 26/27, la única falla es de la expectativa del script (2026-11-17)
```

## 7. Compañeros de cuarto (NV-22)

Fuente: `specs/nv-22-roommates/spec.md` §6.1.
Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/roommate-profile
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { visible, budgetMin, budgetMax, zone: "walking" | "near_campus" | "any", moveIn: "YYYY-MM", durationMonths, hasPlace, habits: { schedule: "early" | "night" | "mixed", cleaning: "daily" | "weekly" | "relaxed", noise: "quiet" | "moderate" | "social", smoking: "no" | "outside" | "yes", pets: "none" | "cats" | "dogs" | "any", social: "rare" | "occasional" | "frequent" }, about? }
RESPONSE     201 | 200 mi perfil + { id, university, expiresAt, createdAt, updatedAt }
STATUS CODES 200, 201, 400, 401, 409 code=university_required, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/roommate-profile
METHOD       GET | DELETE
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 mi perfil | 204
STATUS CODES 200, 204, 401, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/roommates?budgetMax=2500&zone=walking&moveInFrom=2027-01&hasPlace=false&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      budgetMax, zone, moveInFrom, moveInTo, hasPlace, page, pageSize
RESPONSE     200 { items: [{ id, name, university: { id, name }, budgetMin, budgetMax, zone, moveIn, durationMonths, hasPlace, habits, about, matches, updatedAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403 code=roommate_profile_required, 429, 503
PAGINATION   Sí: orden matches desc, updatedAt desc, id
```

```text
ENDPOINT     /api/v1/roommates/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id del perfil
RESPONSE     200 { id, name, university: { id, name }, budgetMin, budgetMax, zone, moveIn, durationMonths, hasPlace, habits, about, matches, updatedAt }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/roommates/{id}/contact
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { message }
RESPONSE     201 | 200 { conversationId }
STATUS CODES 200, 201, 400, 401, 403, 404, 409 code=own_profile | participant_suspended, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44209958409): 957/957 (unitarias 365, integración 469, seguridad 123); prueba HTTP real en Azure 20/20 (2026-11-17)
```

## 8. Smart Match: «¿te sirvió?» (NV-23)

Fuente: `specs/nv-23-smart-match-ml/spec.md` §6.1. Mostrar un 👍/👎 en cada recomendación (solo con sesión); se guarda con la `modelVersion` para evaluar el modelo.

```text
ENDPOINT     /api/v1/smart-match/feedback
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { roomId, useful: true|false }
RESPONSE     204
STATUS CODES 204, 400, 401, 404 cuarto no publicado, 429, 503
PAGINATION   No
```

## 9. Documentos y fotos privados (NV-13)

Fuente: `specs/nv-13-media-documents/spec.md` §6.1. Ya consumen subir, listar y borrar fotos y documentos; falta **ver** un documento privado.

```text
ENDPOINT     /api/v1/media/{id}/private?size=thumb
METHOD       GET
AUTH         Bearer JWT del dueño o de un revisor
HEADERS      —
REQUEST      size = full | thumb
RESPONSE     200 image/webp (no-store)
STATUS CODES 200, 400, 401, 404, 429, 503
PAGINATION   No
```

## 10. Buscar universidad (NV-11)

Fuente: `specs/nv-11-profile-preferences/spec.md` §6.1.

```text
ENDPOINT     /api/v1/universities/search?q=tula | ?lat=20.05&lng=-99.34
METHOD       GET
AUTH         Público (sin token)
HEADERS      —
REQUEST      q (2-60) o lat y lng, no ambos
RESPONSE     200 { items: [{ id, code, name, city, distanceKm }] } (máximo 20)
STATUS CODES 200, 400, 429, 503
PAGINATION   No
```

## 11. Panel de staff (solo si Mobile lo implementa)

Requieren un usuario con rol `admin` o `reviewer` (NV-21). Contratos en `specs/nv-21-verification-moderation/spec.md`, `specs/nv-19-reviews/spec.md` (ocultar reseñas) y `specs/nv-23-smart-match-ml/spec.md` (métricas).

| Método | Ruta |
| --- | --- |
| GET | `/api/v1/admin/audit` |
| GET | `/api/v1/admin/buildings` |
| GET | `/api/v1/admin/buildings/{id}` |
| POST | `/api/v1/admin/buildings/{id}/approve` |
| GET | `/api/v1/admin/buildings/{id}/documents/{documentId}` |
| POST | `/api/v1/admin/buildings/{id}/reject` |
| POST | `/api/v1/admin/buildings/{id}/request-changes` |
| POST | `/api/v1/admin/buildings/{id}/suspend` |
| POST | `/api/v1/admin/buildings/{id}/unsuspend` |
| GET | `/api/v1/admin/moderation/rejections` |
| GET | `/api/v1/admin/reports` |
| POST | `/api/v1/admin/reports/{id}/resolve` |
| POST | `/api/v1/admin/reviews/{id}/hide` |
| POST | `/api/v1/admin/reviews/{id}/unhide` |
| GET | `/api/v1/admin/smart-match/metrics` |
| POST | `/api/v1/admin/users/{id}/suspend` |
| POST | `/api/v1/admin/users/{id}/unsuspend` |
