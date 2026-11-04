# NV-08 — Evidencia University Catalog U2

## Estado

**✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

Resultado:

**PASS WITH WARNINGS**

## Endpoints

### Listado

`GET /api/v1/universities`

Validado:

- `page` default `1`;
- `pageSize` default `20`;
- `page >= 1`;
- `pageSize` entre `1` y `50`;
- resultado vacío → `200`;
- página fuera de rango → `200` con `items: []`;
- `totalItems`;
- `totalPages`;
- parámetros desconocidos ignorados;
- orden estable `name ASC, _id ASC`.

### Detalle

`GET /api/v1/universities/{id}`

Validado:

- ObjectId hexadecimal de 24 caracteres;
- `200` para universidad existente;
- `400` para identificador inválido;
- `404` para ObjectId válido inexistente;
- solamente campos públicos.

## Contrato público

Los DTO públicos contienen exclusivamente:

- id;
- code;
- name;
- city;
- country;
- createdAt.

Se verificó mediante serialización y pruebas de seguridad que las respuestas no expongan:

- ownerId;
- exactAddress;
- address;
- latitude;
- longitude;
- contactEmail;
- email;
- phone;
- telephone;
- moderationStatus;
- connectionString.

## Seed U2

Dataset definido:

### UTTT

- Universidad Tecnológica de Tula-Tepeji
- Tula de Allende
- México

### UAEH

- Universidad Autónoma del Estado de Hidalgo
- Pachuca de Soto
- México

### ITSOEH

- Instituto Tecnológico Superior del Occidente del Estado de Hidalgo
- Mixquiahuala de Juárez
- México

El seed usa una fecha reproducible:

`2026-11-02T00:00:00Z`

Se implementó upsert por:

`code`

Las pruebas aisladas verificaron:

- exactamente tres universidades del seed;
- códigos únicos;
- dos ejecuciones no producen duplicados;
- registros ajenos al seed no son eliminados;
- un registro existente con el mismo `code` se actualiza;
- su identificador se conserva;
- `createdAt` existente se conserva;
- no existen datos personales;
- el seed está deshabilitado por defecto.

## Seed real

**NO EJECUTADO**

No se ejecutó:

`--seed-universities`

contra MongoDB real.

UniversitySeedService se probó únicamente mediante `UniversityTestStore` aislado.

La ejecución real queda pendiente para el entorno autorizado de NV-03.

## MongoDB

Colección:

`universities`

Implementado:

- repositorio de solo lectura para endpoints;
- proyección explícita;
- sin N+1;
- índice único `{ code: 1 }`;
- índice de orden `{ name: 1, _id: 1 }`;
- orden `name ASC, _id ASC`;
- upsert del seed por `code`;
- `SetOnInsert` para conservar `createdAt`;
- fail-closed ante indisponibilidad.

## Rate limiting

University Catalog utiliza:

`60 solicitudes / 60 segundos / IP`

Se validó:

- listado y detalle comparten límite;
- estado compartido entre réplicas simuladas;
- exceso → `429`;
- `Retry-After: 60`;
- fallo del rate-limit store → `503`;
- sin fallback en memoria.

## Problem Details

Se validaron títulos:

- `400 Validation failed`;
- `404 Not found`;
- `429 Too many requests`;
- `503 Service unavailable`;
- `500 Internal server error`.

Los errores usan:

`application/problem+json`

y contienen:

`traceId`

Las respuestas de error no exponen:

- stack traces;
- rutas locales;
- MongoDB;
- mongodb;
- Redis;
- connection strings;
- información privada.

## OpenAPI

Se incorporaron:

- `/api/v1/universities`
- `/api/v1/universities/{id}`

Las rutas existentes de NV-01, NV-04, NV-05, NV-06 y NV-07 continúan autorizadas.

No se agregó funcionalidad fuera del alcance aprobado.

## Pruebas NV-08

### T08 — Unitarias

Se agregaron:

**22 pruebas**

Cobertura:

- page;
- pageSize;
- límites;
- defaults;
- totalPages;
- ObjectId;
- mapping;
- DTO;
- serialización pública;
- dataset del seed;
- códigos únicos;
- nombres y ciudades;
- país;
- fecha determinista;
- idempotencia lógica;
- ausencia de campos personales.

### T09 — Integración

Se agregaron pruebas para:

- listado con datos;
- orden `name ASC, _id ASC`;
- desempate por `_id`;
- defaults;
- paginación;
- listado vacío;
- página fuera de rango;
- query desconocida;
- validaciones 400;
- detalle 200;
- detalle 404;
- dependencia no disponible 503;
- rate limit 429;
- Retry-After 60;
- seed idempotente;
- conservación de registros ajenos;
- upsert por code;
- conservación de Id y createdAt.

### T10 — Seguridad

Se validó:

- ausencia de campos privados;
- errores sanitizados;
- títulos Problem Details correctos;
- sin stack trace;
- sin rutas locales;
- sin MongoDB/Redis/connection strings;
- fallo cerrado del rate-limit store;
- límite compartido entre listado y detalle;
- límite compartido entre réplicas simuladas;
- seed deshabilitado por defecto.

## Resultado total de pruebas

Comando:

`dotnet test TheHouse.slnx --no-build`

Resultado:

**378 / 378 PASS**

Distribución:

- Unit: 130;
- Security: 28;
- Integration: 220.

Los 303 tests existentes antes de NV-08 continúan pasando.

## Build local

Comando:

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

`cb500ed`

Run:

`41781504530`

Job:

`126413868062`

Resultado:

**SUCCESS**

Artifact:

`u2-test-results`

## Matriz de requisitos

| Requisito | Resultado |
| --- | --- |
| NV-08-R01 — listado paginado | PASS |
| NV-08-R02 — detalle | PASS |
| NV-08-R03 — validación | PASS |
| NV-08-R04 — seed idempotente | PASS WITH WARNING |
| NV-08-R05 — MongoDB e índices | PASS WITH WARNING |
| NV-08-R06 — Problem Details y rate limit | PASS |
| NV-08-R07 — pruebas y evidencia | PASS |

## Brechas declaradas

### W01 — MongoDB y Redis reales

MongoDB y Redis reales no fueron utilizados durante las pruebas de NV-08.

No se utilizó Docker ni Testcontainers.

MongoUniversityRepository y la semántica de Redis se validaron mediante código, pruebas aisladas y CI.

La verificación desplegada corresponde a NV-03.

### W02 — Collation de MongoDB

Las pruebas aisladas reproducen:

`name ASC, _id ASC`

usando orden ordinal.

MongoDB real puede presentar diferencias de orden con caracteres acentuados dependiendo de la collation configurada.

La validación del comportamiento real queda pendiente de NV-03.

### W03 — Seed real

El seed real no fue ejecutado.

La idempotencia se verificó únicamente mediante el repositorio aislado de pruebas.

La ejecución controlada y sus conteos antes/después se documentarán durante NV-03.

## Seguridad

Resultado:

**PASS**

Hallazgos abiertos:

- Critical: 0;
- High: 0.

## Decisión final

**PASS WITH WARNINGS**

NV-08 cumple el alcance funcional, de seguridad y testing requerido para U2.

Las brechas restantes pertenecen a la verificación desplegada de NV-03.

**NV-08 queda EJECUTADA, VALIDADA Y CERRADA PARA U2.**
