# NV-15 — Favoritos

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `spec.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («te apluebo o ejecuta para hacer el desarrollo de lo que nos pidió»: aprobación de 1.1.0 y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-15-favorites-compare/`) |
| Estado de ejecución | **✅ COMPLETA — T01–T10 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-31, RF-32 (RF-33, comparación, en versión posterior) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-09 |
| Plan | `plan.md` 1.1.0 |
| Tasks | `task.md` 1.1.0 |
| Módulo | Favorites |
| Precondición | NV-07 (propiedades) y NV-10 (sesión) desplegadas |
| Relación | Usa las propiedades de NV-07; cuando NV-12/NV-14 publiquen cuartos, los favoritos se migran según la decisión de NV-14 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0 (2026-11-07):** se adapta al catálogo que ya existe: los favoritos apuntan a `propertyId` de NV-07 (las propiedades del seed), porque los cuartos de NV-12 todavía no existen. Se agrega `GET /users/me/favorites/ids` para marcar favoritos en listados sin una consulta por propiedad. La comparación (RF-33) pasa a una versión posterior. Alcance pedido por el responsable de Mobile el 2026-11-07, que ya tiene la UX lista.

## 1. Contexto

Mobile ya tiene la UX de favoritos (agregar, quitar y ver la lista), pero el backend no expone contratos. El único catálogo publicado son las propiedades de NV-07 (doce en producción, sin dueño); los cuartos de arrendadores (NV-12) aún no existen. Guardar favoritos requiere sesión (RF-69).

## 2. Objetivo

Exponer favoritos del usuario autenticado sobre las propiedades actuales, con operaciones idempotentes, listado paginado y una consulta ligera de ids para marcar favoritos en la app.

## 3. Precondiciones

1. NV-07 y NV-10 desplegadas.
2. Autorización «EJECUTA».

## 4. Alcance

1. `PUT /users/me/favorites/{propertyId}` (agregar, idempotente).
2. `DELETE /users/me/favorites/{propertyId}` (quitar, idempotente).
3. `GET /users/me/favorites` paginado con los datos públicos de cada propiedad.
4. `GET /users/me/favorites/ids` con todos los ids favoritos del usuario (máximo 200).
5. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Comparación de propiedades (RF-33): versión posterior.
- Favoritos de invitados: la app los pide después de iniciar sesión (RF-69).
- Alertas de cambio de precio.
- Favoritos sobre cuartos de NV-12 (se decide con NV-14).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/users/me/favorites/{propertyId}` | Autenticado | Agrega a favoritos; si ya estaba, no cambia nada | 204, 400, 401, 404, 409, 429, 503 |
| DELETE | `/api/v1/users/me/favorites/{propertyId}` | Autenticado | Quita de favoritos; si no estaba, no cambia nada | 204, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/favorites` | Autenticado | Favoritos paginados, más recientes primero | 200, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/favorites/ids` | Autenticado | Ids de todas las propiedades favoritas | 200, 401, 429, 503 |

- `propertyId` es el ObjectId de 24 caracteres hexadecimales de NV-07; formato inválido → 400.
- Agregar una propiedad inexistente → 404 (Problem Details `Recurso no encontrado`); quitar es idempotente y responde 204 aunque la propiedad no exista o no fuera favorita.
- Si una propiedad favorita deja de existir, se omite del listado y de `ids`.
- Todas las respuestas llevan `Cache-Control: no-store` (datos del usuario).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/favorites/{propertyId}
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo; propertyId en la ruta (ObjectId de 24 hex)
RESPONSE     204 sin cuerpo (también si ya era favorita)
STATUS CODES 204, 400 id inválido, 401, 404 propiedad inexistente, 409 límite de 200, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/favorites/{propertyId}
METHOD       DELETE
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     204 sin cuerpo (también si no era favorita)
STATUS CODES 204, 400 id inválido, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/favorites?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      query page (>=1, def. 1), pageSize (1-50, def. 20)
RESPONSE     200 { items: [{ propertyId, title, price, currency, city, universityId, bedrooms, bathrooms, areaM2, favoritedAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400 paginación inválida, 401, 429, 503
PAGINATION   Sí: sobre uniforme de NV-01; orden favoritedAt desc, propertyId desc
```

```text
ENDPOINT     /api/v1/users/me/favorites/ids
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 { propertyIds: ["..."] } (máximo 200)
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Favorites** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `favorites` |
| Usa (puertos de otros módulos) | Properties `IPropertyRepository` (NV-07) para validar y leer propiedades por lotes |
| Publica (eventos de dominio) | `FavoriteAdded` |
| Consume (eventos de dominio) | `UserDeleted (NV-10, versión posterior)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `favorites`: `userId`, `propertyId`, `createdAt`; índice único `{ userId, propertyId }` e índice `{ userId, createdAt: -1, propertyId: -1 }` para el listado.
- Las propiedades se leen con proyección pública por lotes desde NV-07 (sin N+1).

## 9. Reglas de negocio

1. El usuario sale siempre del `sub` del token; no hay rutas con `userId`.
2. Agregar es idempotente: el índice único evita duplicados aunque lleguen dos solicitudes simultáneas (doble toque).
3. Quitar es idempotente: 204 aunque no existiera.
4. Máximo 200 favoritos por usuario; el siguiente responde 409.
5. El listado y `ids` omiten propiedades que ya no existen.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA imposible por diseño: no se aceptan identificadores de usuario; cada consulta filtra por `sub`.
- Rate limit en Redis de 60 solicitudes por 60 s por usuario para todo el módulo; si Redis falla, 503 (fallo cerrado).
- Respuestas con `no-store`; los logs no incluyen ids de usuario ni de propiedades favoritas.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Límite de favoritos alcanzado | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-15-R01 | Agregar favorito idempotente | `PUT` con propiedad existente responde 204 y crea el favorito una sola vez; repetirlo responde 204 sin duplicar; propiedad inexistente 404; id inválido 400; límite de 200 → 409. | F1 | T01 |
| NV-15-R02 | Quitar favorito idempotente | `DELETE` responde 204 exista o no el favorito; después la propiedad ya no aparece en el listado ni en `ids`. | F1 | T02 |
| NV-15-R03 | Listado paginado | `GET /users/me/favorites` devuelve el sobre de paginación de NV-01, orden `favoritedAt` desc y `propertyId` desc, con datos públicos de la propiedad y sin propiedades inexistentes. | F1 | T03 |
| NV-15-R04 | Ids de favoritos | `GET /users/me/favorites/ids` devuelve todos los ids favoritos vigentes del usuario (máximo 200) en una sola respuesta. | F1 | T04 |
| NV-15-R05 | Persistencia e índices | Índice único `{ userId, propertyId }` probado con solicitudes concurrentes; listado sin N+1 (lectura de propiedades por lotes). | F2 | T05 |
| NV-15-R06 | Autorización y límites | Sin `userId` del cliente; pruebas con dos usuarios (uno no ve ni altera los favoritos del otro); rate limit 429 con `Retry-After`; Redis caído 503; `no-store`. | F3 | T06 |
| NV-15-R07 | Contrato para Mobile y despliegue | Contrato de la §6.1 entregado a Mobile con URL desplegada, resultado de pruebas y prueba HTTP real en Azure (agregar, repetir, listar, ids, quitar, repetir). | F4 | T07 |
| NV-15-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

|  | Propuesta | Estado |
| --- | --- | --- |
| Base de los favoritos | `propertyId` de NV-07 mientras no existan cuartos de NV-12 | APROBADO (M-BE-060) |
| Límite | 200 favoritos por usuario | APROBADO (M-BE-060) |
| Consulta de favoritos para listados | `GET /users/me/favorites/ids` en lugar de una consulta por propiedad | APROBADO (M-BE-060) |
| Migración a cuartos (NV-12/NV-14) | Se decide junto con la migración de NV-07 en NV-14 | PENDIENTE |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Documentos pequeños; un comando Redis por solicitud |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-15-RK01 | Duplicados por doble toque | Datos inconsistentes | Índice único e idempotencia |
| NV-15-RK02 | N+1 al listar | Latencia en móvil | Lectura de propiedades por lotes |
| NV-15-RK03 | Cambio futuro a cuartos | Favoritos huérfanos | Decisión de migración en NV-14 antes de retirar `properties` |

## 16. Dependencias

- NV-07 (propiedades) y NV-10 (sesión y `/users/me`).
- NV-14 (migración futura a cuartos).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETA — T01–T10 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |

## 18. Aprobación y ejecución

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
