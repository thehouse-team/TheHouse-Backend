# NV-14 — Búsqueda, filtros y mapa

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-08 (conteo), RF-23 a RF-30, RF-41 (ubicación aproximada), RF-47 (filtro compartido), RF-68, RF-73 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-05, RNF-08 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-11 y NV-12 |
| Relación | Evoluciona NV-07 (listado y detalle públicos); NV-15, NV-16 y NV-23 parten de sus resultados |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El estudiante, con o sin cuenta, ve las rentas en un mapa centrado en su universidad y en un listado debajo, con filtros de precio, tipo, modalidad y servicios, y busca colonias o ciudades. RF-24 pide que el detalle requiera sesión, mientras NV-07 lo expone hoy de forma pública. La dirección exacta nunca es pública: solo una ubicación aproximada (RF-41).

## 2. Objetivo

Definir la búsqueda pública de cuartos Libres con filtros, orden, mapa y conteo, y el detalle con sesión, con ubicación aproximada y tiempos de respuesta adecuados para móvil.

## 3. Precondiciones

1. NV-12 (cuartos y estados).
2. NV-11 (universidades con ubicación).
3. Decisión sobre la migración de NV-07 (§12).
4. Autorización «EJECUTA».

## 4. Alcance

1. `GET /rooms/search` con filtros y orden.
2. `GET /rooms/map` con marcadores.
3. `GET /rooms/count` para el presupuesto (RF-08).
4. `GET /rooms/{id}` con sesión (RF-24).
5. `GET /places/search` de colonias y ciudades (RF-25).
6. Ubicación aproximada determinista.

## 5. Fuera de alcance

- Geocodificación con servicios de pago.
- Smart Match y orden por compatibilidad: NV-23.
- Favoritos y comparación: NV-15.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/rooms/search` | Público | Cuartos Libres filtrados y paginados | 200, 304, 400, 429 |
| GET | `/api/v1/rooms/map` | Público | Marcadores en un área (máximo 300) | 200, 304, 400, 429 |
| GET | `/api/v1/rooms/count` | Público | Cuántos cuartos entran en un presupuesto | 200, 400, 429 |
| GET | `/api/v1/rooms/{id}` | Autenticado | Detalle del cuarto y de su edificio | 200, 400, 401, 404 |
| GET | `/api/v1/places/search` | Público | Colonias y ciudades cercanas a la universidad | 200, 400, 429 |

- Filtros: `universityId` (obligatorio), `minPrice`, `maxPrice`, `propertyType`, `modality` (`individual`, `shared`), `services[]`, `maxDistanceKm`; orden `sort` = `distance`, `price` o `newest`.
- Cada resultado: `id`, `title`, `price`, `distanceKm`, `walkingMinutes`, `approxLocation { lat, lng }`, `thumbnailUrl`, `verified`, `modality`.
- `rooms/map` recibe `universityId` y un `bbox`; devuelve `id`, `price`, `approxLocation`, `distanceKm`.
- El detalle incluye fotos, servicios, características, condiciones y ubicación aproximada; nunca la dirección exacta.

## 7. Modelo de datos

- Vista de lectura `room_search` (o consulta agregada) con `universityId`, `price`, `modality`, `propertyType`, `services`, `approxLocation` y `location` interna; índices `2dsphere` y compuestos por universidad y precio.
- `places`: colonias y ciudades por universidad con `name`, `nameNormalized`, `type`, `location`; seed controlado sin servicios externos.

## 8. Reglas de negocio

1. Solo cuartos Libres de edificios Verificados y arrendadores no suspendidos.
2. La ubicación aproximada se calcula de forma determinista (desplazamiento estable de 150–300 m por edificio) para que no se pueda promediar entre consultas.
3. La distancia se calcula siempre respecto a la universidad elegida.
4. Las respuestas públicas usan `ETag` y `Cache-Control: public, max-age=30`; el detalle autenticado usa `private, no-store`.
5. Presupuesto de rendimiento: p95 < 500 ms con 50 resultados (RNF-05).

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Ningún endpoint público devuelve dirección exacta, coordenadas reales, `ownerId` ni teléfono (RNF-02).
- Rate limit 60/min por IP en búsqueda y mapa; `bbox` y `pageSize` acotados para evitar extracción masiva.
- El detalle exige sesión (RF-24) y registra la vista para NV-25 sin datos personales.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-14-R01 | Búsqueda con filtros | Combina filtros de precio, tipo, modalidad, servicios y distancia; orden estable; paginación con máximo 50; solo cuartos publicables. | F1 | T01 |
| NV-14-R02 | Mapa de rentas | Marcadores dentro de un `bbox` válido, máximo 300, con precio y distancia a la universidad. | F1 | T02 |
| NV-14-R03 | Conteo por presupuesto | `rooms/count` devuelve el total de cuartos cercanos dentro del presupuesto con los mismos criterios de publicación. | F1 | T03 |
| NV-14-R04 | Detalle con sesión | Invitado recibe 401; con sesión devuelve el detalle público sin dirección exacta. | F1 | T04 |
| NV-14-R05 | Búsqueda de colonias y ciudades | Coincidencia sin acentos ni mayúsculas, máximo 10 resultados y lista vacía con 200 si no hay coincidencias. | F2 | T05 |
| NV-14-R06 | Índices y rendimiento | Índices geoespaciales y compuestos; prueba de carga básica que cumple p95 < 500 ms con datos de seed. | F2 | T06 |
| NV-14-R07 | Ubicación aproximada y anti-extracción | La ubicación aproximada es estable y está a 150–300 m de la real; límites de `bbox`, `pageSize` y rate limit verificados. | F3 | T07 |
| NV-14-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Migración de NV-07 | `/properties` queda como alias de `/rooms/search` durante una versión y se depreca | PENDIENTE |
| Fuente de colonias | Seed propio por ciudad (datos abiertos) sin geocodificación de pago | PROPUESTO |
| Detalle con sesión | Aplicar RF-24 (requiere sesión) aunque NV-07 lo expuso público en U2 | PENDIENTE |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Índices geoespaciales incluidos |
| Mapas en el cliente | $0 en el backend | El proveedor de mapas es decisión del cliente móvil |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-14-RK01 | Triangulación de la ubicación real | Privacidad del propietario | Desplazamiento determinista por edificio |
| NV-14-RK02 | Extracción masiva del catálogo | Abuso | Límites y rate limit |
| NV-14-RK03 | Consultas lentas | Mala experiencia | Índices y presupuesto de rendimiento |
| NV-14-RK04 | Ruptura de la app con el cambio de NV-07 | Pantallas vacías | Alias y deprecación |

## 15. Dependencias

- NV-12 (cuartos) y NV-13 (fotos).
- NV-11 (universidad y preferencias).
- NV-07 (contrato actual que se migra).
- NV-25 (registro de vistas).

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
