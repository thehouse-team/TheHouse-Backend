# NV-19 — Reseñas y calificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-13 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-13 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-19-reviews/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T06 completas; T07–T10 esperan CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-85 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Reviews |
| Precondición | NV-17 1.3.0, NV-18 1.1.0 y NV-21 desplegadas |
| Relación | Usa visitas (NV-17) y rentas (NV-18) como prueba de estancia; NV-21 recibe reportes de reseñas y las oculta; NV-14 mostrará el promedio en una versión posterior |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-13):** se adapta a lo ya desplegado. La elegibilidad usa lo que existe: **visitó** = una visita `confirmed` de NV-17 cuyo horario ya pasó (el estado `completed` sigue reservado y nunca se emite); **rentó** = un convenio `active` o `ended` de NV-18 (puerto `IRentalHistoryQuery`, ya desplegado). La calificación sigue el prototipo «Calificar estancia»: 4 criterios del arrendador y 5 de la propiedad, de 1 a 5, más un comentario opcional. Los promedios se calculan al leer desde las reseñas publicadas, sin colección `rating_summaries` ni eventos. Se agrega la consulta de elegibilidad para que la app sepa si muestra «Calificar», «Mis reseñas», el reporte de una reseña (objetivo nuevo `review` en NV-21) y ocultar o mostrar una reseña por un admin. No cambia el detalle de NV-14: la app pide el resumen de reseñas aparte.

## 1. Contexto

El prototipo tiene la pantalla «Calificar estancia» (el arrendador en 4 criterios: comunicación, atención, cumplimiento y trato; la propiedad en 5: limpieza, ubicación, calidad/precio, estado y servicios; comentario opcional) y muestra el promedio y el número de reseñas en el detalle del cuarto y en el perfil del propietario. RF-85 pide que solo califique quien visitó o rentó. Con NV-17 1.3.0 y NV-18 1.1.0 ya hay visitas confirmadas y convenios en producción, así que la elegibilidad se puede verificar en el servidor.

## 2. Objetivo

Que el estudiante que visitó o rentó un cuarto lo califique a él y a su arrendador, y que cualquier usuario con sesión vea reseñas confiables y sus promedios.

## 3. Precondiciones

1. NV-17 1.3.0, NV-18 1.1.0 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Consultar si puedo calificar un cuarto (y con qué base: visita o renta).
2. Crear una reseña por cuarto con 9 criterios de 1 a 5 y comentario opcional.
3. Editar mi reseña durante 30 días.
4. Listar las reseñas publicadas de un cuarto con su resumen: promedio de la propiedad, promedio del arrendador (todas sus reseñas) y promedio por criterio.
5. Mis reseñas.
6. Reportar una reseña (objetivo `review` en los reportes de NV-21).
7. Ocultar y volver a mostrar una reseña (rol `admin`, con motivo y bitácora de NV-21).
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Respuesta del arrendador a una reseña.
- Promedio dentro de la búsqueda y del detalle de NV-14: versión posterior (la app consulta el resumen aparte).
- Notificación de reseña nueva (el catálogo de NV-20 queda igual).
- Moderación automática del texto; las reseñas se reportan y las revisa un admin.
- Fotos en reseñas.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/rooms/{roomId}/reviews/eligibility` | Autenticado | ¿Puedo calificar este cuarto? | 200, 400, 401, 404, 429, 503 |
| POST | `/api/v1/rooms/{roomId}/reviews` | Autenticado y elegible | Crea mi reseña | 201, 400, 401, 403, 404, 409, 429, 503 |
| GET | `/api/v1/rooms/{roomId}/reviews` | Autenticado | Reseñas publicadas y resumen | 200, 400, 401, 404, 429, 503 |
| PATCH | `/api/v1/reviews/{id}` | Autor | Edita mi reseña dentro de 30 días | 200, 400, 401, 404, 409, 429, 503 |
| GET | `/api/v1/users/me/reviews` | Autenticado | Mis reseñas | 200, 400, 401, 429, 503 |
| POST | `/api/v1/admin/reviews/{id}/hide` | Rol `admin` | Oculta con `{ reason }` | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/admin/reviews/{id}/unhide` | Rol `admin` | Vuelve a mostrar con `{ reason }` | 200, 400, 401, 403, 404, 409, 429, 503 |

- Reseña: `{ id, room: { id, buildingId, title, city }, author: { name }, basis: "visit" | "rental", landlord: { communication, attention, compliance, respect }, property: { cleanliness, location, value, condition, services }, landlordRating, propertyRating, comment, edited, createdAt, updatedAt }`. Los 9 criterios son enteros de 1 a 5; `landlordRating` y `propertyRating` son el promedio de su parte con un decimal; `edited` indica si se editó; `author.name` es el primer nombre del perfil o «Estudiante».
- Crear y editar: `{ landlord: { communication, attention, compliance, respect }, property: { cleanliness, location, value, condition, services }, comment? }`. Crear exige los 9 criterios; editar acepta solo las partes que cambian. `comment` ≤ 1000 caracteres, texto plano.
- Elegibilidad: `{ eligible, basis: "visit" | "rental" | null, reason: "own_room" | "no_stay" | "already_reviewed" | null, reviewId | null, editableUntil | null }`.
- Listado de un cuarto: `{ summary: { property: { average, count }, landlord: { average, count }, criteria: { communication, attention, compliance, respect, cleanliness, location, value, condition, services } }, items: [reseña], page, pageSize, totalItems, totalPages }`; orden `createdAt` desc e id. Sin reseñas, los promedios son `null` y `count` 0. El promedio del arrendador cuenta todas sus reseñas publicadas, de todos sus cuartos.
- Mis reseñas: las mismas reseñas con `status` (`published` | `hidden`) para que el autor sepa si una se ocultó.
- Reportes (NV-21): `targetType` acepta `review` además de `building`, `room` y `user`.
- Códigos: 403 `own_room` (el arrendador no califica su cuarto), 403 `no_stay` (sin visita pasada ni renta), 409 `review_exists`, 409 `review_locked` (pasaron los 30 días), 409 `review_state` (ocultar una oculta o mostrar una publicada), 403 `role_required`.
- Todas las respuestas con `no-store`.

## 6.1 Contrato para Mobile

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
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Reviews** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `reviews` |
| Usa (puertos de otros módulos) | Visits `IVisitRepository` (NV-17) para saber si el autor visitó el cuarto; Rentals `IRentalHistoryQuery` (NV-18) para saber si el autor rentó el cuarto; Listings `IRoomRepository` e `IBuildingRepository` (NV-12) para el cuarto, su dueño y su título; Profile `IProfileRepository` (NV-11) para el primer nombre del autor |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `reviews` (colección nueva del módulo Reviews): `roomId`, `buildingId`, `ownerId`, `authorId`, `basis`, los 9 criterios, `landlordRating`, `propertyRating`, `comment`, `status` (`published` | `hidden`), `edited`, `createdAt`, `updatedAt`, `hiddenAt`.
- Índices: único `{ authorId, roomId }`; `{ roomId, status, createdAt: -1 }` para el listado y el promedio del cuarto; `{ ownerId, status }` para el promedio del arrendador; `{ authorId, createdAt: -1 }` para mis reseñas.
- Promedios con una agregación sobre las reseñas `published` al leer; sin colección de resúmenes ni eventos (los volúmenes de un cuarto son pequeños).
- Sin migraciones; no cambia ninguna colección existente salvo el catálogo de objetivos de reporte de NV-21.

## 9. Reglas de negocio

1. Elegible si el autor no es el dueño del cuarto y tiene una visita `confirmed` a ese cuarto cuyo horario ya pasó (visitó) o un convenio `active` o `ended` de ese cuarto (rentó); si tiene ambos, `basis` es `rental`.
2. Una reseña por autor y cuarto (índice único); la segunda → 409 `review_exists`.
3. El autor la edita durante 30 días desde que la creó; después → 409 `review_locked`. Editar marca `edited` y recalcula los promedios de esa reseña.
4. Solo las reseñas `published` aparecen en el listado y cuentan en los promedios; el autor ve las suyas también ocultas.
5. Ocultar y mostrar: solo rol `admin`, con motivo obligatorio y registro en la bitácora de NV-21.
6. Se puede calificar un cuarto aunque ya no esté publicado (por ejemplo, rentado), siempre que exista.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Elegibilidad verificada en el servidor con las visitas y convenios del token; nunca con datos del cliente.
- BOLA: solo el autor edita (lo ajeno 404); el arrendador no puede calificar sus cuartos (403 `own_room`).
- El autor aparece solo con su primer nombre; nunca correo, teléfono, id ni universidad. Los textos de la reseña no se registran en logs.
- Rate limit 60/min por usuario y 10 reseñas nuevas al día; Redis caído → 503.
- Entradas con campos desconocidos → 400 (sin `authorId`, `status` ni promedios del cliente).

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Calificar el cuarto propio o sin estancia | 403 | Acceso denegado (`code`: `own_room`, `no_stay`, `role_required`) |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Reseña repetida, fuera de plazo o estado inválido | 409 | Conflicto (`code`: `review_exists`, `review_locked`, `review_state`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-19-R01 | Elegibilidad verificada | Visita confirmada pasada o convenio activo o terminado del autor en ese cuarto; el dueño 403 `own_room`; sin estancia 403 `no_stay`; la consulta de elegibilidad responde lo mismo que crear. | F1 | T01 |
| NV-19-R02 | Crear y editar | 9 criterios de 1 a 5 obligatorios al crear y comentario ≤ 1000; una por autor y cuarto (409 `review_exists`); editar solo el autor durante 30 días (409 `review_locked`); promedios de la reseña con un decimal. | F1 | T02 |
| NV-19-R03 | Listado y resumen | Reseñas publicadas del cuarto paginadas por fecha; resumen con promedio del cuarto, del arrendador (todas sus reseñas) y por criterio; sin reseñas, promedios `null`. | F2 | T03 |
| NV-19-R04 | Mis reseñas | Lista del autor con su estado, incluidas las ocultas. | F2 | T04 |
| NV-19-R05 | Moderación | Reporte con objetivo `review` en NV-21; ocultar y mostrar solo con rol `admin`, motivo y bitácora; una oculta deja de contar en los promedios. | F2 | T05 |
| NV-19-R06 | Privacidad, autorización y límites | Pruebas con autor, arrendador, tercero y admin; autor solo con primer nombre; rate limits 429; Redis caído 503; campos desconocidos 400. | F3 | T06 |
| NV-19-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (no elegible, elegible por renta, crear, listar con resumen, editar, reportar y ocultar). | F4 | T07 |
| NV-19-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Elegibilidad «visitó» | Visita `confirmed` cuyo horario ya pasó (NV-17 no emite `completed`) | APROBADO (M-BE-072) |
| Criterios | Los 9 del prototipo (4 del arrendador y 5 de la propiedad), de 1 a 5, todos obligatorios al crear | APROBADO (M-BE-072) |
| Promedios | Calculados al leer desde las reseñas publicadas; sin `rating_summaries` ni eventos (cambia la propuesta de 1.0.0) | APROBADO (M-BE-072) |
| Visibilidad | Las reseñas requieren sesión, igual que el detalle (RF-24) | APROBADO (M-BE-072) |
| Edición | 30 días desde la creación; sin borrar la reseña | APROBADO (M-BE-072) |
| Moderación | Objetivo `review` en los reportes de NV-21; ocultar y mostrar por un `admin` desde NV-21 (Moderation usa un puerto de Reviews) | APROBADO (M-BE-072) |
| NV-14 y NV-20 | Sin cambios en 1.1.0: el promedio en la búsqueda y la notificación de reseña nueva quedan para después | APROBADO (M-BE-072) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva (`reviews`) con cuatro índices; sin proveedores nuevos |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-19-RK01 | Reseñas falsas | Promedios engañosos | Solo con visita pasada o renta verificadas en el servidor; una por cuarto; reporte y ocultamiento |
| NV-19-RK02 | Comentarios ofensivos o con datos personales | Daño a terceros | Reporte de reseñas en NV-21 y ocultamiento por un admin con bitácora |
| NV-19-RK03 | Promedio lento en arrendadores con muchas reseñas | Detalle lento | Índices por cuarto y por arrendador; resumen solo en el listado de reseñas |
| NV-19-RK04 | Represalias del arrendador | Estudiantes que no califican | El arrendador ve solo el primer nombre y no puede responder ni borrar |

## 16. Dependencias

- NV-17 1.3.0 (visitas), NV-18 1.1.0 (`IRentalHistoryQuery`) y NV-21 (reportes, rol `admin` y bitácora).
- NV-14 y NV-20 (versiones posteriores).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 6 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T06 completas; T07–T10 esperan CI, despliegue y Azure** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
