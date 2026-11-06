# NV-11 — Perfil, universidad y preferencias

| Campo | Valor |
| --- | --- |
| ID | NV-11 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-05, RF-07 (búsqueda y selección), RF-08 a RF-12, RF-72; base del perfil de RF-48 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-04, RNF-09, RNF-10, RNF-11 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-08 y NV-10 |
| Relación | NV-23 (Smart Match) consume las preferencias; NV-14 usa la universidad seleccionada |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

La app necesita guardar el perfil del usuario, su universidad y las preferencias de búsqueda (presupuesto, distancia, servicios, con quién vivir, tipo de lugar y extras) para personalizar el mapa y Smart Match. El catálogo de universidades (NV-08) solo lista y consulta por id; RF-07 pide buscar por siglas, nombre o ciudad y encontrar la más cercana por ubicación.

## 2. Objetivo

Definir el perfil editable, la selección de universidad y las preferencias rápidas y completas como fuente única para búsqueda y Smart Match.

## 3. Precondiciones

1. NV-10 (`/users/me`).
2. Extensión aditiva de NV-08 con `acronym`, `city` normalizada y `location` (GeoJSON).
3. Autorización «EJECUTA».

## 4. Alcance

1. `GET` y `PATCH /users/me/profile`.
2. `PUT /users/me/university`.
3. `GET /universities/search` por texto y por cercanía.
4. `GET` y `PUT /users/me/preferences` con preferencias rápidas (RF-72) y completas (RF-08 a RF-12).

## 5. Fuera de alcance

- Preferencias de invitados: se guardan en el dispositivo; el backend no las persiste (Smart Match las recibe en el cuerpo, NV-23).
- Conteo de rentas dentro del presupuesto (RF-08): NV-14.
- Foto de perfil.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me/profile` | Autenticado | Perfil: name, universityId, quickSetupCompleted | 200, 401 |
| PATCH | `/api/v1/users/me/profile` | Autenticado | Actualiza `name` | 200, 400, 401 |
| PUT | `/api/v1/users/me/university` | Autenticado | Guarda la universidad elegida `{ universityId }` | 204, 400, 401, 404 |
| GET | `/api/v1/universities/search` | Público | Busca por `q` (siglas, nombre o ciudad) o por `lat`/`lng` | 200, 400, 429 |
| GET | `/api/v1/users/me/preferences` | Autenticado | Preferencias guardadas | 200, 401 |
| PUT | `/api/v1/users/me/preferences` | Autenticado | Reemplaza las preferencias | 200, 400, 401 |

- Preferencias: `budget { min, max, currency }`, `maxDistanceKm` (0.5–30), `essentialServices[]`, `livingWith` (`women`, `men`, `any`), `placeType` (`room`, `room_private_bath`, `studio`, `shared`), `pets`, `transport` (`walk`, `bike`, `public`, `car`), `propertyTypes[]`, `quickSetupCompleted`.
- `universities/search`: `q` de 2 a 60 caracteres o `lat` y `lng` válidos; máximo 20 resultados; con coordenadas ordena por distancia y devuelve `distanceKm`.
- Las coordenadas del usuario no se almacenan.

## 7. Modelo de datos

- `user_profiles`: `userId` (único), `name`, `universityId`, `updatedAt`.
- `user_preferences`: `userId` (único), campos de la §6, `version` (para invalidar cálculos de Smart Match), `updatedAt`.
- `universities` (NV-08) agrega `acronym`, `cityNormalized`, `location` con índice `2dsphere` e índice de texto en `acronym`, `name` y `city`.

## 8. Reglas de negocio

1. Los servicios y enumeraciones se validan contra catálogos cerrados; valores desconocidos 400.
2. `budget.min` ≤ `budget.max`; moneda MXN por defecto.
3. Cada `PUT` de preferencias incrementa `version`.
4. Las preferencias rápidas (presupuesto, con quién vivir y tipo de lugar) marcan `quickSetupCompleted=true` y pueden completarse después.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Solo el dueño lee o modifica su perfil y preferencias (filtro por `sub`).
- La búsqueda pública de universidades tiene rate limit 60/min por IP y no expone campos internos.
- Las coordenadas enviadas para buscar universidad no se registran en logs ni se persisten (RNF-02, RNF-11).

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
| NV-11-R01 | Perfil propio | `GET`/`PATCH /users/me/profile` con `name` de 1 a 80 caracteres; solo campos permitidos. | F1 | T01 |
| NV-11-R02 | Selección de universidad | `PUT /users/me/university` valida que el id exista en el catálogo; 404 si no; se refleja en el perfil. | F1 | T02 |
| NV-11-R03 | Búsqueda de universidades | Por texto encuentra por siglas, nombre o ciudad sin distinguir acentos ni mayúsculas; por coordenadas devuelve las más cercanas con `distanceKm`; máximo 20. | F2 | T03 |
| NV-11-R04 | Preferencias de búsqueda | `PUT` valida rangos y enumeraciones, incrementa `version`; `GET` devuelve valores por defecto si no existen. | F1 | T04 |
| NV-11-R05 | Persistencia e índices | Documento único por usuario; índices `2dsphere` y de texto en universidades; proyecciones explícitas. | F2 | T05 |
| NV-11-R06 | Privacidad de ubicación | Las coordenadas usadas en la búsqueda no se persisten ni se registran; prueba que lo verifica. | F3 | T06 |
| NV-11-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Extensión del modelo de universidades | Campos aditivos en NV-08 (versión 1.2.0) con migración del seed | PENDIENTE |
| Moneda | MXN única en la primera versión | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Índices dentro del límite de 512 MB |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-11-RK01 | Preferencias inconsistentes con Smart Match | Recomendaciones erróneas | `version` y validación compartida |
| NV-11-RK02 | Fuga de ubicación del usuario | Privacidad | No persistir ni registrar coordenadas |
| NV-11-RK03 | Búsqueda lenta sin índices | Experiencia pobre | Índices de texto y geoespacial |

## 15. Dependencias

- NV-08 (catálogo de universidades).
- NV-10 (`/users/me`).
- NV-14 y NV-23 consumen preferencias y universidad.

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
