# NV-11 — Perfil, universidad y preferencias

| Campo | Valor |
| --- | --- |
| ID | NV-11 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-11-profile-preferences/`; el seed de coordenadas en Production requiere autorización aparte, M-BE-043) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-05, RF-07 (búsqueda y selección), RF-08 a RF-12, RF-72; base del perfil de RF-48 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-04, RNF-09, RNF-10, RNF-11 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Profile |
| Precondición | NV-08 (universidades) y NV-10 (sesión) desplegadas |
| Relación | NV-14 (búsqueda) y NV-23 (Smart Match) consumen las preferencias y la universidad; el esquema de preferencias es el contrato de entrada del modelo de `TheHouse-ML` |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-07):** se completa al nivel de NV-15 y NV-17 antes de su aprobación: contrato para Mobile (§6.1); códigos 413, 415, 429 y 503 en todos los endpoints; rate limit por usuario; catálogos cerrados de servicios y tipos; `quickSetupCompleted` lo calcula el servidor; la universidad reutiliza `code` como siglas y solo agrega `location` y `searchKey` (sin índice de texto, que no encuentra coincidencias parciales); los eventos `PreferencesChanged` y `UserDeleted` salen de la v1 porque todavía no hay consumidores. Las preferencias son la entrada del modelo de Smart Match que se entrena en el repositorio `TheHouse-ML`.

## 1. Contexto

La app necesita guardar el perfil del estudiante, su universidad y sus preferencias de búsqueda (presupuesto, distancia, servicios, con quién vivir, tipo de lugar y extras) para personalizar la búsqueda y Smart Match. Sin preferencias, Smart Match no tiene con qué calcular. El catálogo de universidades (NV-08) tiene 10 universidades regionales con `code` (siglas), `name` y `city`, pero sin coordenadas; RF-07 pide buscar por siglas, nombre o ciudad y encontrar la más cercana por ubicación.

## 2. Objetivo

Exponer el perfil editable, la selección de universidad, la búsqueda de universidades por texto o cercanía y las preferencias rápidas y completas como fuente única para búsqueda y Smart Match.

## 3. Precondiciones

1. NV-08 y NV-10 desplegadas.
2. Coordenadas de las 10 universidades del seed verificadas y cargadas con un seed autorizado en producción (M-BE-043).
3. Validación del contrato §6.1 con el responsable de Mobile (nombres de campos y valores de los catálogos).
4. Autorización «EJECUTA».

## 4. Alcance

1. `GET` y `PATCH /users/me/profile`.
2. `PUT /users/me/university`.
3. `GET /universities/search` por texto (siglas, nombre o ciudad) o por cercanía.
4. `GET` y `PUT /users/me/preferences` con preferencias rápidas (RF-72) y completas (RF-08 a RF-12).
5. Extensión aditiva de `universities` (NV-08) con `location` y `searchKey`, y actualización del seed.
6. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Preferencias de invitados: se guardan en el dispositivo; el backend no las persiste (Smart Match las recibe en el cuerpo, NV-23).
- Conteo de rentas dentro del presupuesto (RF-08): NV-14.
- Foto de perfil.
- Eventos `PreferencesChanged` y `UserDeleted`: se agregan cuando exista un consumidor (NV-23 y eliminación de cuenta).
- Cambios en `GET /universities`, `GET /universities/{id}` y `GET /users/me`: quedan igual.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me/profile` | Autenticado | Perfil propio con la universidad elegida | 200, 401, 429, 503 |
| PATCH | `/api/v1/users/me/profile` | Autenticado | Actualiza `name` | 200, 400, 401, 413, 415, 429, 503 |
| PUT | `/api/v1/users/me/university` | Autenticado | Guarda la universidad elegida `{ universityId }` | 204, 400, 401, 404, 413, 415, 429, 503 |
| GET | `/api/v1/universities/search` | Público | Busca por `q` (siglas, nombre o ciudad) o por `lat`/`lng` | 200, 400, 429, 503 |
| GET | `/api/v1/users/me/preferences` | Autenticado | Preferencias guardadas o valores por defecto | 200, 401, 429, 503 |
| PUT | `/api/v1/users/me/preferences` | Autenticado | Reemplaza las preferencias | 200, 400, 401, 413, 415, 429, 503 |

- Perfil: `{ name, university: { id, code, name, city } | null, quickSetupCompleted, updatedAt }`. `name` de 1 a 80 caracteres sin caracteres de control; `null` si nunca se capturó.
- Preferencias: `{ budget: { min, max, currency: "MXN" }, maxDistanceKm, essentialServices: [..], livingWith, placeType, pets, transport, propertyTypes: [..], quickSetupCompleted, version, updatedAt }`.
- `budget.min` y `budget.max` enteros de 0 a 50 000 con `min` ≤ `max`; `currency` solo `MXN`; `maxDistanceKm` de 0.5 a 30 con un decimal.
- `essentialServices` (sin repetidos, máximo 11): `wifi`, `water`, `electricity`, `gas`, `hot_water`, `laundry`, `kitchen`, `furnished`, `parking`, `security`, `cleaning`. `livingWith`: `women`, `men`, `any`. `placeType`: `room`, `room_private_bath`, `studio`, `shared`. `transport`: `walk`, `bike`, `public`, `car`. `propertyTypes` (sin repetidos): `house`, `apartment`, `student_residence`. `pets`: booleano.
- Todos los campos de preferencias son opcionales (`null` o lista vacía). `quickSetupCompleted` y `version` los calcula el servidor: el cliente no los envía (400 si lo hace). `quickSetupCompleted` es `true` cuando hay `budget.max`, `livingWith` y `placeType`.
- `GET /users/me/preferences` sin preferencias guardadas responde 200 con todo en `null` o vacío, `quickSetupCompleted=false` y `version=0`.
- `universities/search`: exactamente uno de los dos modos; `q` de 2 a 60 caracteres, o `lat` (-90 a 90) y `lng` (-180 a 180). Máximo 20 resultados en `{ items: [{ id, code, name, city, distanceKm }] }`; `distanceKm` solo en el modo por cercanía, ordenado de menor a mayor y con un decimal.
- Valores desconocidos, repetidos o fuera de rango → 400. Los cuerpos son `application/json` (415 si no) de hasta 2 KB (413).
- Respuestas autenticadas con `Cache-Control: no-store`; la búsqueda pública con el mismo caché condicional de NV-08 (M-BE-046).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/profile
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 { name, university: { id, code, name, city } | null, quickSetupCompleted, updatedAt }
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/profile
METHOD       PATCH
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Content-Type: application/json
REQUEST      { "name": "1 a 80 caracteres" }
RESPONSE     200 perfil actualizado
STATUS CODES 200, 400, 401, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/university
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Content-Type: application/json
REQUEST      { "universityId": "<24 hex>" }
RESPONSE     204 sin cuerpo (también si ya era la elegida)
STATUS CODES 204, 400, 401, 404 universidad inexistente, 413, 415, 429, 503
PAGINATION   No
```

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

```text
ENDPOINT     /api/v1/users/me/preferences
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 { budget: { min, max, currency: "MXN" }, maxDistanceKm, essentialServices: [..], livingWith, placeType, pets, transport, propertyTypes: [..], quickSetupCompleted, version, updatedAt }
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/preferences
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Content-Type: application/json
REQUEST      preferencias sin quickSetupCompleted, version ni updatedAt (ver §6)
RESPONSE     200 preferencias guardadas con version incrementada
STATUS CODES 200, 400, 401, 413, 415, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Profile** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `user_profiles`, `user_preferences` |
| Usa (puertos de otros módulos) | Catalog `IUniversityQuery` (NV-08) para validar la universidad y buscar |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `user_profiles`: `userId` (único), `name`, `universityId`, `updatedAt`.
- `user_preferences`: `userId` (único), campos de la §6, `quickSetupCompleted`, `version` (se incrementa en el servidor en cada `PUT`, para invalidar cálculos de Smart Match), `updatedAt`.
- `universities` (NV-08) agrega `location` (GeoJSON `Point`) con índice `2dsphere` y `searchKey` (siglas, nombre y ciudad en minúsculas y sin acentos). `code` ya cubre las siglas.

## 9. Reglas de negocio

1. El usuario sale del `sub` del token; no hay rutas ni campos con `userId`.
2. Los catálogos de §6 son cerrados; un valor nuevo requiere cambio de spec y aviso a Mobile.
3. `PUT` de preferencias reemplaza el documento completo e incrementa `version` de forma atómica.
4. `quickSetupCompleted` se deriva en el servidor: `budget.max`, `livingWith` y `placeType` presentes.
5. `PUT /users/me/university` solo acepta universidades del catálogo; repetirla responde 204.
6. La búsqueda por texto normaliza `q` igual que `searchKey` y busca coincidencias parciales con el texto escapado; la búsqueda por cercanía usa el índice `2dsphere`.
7. Las coordenadas de búsqueda no se guardan ni se registran.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Solo el dueño lee o modifica su perfil y preferencias (filtro por `sub`); no hay forma de pedir los de otro usuario.
- Rate limit en Redis de 60 solicitudes por 60 s por usuario para perfil y preferencias, y de 60 por 60 s por IP para la búsqueda pública; si Redis falla, 503.
- El texto de búsqueda se escapa antes de usarlo como patrón (sin inyección de expresiones regulares ni ReDoS).
- `name` es dato personal: nunca aparece en logs.
- Las coordenadas enviadas para buscar universidad no se registran en logs ni se persisten (RNF-02, RNF-11).

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Cuerpo mayor a 2 KB | 413 | Solicitud demasiado grande |
| Contenido distinto de `application/json` | 415 | Tipo de contenido no admitido |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-11-R01 | Perfil propio | `GET`/`PATCH /users/me/profile`; `name` de 1 a 80 caracteres; campos no permitidos 400; perfil inexistente responde 200 con valores vacíos. | F1 | T01 |
| NV-11-R02 | Selección de universidad | `PUT /users/me/university` valida que el id exista (404 si no; 400 si el formato es inválido), es idempotente y se refleja en `GET /users/me/profile`. | F1 | T02 |
| NV-11-R03 | Preferencias de búsqueda | `PUT` valida rangos, catálogos y repetidos; rechaza `quickSetupCompleted` y `version` del cliente; incrementa `version`; deriva `quickSetupCompleted`; `GET` sin datos devuelve los valores por defecto. | F1 | T03 |
| NV-11-R04 | Búsqueda de universidades por texto | Encuentra por siglas, nombre o ciudad, parcial y sin distinguir acentos ni mayúsculas (`tula`, `UAEH`, `tepeji`); máximo 20; `q` fuera de 2–60 o combinado con coordenadas 400. | F2 | T04 |
| NV-11-R05 | Búsqueda por cercanía | Con `lat`/`lng` devuelve las universidades más cercanas con `distanceKm`, ordenadas; las 10 del seed tienen coordenadas verificadas y cargadas con seed autorizado. | F2 | T05 |
| NV-11-R06 | Persistencia e índices | Documento único por usuario en perfil y preferencias; índice `2dsphere` y `searchKey` en universidades; `GET /universities` y `/universities/{id}` sin cambios de contrato. | F2 | T06 |
| NV-11-R07 | Autorización, límites y privacidad | Sin `userId` del cliente; pruebas con dos usuarios; rate limit 429 con `Retry-After`; Redis caído 503; `no-store`; las coordenadas y el nombre no aparecen en logs (prueba que lo verifica). | F3 | T07 |
| NV-11-R08 | Contrato para Mobile y despliegue | Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (perfil, universidad, búsqueda por texto y cercanía, preferencias). | F4 | T08 |
| NV-11-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Siglas de universidad | Reutilizar `code` de NV-08; no agregar `acronym` | APROBADO (M-BE-063) |
| Extensión de `universities` | Agregar `location` y `searchKey` de forma aditiva; respuestas de NV-08 sin cambios | APROBADO (M-BE-063) |
| Coordenadas del seed | Punto del campus tomado de OpenStreetMap y verificado; carga con seed autorizado (M-BE-043) | APROBADO (M-BE-063) |
| Búsqueda por texto | `searchKey` normalizado con coincidencia parcial en lugar de índice de texto (catálogo pequeño) | APROBADO (M-BE-063) |
| Catálogos de preferencias | Valores de la §6, a confirmar con Mobile | APROBADO (M-BE-063) |
| `quickSetupCompleted` | Derivado en el servidor | APROBADO (M-BE-063) |
| Moneda | MXN única en la primera versión | APROBADO (M-BE-063) |
| Eventos de dominio | Fuera de v1 hasta que exista un consumidor | APROBADO (M-BE-063) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Dos colecciones pequeñas y un índice `2dsphere` sobre 10 universidades; un comando Redis por solicitud |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-11-RK01 | Preferencias inconsistentes con Smart Match | Recomendaciones erróneas | Catálogos cerrados, `version` y el mismo esquema como contrato de entrada del modelo de `TheHouse-ML` |
| NV-11-RK02 | Fuga de ubicación del usuario | Privacidad | No persistir ni registrar coordenadas; prueba de logs |
| NV-11-RK03 | Coordenadas equivocadas de universidades | Resultados de cercanía erróneos | Verificación manual de cada punto y prueba de orden por distancia |
| NV-11-RK04 | Catálogos distintos en la app | Errores 400 en Mobile | Validar §6.1 con Mobile antes de aprobar |

## 16. Dependencias

- NV-08 (catálogo de universidades) y NV-10 (sesión).
- NV-14 y NV-23 consumen preferencias y universidad.
- `TheHouse-ML` usa el esquema de preferencias como entrada del modelo.

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
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
