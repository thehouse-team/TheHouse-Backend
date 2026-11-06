# NV-07 — Evidencia Property Core U2

## Estado

**✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

Resultado:

**PASS WITH WARNINGS**

## Endpoints

### Listado

`GET /api/v1/properties`

Validado:

- `page` default `1`;
- `pageSize` default `20`;
- `page >= 1`;
- `pageSize` entre `1` y `50`;
- resultado vacío: `200`;
- página fuera de rango: `200` con `items: []`;
- `totalItems`;
- `totalPages`;
- orden estable `createdAt DESC, _id DESC`.

### Detalle

`GET /api/v1/properties/{id}`

Validado:

- ObjectId hexadecimal de 24 caracteres;
- `200` para propiedad existente;
- `400` para identificador inválido;
- `404` para ObjectId válido inexistente.

## Datos públicos

Las respuestas públicas contienen únicamente los campos aprobados por NV-07.

Listado:

- id;
- title;
- price;
- currency;
- city;
- universityId;
- bedrooms;
- bathrooms;
- areaM2;
- createdAt.

Detalle agrega:

- description;
- amenities.

Se verificó que las respuestas no expongan:

- ownerId;
- exactAddress;
- address;
- latitude;
- longitude;
- contactEmail;
- moderationStatus.

## MongoDB

Colección:

`properties`

Se implementó:

- repositorio de solo lectura;
- proyección explícita;
- sin N+1;
- orden `createdAt DESC, _id DESC`;
- índice compuesto `createdAt DESC, _id DESC`;
- fail-closed ante indisponibilidad.

La implementación no devuelve documentos MongoDB completos.

## Rate limiting

Properties utiliza:

`60 solicitudes / 60 segundos / IP`

mediante el store Redis compartido.

Se validó:

- listado y detalle comparten el mismo límite por IP;
- múltiples réplicas comparten el mismo estado en pruebas;
- exceso devuelve `429`;
- `Retry-After: 60`;
- fallo del store produce `503`;
- no existe fallback en memoria.

## Problem Details

Se validaron:

- `400 Validación fallida`;
- `404 Recurso no encontrado`;
- `429 Demasiadas solicitudes`;
- `503 Servicio no disponible`;
- `500 Error interno del servidor`.

Los errores usan:

`application/problem+json`

y contienen `traceId`.

No exponen:

- stack trace;
- rutas locales;
- detalles MongoDB;
- connection strings;
- Redis;
- campos privados.

## OpenAPI

Se autorizaron únicamente los endpoints nuevos de NV-07:

- `/api/v1/properties`
- `/api/v1/properties/{id}`

Los contratos heredados de NV-01/NV-04/NV-05/NV-06 fueron actualizados sin cambiar su comportamiento funcional.

NV-08 Universities continúa sin exponerse.

## Pruebas

### Unit

Resultado:

**92 PASS**

NV-07 agregó 26 pruebas para:

- page;
- pageSize;
- totalPages;
- límites;
- ObjectId;
- DTOs;
- mapping;
- serialización pública.

### Security

Resultado:

**28 PASS**

La suite global de seguridad continúa verde.

NV-07 agregó validaciones específicas dentro de integración para ausencia de campos privados y errores sanitizados.

### Integration

Resultado:

**183 PASS**

NV-07 agregó pruebas para:

- listado con datos;
- listado vacío;
- página fuera de rango;
- defaults;
- detalle 200;
- detalle 404;
- id inválido 400;
- paginación inválida 400;
- query duplicada 400;
- 429;
- Retry-After;
- Mongo/Redis fail-closed 503;
- rate limit compartido;
- comportamiento multi-réplica;
- errores sanitizados.

## Resultado total

`dotnet test TheHouse.slnx --no-build`

Resultado local:

**303 / 303 PASS**

Distribución:

- Unit: 92;
- Security: 28;
- Integration: 183.

## Build

`dotnet build TheHouse.slnx`

Resultado:

- 0 errores;
- 0 advertencias.

## GitHub Actions

Workflow:

`U2 Linux Validation`

Branch:

`feature/u2-entrega`

Commit técnico:

`faafa46`

Run:

`41776038583`

Job:

`126396500905`

Resultado:

**SUCCESS**

Artifact:

`u2-test-results`

## Matriz de evidencia

| Requisito | Resultado |
| --- | --- |
| R01 listado paginado | PASS |
| R02 detalle | PASS |
| R03 validación | PASS |
| R04 MongoDB/proyección/índice | PASS WITH WARNING |
| R05 datos públicos seguros | PASS |
| R06 Problem Details/rate limit | PASS |
| R07 testing/evidencia | PASS |

## Brechas

### W01 — MongoDB real

MongoPropertyRepository no ha sido validado todavía contra una instancia MongoDB cloud real.

La implementación está validada mediante revisión, build, unitarias e integración aislada.

La comprobación real corresponde a NV-03.

### W02 — Orden real e índice

La consulta e índice:

`createdAt DESC, _id DESC`

están implementados y reproducidos por el fake de integración.

Su validación final con datos reales queda pendiente del entorno desplegado de NV-03.

## Seguridad

Resultado:

**PASS**

No existen hallazgos:

- Critical;
- High.

## Decisión final

**PASS WITH WARNINGS**

NV-07 cumple el alcance funcional y de seguridad aprobado para U2.

Las advertencias restantes pertenecen a la validación desplegada de NV-03.

**NV-07 queda EJECUTADA, VALIDADA Y CERRADA PARA U2.**

## Addendum — Correcciones de revisión U2 (2026-11-05)

Correcciones autorizadas por Victor Manuel Jose Jose el 2026-11-05 en la rama `fix/u2-review`. Este addendum documenta la corrección del punto 4 de NV-07: el seed controlado de propiedades.

- Problem Details de propiedades: títulos en español y consistentes (`Validación fallida`, `Recurso no encontrado`, `Demasiadas solicitudes`, `Servicio no disponible`). El contrato omite `detail` e `instance` en estos endpoints.
- Seed de propiedades: seis registros de demostración con ObjectIds fijos `000000000000000000000001` a `000000000000000000000006`; `upsert` idempotente por `_id`; `createdAt` reproducible `2026-11-02T00:00:00Z` conservado con `SetOnInsert`; no elimina propiedades ajenas al seed.
- Vinculación: cada propiedad referencia una universidad del seed NV-08 por `code` (`UTTT`, `UAEH`, `ITSOEH`). Si falta la universidad requerida, el seed falla de forma cerrada y no escribe propiedades parciales.
- Activación: solo mediante `--seed-properties` con `PropertySeed__Enabled=true`; el seed de universidades debe ejecutarse primero. En `Production` se rechaza salvo con `--allow-production-seed`, registrado en el log (M-BE-043), y por defecto está deshabilitado en `appsettings.json`.
- Pruebas: `PropertySeedTests` cubre creación de seis propiedades vinculadas, idempotencia, preservación de `createdAt`, conservación de registros ajenos, requisito del seed de universidades, deshabilitado por defecto y fallo cerrado del store.
- Seed real: **NO EJECUTADO** contra MongoDB ni Azure. La idempotencia se verificó con `PropertyTestStore` aislado con la misma semántica de `upsert` por `_id`.
- Verificación ejecutada: `dotnet build --verbosity quiet` con 0 errores y 0 advertencias; `dotnet test --verbosity quiet` con 130 pruebas unitarias, 39 de seguridad y 227 de integración: 396 aprobadas y 0 fallidas.
- Nota de entorno: `TheHouse/appsettings.Development.json` es un archivo local ignorado por Git, no forma parte del repositorio y habilita JWT sin `JWT_SIGNING_KEY`; para ejecutar la suite se excluyó temporalmente y se restauró byte a byte sin incorporarlo al commit. Esta limitación se declara para no atribuir a la suite una ejecución que dependa de configuración no versionada.
- Seguridad: revisión de OWASP API Security Top 10, secretos, autenticación, autorización, validación de entrada, exposición de datos, errores, logging, rate limiting y mass assignment. Sin hallazgos Critical/High ni secretos confirmados en el alcance revisado. Resultado: PASS WITH WARNINGS. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación ni performance.
- SDD: la revisión no crea tasks, no reabre el cierre U2 y no autoriza implementación adicional. Resultado: PASS WITH WARNINGS.

## Addendum 1.1.0-u2 — Caché HTTP condicional (T11 y T12)

Ejecutado el 2026-11-05 en la rama `fix/u2-review` con la autorización humana «EJECUTA» de Victor Manuel Jose Jose (M-BE-046).

- Implementación: `TheHouse/Http/HttpCaching.cs` serializa la respuesta con las opciones JSON del host, calcula un `ETag` débil (SHA-256 en base64url del cuerpo sin comprimir), envía `Cache-Control: public, max-age=60` y `Vary: Accept-Encoding`, y responde 304 sin cuerpo cuando `If-None-Match` coincide (comparación débil, listas y `*`).
- `max-age` configurable con `Foundation:PublicCacheMaxAgeSeconds` (0–300, por defecto 60); valores fuera de rango impiden el arranque.
- Corrección encontrada al probar: los errores de este endpoint no enviaban `Cache-Control: no-store`. Ahora todos los errores (400/404/429/503) lo envían y nunca llevan `ETag` (M-BE-048).
- OpenAPI documenta la respuesta 304. El contrato JSON no cambia.
- Pruebas (`HttpCachingTests`, `ConfigurationTests`): `ETag` débil y `Cache-Control` en 200, mismo `ETag` para el mismo contenido, 304 sin cuerpo con `If-None-Match`, lista y `*`, comparación débil, `ETag` nuevo cuando cambia el contenido, 200 sin coincidencia, errores sin `ETag` y con `no-store`, el 304 cuenta para el rate limit, `ETag` independiente de `Accept-Encoding` y rechazo de `max-age` fuera de rango.
- Resultado local: `dotnet build` 0 errores y 0 advertencias; `dotnet test` 429 aprobadas, 0 fallidas (130 unitarias, 47 de seguridad, 252 de integración), con `appsettings.Development.json` presente.
- Pendiente: la verificación contra Azure requiere publicar y desplegar una imagen nueva; la imagen actual (`c651886`) no incluye este cambio.
