# NV-25 — Estadísticas del arrendador

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T09; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-25-metrics-stats/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-86 (estadísticas del propietario); RF-66 se mueve a NV-23/NV-24 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-05, RNF-11 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Analytics |
| Precondición | NV-12, NV-13, NV-15, NV-16, NV-17, NV-18 y NV-19 desplegadas |
| Relación | Lee cuartos y edificios (NV-12), fotos (NV-13), favoritos y visitas (NV-15/NV-17), solicitudes (NV-16), convenios (NV-18) y reseñas (NV-19); registra vistas en el detalle público (NV-07/NV-08); Smart Match (NV-23/NV-24) tendrá sus propias métricas |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-17):** se acota a las **estadísticas del arrendador** (RF-86) para no depender de Smart Match. Favoritos (NV-15), visitas (NV-17), solicitudes (NV-16), convenios (NV-18), reseñas (NV-19) y fotos (NV-13) ya están en la base, así que se cuentan directamente en la consulta. Lo único nuevo son las **vistas** del detalle del cuarto y del edificio, deduplicadas por persona y día sin guardar quién vio. **No hay tarea diaria**: los números se calculan al pedirlos (máximo 90 días), lo que elimina el problema de ejecutarla una sola vez con dos réplicas. Las métricas de Smart Match para el administrador (RF-66) pasan a NV-23/NV-24, donde nacen esos datos.

## 1. Contexto

El arrendador necesita saber si sus anuncios funcionan: cuántas personas ven sus cuartos, cuántas los guardan, piden visita, envían solicitud y terminan rentando, y qué puede mejorar. RF-86 pide estadísticas del propietario. Casi todos los datos ya existen en las colecciones de NV-12 a NV-19; faltan las vistas, que hoy no se registran. La versión 1.0.0 dependía de Smart Match (NV-23), que aún no existe, y de una tarea diaria difícil de ejecutar una sola vez con dos réplicas.

## 2. Objetivo

Que el arrendador vea, para un rango de hasta 90 días, las vistas, favoritos, visitas, solicitudes y convenios de sus cuartos con su conversión y tendencia, y reciba sugerencias claras para mejorar sus anuncios, sin exponer quién vio o guardó un cuarto.

## 3. Precondiciones

1. NV-12, NV-13, NV-15, NV-16, NV-17, NV-18 y NV-19 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Registro de vistas del detalle público del cuarto (`GET /rooms/{id}`) y del edificio (`GET /properties/{id}`), una por persona y día.
2. Resumen del arrendador: totales del rango, el periodo anterior del mismo tamaño para la tendencia y la serie diaria de vistas y solicitudes.
3. Estadísticas por cuarto, paginadas.
4. Sugerencias por reglas simples y explicables (fotos, precio, detalles, solicitudes y visitas sin responder, vistas sin solicitudes).
5. Filtro por edificio propio.
6. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Métricas de Smart Match para el administrador (RF-66): NV-23/NV-24.
- Saber quién vio o guardó un cuarto, o estadísticas por usuario.
- Analítica de terceros, SDK de seguimiento o cookies.
- Exportar a CSV o PDF.
- Vistas desde la lista de búsqueda (solo cuenta el detalle).
- Vistas anteriores al despliegue (empiezan en cero).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/landlord/stats` | Arrendador | Resumen del rango con tendencia y serie diaria | 200, 400, 401, 403, 404, 429, 503 |
| GET | `/api/v1/landlord/stats/rooms` | Arrendador | Estadísticas por cuarto, paginadas | 200, 400, 401, 403, 404, 429, 503 |
| GET | `/api/v1/landlord/stats/recommendations` | Arrendador | Sugerencias para mejorar los anuncios | 200, 400, 401, 403, 404, 429, 503 |

- Rango: `from, to (YYYY-MM-DD, máximo 90 días; por defecto los últimos 30), buildingId?`. `from ≤ to`, máximo 90 días y no antes de 365 días atrás (400 si no). Fechas en la zona de la Ciudad de México. `buildingId` debe ser un edificio propio (ajeno o inexistente → 404).
- Resumen: `{ from, to, buildingId, totals: { views, favorites, requests, acceptedRequests, visits, confirmedVisits, agreements, conversion }, previous: { views, favorites, requests, acceptedRequests, visits, confirmedVisits, agreements, conversion }, daily: [{ date, views, requests }], favoritesNow, updatedAt }`. `previous` es el periodo anterior del mismo largo, para mostrar «↑ 12 %». `favorites` son los guardados nuevos del rango que siguen guardados; `favoritesNow` son los guardados actuales. `conversion` = solicitudes ÷ vistas con 3 decimales (`null` sin vistas).
- Por cuarto: `{ items: [{ roomId, buildingId, buildingName, number, status, price, views, favorites, requests, visits, agreements, conversion, rating: { average, count } | null }], page, pageSize, totalItems, totalPages }` con `page`, `pageSize` ≤ 50; orden `views` desc, `requests` desc y número de cuarto. `rating` sale de NV-19 (solo reseñas visibles).
- Sugerencias: `{ items: [{ code, priority: "high" | "medium" | "low", buildingId, roomId | null, title, detail }] }`, máximo 20, ordenadas por prioridad. Códigos: `add_photos`, `price_above_median`, `missing_details`, `views_without_requests`, `pending_requests`, `pending_visits`.
- Las vistas se registran en el detalle público sin cambiar su respuesta, su caché ni su tiempo; si el registro falla, el detalle responde igual.
- Todas las respuestas de estadísticas con `no-store`.

## 6.1 Contrato para Mobile

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

## 7. Módulo, datos propios e integración

Módulo **Analytics** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `room_views` |
| Usa (puertos de otros módulos) | Listings `IListingRepository` (NV-12) para cuartos, edificios y la mediana de precios; Media `IMediaRepository` (NV-13) para contar fotos; Favorites (NV-15) para contar guardados por cuarto; Visits (NV-17) para contar visitas solicitadas y confirmadas; Messaging (NV-16) para contar solicitudes y su respuesta; Rentals (NV-18) para contar convenios activados; Reviews (NV-19) para la calificación visible por cuarto; Identity `IRoleQuery` (NV-10) para el rol de arrendador |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | `Lectura del detalle público (NV-07/NV-08): registra la vista sin cambiar la respuesta` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `room_views` (colección nueva del módulo Analytics): `roomId`, `buildingId`, `ownerId`, `day` (`YYYY-MM-DD`), `actorHash`, `createdAt`. Una vista del edificio se guarda con `roomId` vacío.
- Índices: único `{ roomId, buildingId, day, actorHash }` (una vista por persona y día, idempotente con dos réplicas); `{ ownerId, day }` para las consultas; TTL de 400 días en `createdAt`.
- `actorHash` = HMAC-SHA256 del usuario (si trae token válido) o de la IP y el agente, con una sal aleatoria del día guardada en Redis (`SET NX`, 48 h). La sal se descarta: el mismo usuario no se puede seguir entre días ni recuperar desde el hash.
- Favoritos, visitas, solicitudes, convenios, reseñas y fotos se leen de sus colecciones; no se copian.

## 9. Reglas de negocio

1. Solo usuarios con rol de arrendador (NV-10) y solo sus cuartos y edificios (`ownerId` del token).
2. Una vista por persona, cuarto (o edificio) y día; no cuentan las vistas del propio dueño ni de cuentas staff con token válido.
3. Rango máximo de 90 días; el periodo anterior tiene el mismo largo y termina el día antes de `from`.
4. Conteos: solicitudes creadas en el rango; aceptadas con fecha de aceptación en el rango; visitas creadas en el rango y confirmadas; convenios activados en el rango.
5. Sugerencias: `add_photos` (cuarto publicado con menos de 2 fotos); `price_above_median` (precio 20 % arriba de la mediana de cuartos publicados cerca de la misma universidad, con al menos 5 para comparar); `missing_details` (sin tamaño, muebles o fecha de disponibilidad); `views_without_requests` (30 vistas o más en 30 días sin solicitudes); `pending_requests` (solicitudes sin responder por más de 48 h); `pending_visits` (visitas sin confirmar por más de 24 h).
6. Las sugerencias se calculan al pedirlas y se explican con números del propio arrendador (por ejemplo «3 solicitudes esperan respuesta»).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: todo se filtra por el `ownerId` del token; `buildingId` ajeno → 404.
- Privacidad (RNF-11): solo agregados; nunca ids, nombres ni hashes de quien vio, guardó o solicitó; los hashes cambian de sal cada día.
- El registro de vistas no agrega cookies ni identificadores al cliente y no cambia la respuesta pública.
- Rate limit: 30/min por usuario; Redis caído → 503 en estadísticas (el detalle público sigue respondiendo).
- Parámetros desconocidos o inválidos → 400.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Usuario sin rol de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-25-R01 | Registro de vistas | Una vista por persona, cuarto o edificio y día con `actorHash` de sal diaria; sin vistas del dueño; dos réplicas no duplican; si falla, el detalle público responde igual. | F1 | T01 |
| NV-25-R02 | Resumen del arrendador | Totales del rango, periodo anterior y serie diaria con conversión; solo datos propios; rango validado (90 días). | F1 | T02 |
| NV-25-R03 | Estadísticas por cuarto | Vistas, favoritos, solicitudes, visitas, convenios, conversión y calificación por cuarto; paginado y ordenado; filtro por edificio propio. | F2 | T03 |
| NV-25-R04 | Sugerencias por reglas | Las 6 reglas documentadas, cada una con una prueba que la dispara y otra que no; máximo 20; textos en español con números del arrendador. | F2 | T04 |
| NV-25-R05 | Privacidad, autorización y límites | Sin datos de usuarios en ninguna respuesta; otro arrendador o estudiante 403/404; 429; Redis caído 503 en estadísticas. | F3 | T05 |
| NV-25-R06 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (vistas de un estudiante, favorito, visita y solicitud reflejados en el resumen, por cuarto y en sugerencias). | F4 | T06 |
| NV-25-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Alcance | Solo estadísticas del arrendador (RF-86); las métricas de Smart Match (RF-66) pasan a NV-23/NV-24 | APROBADO (M-BE-075) |
| Cálculo | Al pedirlo, sobre las colecciones existentes, sin tarea diaria ni colección de agregados; se reconsidera si una consulta pasa de 1 s | APROBADO (M-BE-075) |
| Vistas | Solo el detalle del cuarto y del edificio; una por persona y día; sal diaria en Redis sin secreto nuevo | APROBADO (M-BE-075) |
| Retención | 400 días para `room_views` (permite comparar con el año anterior) | APROBADO (M-BE-075) |
| Sugerencias | 6 reglas fijas con umbrales documentados (2 fotos, 20 % sobre la mediana, 30 vistas, 48 h, 24 h) | APROBADO (M-BE-075) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección pequeña (`room_views`, un documento por persona, cuarto y día) y una llave de Redis al día |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-25-RK01 | Consultas lentas con muchos datos | Tablero lento | Índice `{ ownerId, day }`, rango de 90 días y límite de 20 sugerencias |
| NV-25-RK02 | Vistas infladas por bots | Números poco fiables | Una por persona y día, rate limit público existente; sin dueño ni staff |
| NV-25-RK03 | Re-identificar a quien vio | Privacidad | Sal diaria descartada, solo agregados en las respuestas |
| NV-25-RK04 | Caché del cliente evita la petición | Menos vistas contadas | Se documenta como «vistas que llegan al servidor»; el detalle usa ETag |

## 16. Dependencias

- NV-12, NV-13, NV-15, NV-16, NV-17, NV-18 y NV-19 (datos que se cuentan).
- NV-23/NV-24 (métricas de Smart Match en su propia spec).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 9 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
