# NV-12 — Arrendador, edificios y cuartos

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-06, RF-13, RF-14, RF-16 a RF-22, RF-45, RF-76, RF-77, RF-78 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-08, RNF-09, RNF-10 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-10 y NV-11 |
| Relación | Fuente de los cuartos que publica NV-14; NV-13 (fotos y documentos) y NV-21 (verificación) completan la publicación; sustituye el modelo provisional de NV-07 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

Cualquier usuario puede publicar alojamientos desde su misma cuenta activando el modo arrendador. El propietario registra primero el edificio (datos compartidos, ubicación y servicios) y después genera los cuartos por piso, los configura y publica cuando al menos uno está listo. NV-07 expone hoy una colección provisional `properties` sin dueño ni estados.

## 2. Objetivo

Definir el alta de arrendador y la gestión completa de edificios y cuartos con sus estados, de modo que solo los cuartos Libres de edificios Verificados lleguen a la búsqueda pública.

## 3. Precondiciones

1. NV-10 (cuenta) y NV-11 (universidades con ubicación).
2. Decisión sobre la migración de NV-07 (§12).
3. Autorización «EJECUTA».

## 4. Alcance

1. Alta y consulta del perfil de arrendador (RF-06).
2. CRUD de edificios con estados (RF-13, RF-16, RF-17, RF-20, RF-21).
3. Generación de cuartos por pisos y datos base (RF-76, RF-77).
4. Edición, duplicado, pausa y eliminación de cuartos (RF-14, RF-18 a RF-21, RF-45).
5. Lista de pasos y solicitud de publicación (RF-78).
6. Tablero Mis alojamientos (RF-22).

## 5. Fuera de alcance

- Fotos y documentos: NV-13.
- Revisión y aprobación del edificio: NV-21.
- Búsqueda pública: NV-14.
- Cobro de rentas (RES-03).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/landlord/profile` | Autenticado | Activa el modo arrendador | 201, 400, 401, 409 |
| GET | `/api/v1/landlord/profile` | Arrendador | Perfil de arrendador | 200, 401, 404 |
| POST | `/api/v1/landlord/buildings` | Arrendador | Crea edificio en Borrador | 201, 400, 401, 403 |
| GET | `/api/v1/landlord/buildings` | Arrendador | Mis alojamientos con totales (RF-22) | 200, 401, 403 |
| GET/PATCH/DELETE | `/api/v1/landlord/buildings/{id}` | Dueño | Detalle, edición y borrado del edificio | 200, 204, 400, 404, 409 |
| POST | `/api/v1/landlord/buildings/{id}/rooms:generate` | Dueño | Crea cuartos por piso con datos base | 201, 400, 404, 409 |
| GET | `/api/v1/landlord/buildings/{id}/rooms` | Dueño | Cuartos del edificio | 200, 404 |
| PATCH/DELETE | `/api/v1/landlord/rooms/{id}` | Dueño | Edita o elimina un cuarto | 200, 204, 400, 404, 409 |
| POST | `/api/v1/landlord/rooms/{id}:duplicate \| :pause \| :resume \| :copy-from` | Dueño | Acciones sobre el cuarto | 200, 201, 400, 404, 409 |
| GET | `/api/v1/landlord/buildings/{id}/publication` | Dueño | Pasos pendientes para publicar | 200, 404 |
| POST | `/api/v1/landlord/buildings/{id}:publish` | Dueño | Envía a revisión (NV-21) | 202, 404, 409 |

- Perfil de arrendador: `fullName`, `phone` (E.164), `rentalMode` (`individual`, `agency`), `rfc` (obligatorio y validado si es `agency`), `acceptedLandlordTermsVersion`.
- Edificio: `type` (`rooms_building`, `house_rooms`, `full_apartment`), `name`, `address` (calle, número, colonia, ciudad, estado, CP), `location { lat, lng }`, `nearestUniversityId`, `allowedTenants` (`women`, `men`, `any`), `services[]`, `rules`.
- Cuarto: `number`, `floor`, `price` (MXN), `sizeM2`, `type` (`single`, `double`, `shared`), `bathroom` (`private`, `shared`), `furniture[]`, `availableFrom`, `minContract` (`four_months`, `six_months`, `annual`), `sharedMode`.
- `rooms:generate` recibe `floors` (1–30), `roomsPerFloor[]` y `base`; máximo 200 cuartos por edificio.

## 7. Modelo de datos

- `landlord_profiles`: `userId` (único), datos de la §6, `createdAt`.
- `buildings`: `ownerId`, datos, `location` GeoJSON con índice `2dsphere`, `distanceToUniversityKm`, `walkingMinutes`, `status`, `version`.
- `rooms`: `buildingId`, `ownerId`, datos del cuarto, `status`, `services` heredados por referencia; índices `{ buildingId, floor, number }` único y `{ status, price }`.

## 8. Reglas de negocio

1. Estados del edificio: Borrador → En revisión → Verificado; desde revisión puede volver a Borrador con correcciones (NV-21).
2. Estados del cuarto: Por configurar → Lista → En revisión → Libre ⇄ Pausado; Libre → Rentado (NV-18); solo los cuartos Libres de edificios Verificados son públicos.
3. Los servicios se definen una vez en el edificio y todos los cuartos los heredan (RF-17).
4. La distancia y los minutos a pie se calculan con haversine a 5 km/h respecto a la universidad elegida (RF-16).
5. Publicar exige datos del edificio completos, al menos un cuarto Listo y documentos cargados (NV-13); si falta algo, 409 con la lista de pasos.
6. No se elimina un cuarto Rentado ni un edificio con cuartos Rentados (409).
7. Editar precio o datos de un cuarto Libre no cambia su estado; editar datos sensibles del edificio Verificado lo regresa a revisión.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Todo endpoint `/landlord/*` exige perfil de arrendador y filtra por `ownerId = sub` (BOLA); un recurso ajeno responde 404.
- La dirección exacta y el teléfono nunca salen de los endpoints del dueño (RNF-02).
- Validación de RFC y teléfono; límites de tamaño en textos y listas.
- Operaciones de escritura con control de concurrencia optimista (`version`, 409 si cambió).

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Estado no permite la operación o publicación incompleta | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-12-R01 | Alta de arrendador | Activa el modo arrendador una sola vez por cuenta (409 si ya existe); valida RFC si es inmobiliaria y registra la aceptación de términos. | F1 | T01 |
| NV-12-R02 | Gestión de edificios | Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo y servicios; borrado bloqueado con cuartos Rentados. | F1 | T02 |
| NV-12-R03 | Generación y edición de cuartos | `rooms:generate` crea cuartos numerados por piso con datos base; edición, duplicado, copia de datos, pausa y eliminación respetan los estados. | F1 | T03 |
| NV-12-R04 | Máquina de estados | Transiciones válidas de edificio y cuarto implementadas en el dominio; transiciones inválidas 409; pruebas de cada transición. | F1 | T04 |
| NV-12-R05 | Cálculo de distancia | Distancia en km y minutos a pie a la universidad elegida, recalculados al cambiar la ubicación o la universidad. | F2 | T05 |
| NV-12-R06 | Persistencia e índices | Colecciones con índices únicos y geoespaciales; concurrencia optimista con `version`. | F2 | T06 |
| NV-12-R07 | Publicación y tablero | `publication` lista pasos pendientes; `:publish` pasa a En revisión solo si se cumplen; Mis alojamientos devuelve totales, libres, ocupación e ingreso mensual. | F2 | T07 |
| NV-12-R08 | Autorización del dueño | Ningún endpoint permite leer o modificar edificios o cuartos ajenos; pruebas con dos arrendadores. | F3 | T08 |
| NV-12-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Migración de NV-07 | El listado público pasa a leer cuartos Libres (NV-14); `GET /properties` se mantiene como alias temporal y se depreca con aviso | PENDIENTE |
| Límite de cuartos | 200 por edificio | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Volumen estimado dentro de 512 MB |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-12-RK01 | Exponer dirección exacta | Privacidad y seguridad del propietario | Proyecciones separadas para dueño y público |
| NV-12-RK02 | Estados inconsistentes | Cuartos visibles sin verificar | Máquina de estados en el dominio y pruebas |
| NV-12-RK03 | Generación masiva abusiva | Saturación de la base | Límite de pisos y cuartos y rate limit |
| NV-12-RK04 | Ruptura del contrato de NV-07 | App móvil afectada | Alias y deprecación anunciada |

## 15. Dependencias

- NV-10 y NV-11.
- NV-13 (fotos y documentos) para publicar.
- NV-21 (revisión) para Verificado.
- NV-14 (búsqueda) y NV-18 (Rentado).

## 16. Trazabilidad y cobertura

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

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
