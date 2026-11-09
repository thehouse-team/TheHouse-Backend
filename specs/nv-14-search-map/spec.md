# NV-14 — Búsqueda, filtros y mapa

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-14-search-map/`; los seeds de colonias y de demostración en Production requieren autorización aparte) |
| Estado de ejecución | **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-08 (conteo), RF-23 a RF-30, RF-41 (ubicación aproximada), RF-47 (filtro compartido), RF-68, RF-73 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-05, RNF-08 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Search |
| Precondición | NV-11 (universidades con coordenadas), NV-12 (cuartos) y NV-21 (verificación) desplegadas |
| Relación | Hace públicos los cuartos aprobados en NV-21; NV-15/NV-17 1.2.0 migrarán favoritos y visitas a estos cuartos; NV-23 ordenará por compatibilidad |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-07):** se adapta a lo ya desplegado: cuartos `available` de edificios `verified` (NV-12 y NV-21) y universidades con coordenadas (NV-11). Contrato para Mobile (§6.1); filtros con los catálogos de NV-12; ubicación aproximada por celda de unos 300 m sin secretos y distancias calculadas desde esa celda (evita triangular la dirección con varias universidades); consulta en dos pasos sin modelo de lectura en v1; seed autorizado de colonias desde OpenStreetMap; seed de demostración que convierte las 12 propiedades de NV-07 en cuartos verificados marcados `demo`. La migración de favoritos y visitas a cuartos sale de NV-14 y se hace en NV-15 1.2.0 y NV-17 1.2.0, coordinada con Mobile. `/properties` no cambia. Fotos con NV-13; eventos fuera de v1.

## 1. Contexto

La app muestra hoy 12 propiedades de ejemplo (NV-07) sin dueño. Con NV-12 y NV-21 ya existen cuartos `available` de edificios `verified`, pero ningún endpoint público los lista. El estudiante, con o sin cuenta, necesita ver las rentas cerca de su universidad en un listado y un mapa, filtrar por precio, tipo, baño y servicios, buscar colonias y saber cuántos cuartos entran en su presupuesto. La dirección exacta nunca es pública (RF-41) y el detalle requiere sesión (RF-24).

## 2. Objetivo

Exponer la búsqueda pública de cuartos aprobados con filtros, orden, mapa, conteo y búsqueda de colonias, y el detalle con sesión, sin revelar la ubicación exacta.

## 3. Precondiciones

1. NV-11, NV-12 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA»; los seeds de colonias y de demostración en Production requieren autorización aparte (M-BE-043).

## 4. Alcance

1. `GET /rooms/search` con filtros, orden y paginación.
2. `GET /rooms/map` con marcadores dentro de un área.
3. `GET /rooms/count` para el presupuesto (RF-08).
4. `GET /rooms/{id}` con sesión (RF-24).
5. `GET /places/search` de colonias y ciudades (RF-25) con seed desde OpenStreetMap.
6. Ubicación aproximada por celda y distancias desde la celda.
7. Seed de demostración con las 12 propiedades de NV-07 como cuartos verificados marcados `demo`.
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Migración de favoritos y visitas a cuartos: NV-15 1.2.0 y NV-17 1.2.0, coordinadas con Mobile.
- Cambios en `GET /properties` y `GET /properties/{id}`: siguen igual hasta esa migración.
- Fotos (`thumbnailUrl` es `null`): NV-13.
- Geocodificación con servicios de pago.
- Orden por compatibilidad (Smart Match): NV-23.
- Evento `RoomViewed` para estadísticas: con NV-25.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/rooms/search` | Público | Cuartos disponibles filtrados, ordenados y paginados | 200, 304, 400, 404, 429, 503 |
| GET | `/api/v1/rooms/map` | Público | Marcadores dentro de un `bbox` (máximo 300) | 200, 304, 400, 404, 429, 503 |
| GET | `/api/v1/rooms/count` | Público | Cuántos cuartos entran en el presupuesto y la distancia | 200, 304, 400, 404, 429, 503 |
| GET | `/api/v1/rooms/{id}` | Autenticado | Detalle del cuarto y de su edificio | 200, 400, 401, 404, 429, 503 |
| GET | `/api/v1/places/search` | Público | Colonias y ciudades cercanas a la universidad | 200, 304, 400, 404, 429, 503 |

- Filtros comunes: `universityId` (obligatorio; 404 si no existe), `minPrice`, `maxPrice` (500–50 000), `roomType` (`single`, `double`, `shared`), `bathroom` (`private`, `shared`), `buildingType` (`rooms_building`, `house_rooms`, `full_apartment`), `allowedTenants` (`women`, `men`, `any`; `women` incluye edificios `any`), `services` (lista separada por comas, catálogo de NV-11; el cuarto debe tenerlos todos), `maxDistanceKm` (0.5–30, por defecto 10).
- `rooms/search`: además `sort` (`distance` por defecto, `price`, `newest`), `page`, `pageSize` (máximo 50); cada item: `{ id, buildingId, title, price, currency: "MXN", roomType, bathroom, buildingType, allowedTenants, services: [..], distanceKm, walkingMinutes, approxLocation: { lat, lng }, thumbnailUrl: null, demo }`. `title` = nombre del edificio y número de cuarto.
- `rooms/map`: además `bbox=minLng,minLat,maxLng,maxLat` (lado máximo 0.3°); responde `{ items: [{ id, price, approxLocation, distanceKm }] }` con máximo 300 marcadores.
- `rooms/count`: mismos filtros; responde `{ count }`.
- `rooms/{id}`: con sesión; devuelve el item más `sizeM2`, `furniture`, `availableFrom`, `minContract`, `rules`, `nearestUniversity` y `building { id, name, type, allowedTenants, services, rules }`; nunca dirección, coordenadas reales, dueño ni teléfono. Un cuarto que no está publicado responde 404.
- `places/search`: `q` (2–60) y `universityId`; hasta 10 resultados `{ id, name, type (neighborhood|city), approxLocation, distanceKm }`, ordenados por coincidencia y distancia; sin coincidencias → 200 con lista vacía.
- `approxLocation` es el centro de una celda de 0.003° (unos 330 m) que contiene al edificio; `distanceKm` (1 decimal) y `walkingMinutes` se calculan desde ese centro. `demo: true` marca los cuartos del seed de demostración.
- Respuestas públicas con `ETag` y caché pública corta (M-BE-046); el detalle con `no-store`.

## 6.1 Contrato para Mobile

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

## 7. Módulo, datos propios e integración

Módulo **Search** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `places` |
| Usa (puertos de otros módulos) | Listings `IBuildingRepository` e `IRoomRepository` (NV-12) para leer el catálogo publicado; Catalog `IUniversityQuery` (NV-08) para la universidad de referencia |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- Sin colecciones nuevas para la búsqueda: lee `buildings` (`verified`, índice `2dsphere`) y `rooms` (`available`, índice `{ status, price }`) de NV-12 en dos pasos.
- `places`: `name`, `nameNormalized`, `type`, `city`, `location` (GeoJSON) con índice `2dsphere`; seed curado desde OpenStreetMap con el id de cada elemento.
- `rooms` y `buildings` agregan `demo` (booleano, por defecto `false`) para el seed de demostración.

## 9. Reglas de negocio

1. Solo cuartos `available` de edificios `verified`; un edificio suspendido o rechazado desaparece de inmediato.
2. Paso 1: edificios `verified` dentro de `maxDistanceKm` de la universidad (`$nearSphere`, filtros de edificio). Paso 2: cuartos `available` de esos edificios con los filtros de cuarto. El orden y la paginación se aplican sobre el resultado combinado; con más de 5 000 cuartos candidatos se introduce un modelo de lectura (decisión futura).
3. La ubicación aproximada es el centro de la celda de 0.003° del edificio; la distancia y los minutos a pie se calculan desde ese centro, nunca desde la ubicación real.
4. Seed de demostración (`--seed-demo-rooms`, autorizado): crea un arrendador de sistema sin contraseña (no puede iniciar sesión) y, por cada propiedad de NV-07, un edificio `verified` y un cuarto `available` con los mismos datos, ubicados a 0.5–2 km de su universidad; marca `demo: true`; es idempotente y guarda el mapeo propiedad → cuarto para la migración de NV-15/NV-17.
5. Seed de colonias (`--seed-places`, autorizado): colonias y cabeceras de los municipios de las 10 universidades, tomadas de OpenStreetMap con su id; idempotente.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Ningún endpoint público devuelve dirección, coordenadas reales, `ownerId`, teléfono ni nombre del arrendador (RNF-02).
- Anti-extracción: `pageSize` ≤ 50, `bbox` ≤ 0.3° por lado, máximo 300 marcadores, rate limit 60/min por IP en búsqueda, mapa, conteo y colonias; detalle 60/min por usuario.
- Las distancias desde la celda impiden triangular la ubicación exacta consultando varias universidades.
- El texto de búsqueda de colonias se escapa antes de usarse como patrón.

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
| NV-14-R01 | Búsqueda con filtros | Combina precio, tipo de cuarto, baño, tipo de edificio, con quién vivir, servicios y distancia; orden `distance`, `price` o `newest` estable con id; paginación máximo 50; solo cuartos publicables. | F1 | T01 |
| NV-14-R02 | Mapa de rentas | Marcadores dentro de un `bbox` válido (máximo 0.3° por lado), máximo 300, con precio y distancia; `bbox` inválido 400. | F1 | T02 |
| NV-14-R03 | Conteo por presupuesto | `rooms/count` devuelve el total con los mismos criterios que la búsqueda. | F1 | T03 |
| NV-14-R04 | Detalle con sesión | Invitado 401; con sesión devuelve el detalle sin dirección, coordenadas reales ni dueño; cuarto no publicado 404. | F1 | T04 |
| NV-14-R05 | Colonias y ciudades | Coincidencia parcial sin acentos ni mayúsculas, máximo 10, lista vacía con 200; seed autorizado desde OpenStreetMap. | F2 | T05 |
| NV-14-R06 | Ubicación aproximada | Centro de celda estable para el mismo edificio y a menos de 250 m de la ubicación real; distancias calculadas desde la celda; prueba de que dos universidades no permiten triangular más allá de la celda. | F2 | T06 |
| NV-14-R07 | Datos de demostración | `--seed-demo-rooms` crea los 12 cuartos `demo` verificados, idempotente, con arrendador de sistema sin acceso y mapeo propiedad → cuarto. | F2 | T07 |
| NV-14-R08 | Índices y rendimiento | Índices geoespaciales y compuestos; prueba de carga básica con p95 < 500 ms en Azure con los datos de demostración. | F3 | T08 |
| NV-14-R09 | Anti-extracción y límites | Límites de `pageSize`, `bbox` y marcadores; rate limits con 429; Redis caído 503; ningún campo privado en respuestas públicas. | F3 | T09 |
| NV-14-R10 | Contrato para Mobile y despliegue | Contrato de la §6.1 entregado a Mobile con URL desplegada; seeds autorizados en Production; prueba HTTP real en Azure (búsqueda, mapa, conteo, detalle, colonias). | F4 | T10 |
| NV-14-R11 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T11, T12, T13 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Catálogo público | Solo cuartos `available` de edificios `verified` | APROBADO (M-BE-066) |
| Consulta | Dos pasos sobre `buildings` y `rooms`, sin modelo de lectura en v1 | APROBADO (M-BE-066) |
| Ubicación aproximada | Centro de celda de 0.003° y distancias desde la celda, sin secretos | APROBADO (M-BE-066) |
| Detalle con sesión | `/rooms/{id}` exige sesión (RF-24); `/properties` sin cambios | APROBADO (M-BE-066) |
| Migración de favoritos y visitas | Fuera de NV-14: NV-15 1.2.0 y NV-17 1.2.0 con Mobile, usando el mapeo del seed de demostración | APROBADO (M-BE-066) |
| Datos de demostración | Seed autorizado que convierte las 12 propiedades en cuartos verificados `demo` | APROBADO (M-BE-066) |
| Colonias | Seed curado desde OpenStreetMap (ODbL) para los municipios de las 10 universidades | APROBADO (M-BE-066) |
| Filtros | Los de la §6 con los catálogos de NV-11/NV-12 | APROBADO (M-BE-066) |
| Fotos y eventos | `thumbnailUrl` nulo hasta NV-13; `RoomViewed` con NV-25 | APROBADO (M-BE-066) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Lecturas con índices existentes; `places` con unas decenas de documentos; un comando Redis por solicitud |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-14-RK01 | Triangular la dirección exacta | Seguridad del arrendador | Celdas de 330 m y distancias desde la celda |
| NV-14-RK02 | Extracción masiva del catálogo | Copia por terceros | Límites de página, `bbox`, marcadores y rate limit |
| NV-14-RK03 | Consulta en dos pasos lenta con muchos cuartos | Latencia en móvil | Prueba p95 y modelo de lectura al pasar de 5 000 candidatos |
| NV-14-RK04 | Cuartos de demostración confundidos con reales | Confianza del usuario | Campo `demo` y etiqueta "Ejemplo" en la app |
| NV-14-RK05 | Dos catálogos mientras no se migran favoritos y visitas | Confusión en la app | NV-15/NV-17 1.2.0 justo después de NV-14 |

## 16. Dependencias

- NV-11, NV-12 y NV-21.
- NV-13 (fotos).
- NV-15 y NV-17 1.2.0 (migración).
- NV-23 (Smart Match) y NV-25 (estadísticas).

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
