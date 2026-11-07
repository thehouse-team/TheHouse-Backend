# NV-15 — Favoritos

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `spec.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-31, RF-32 (RF-33, comparación, en versión posterior) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-09 |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |
| Módulo | Favorites |
| Precondición | NV-15 1.1.0, NV-12, NV-14 y NV-21 desplegadas; seed de demostración aplicado |
| Relación | Usa los cuartos publicados de NV-14; NV-23 podrá usar los favoritos como señal de interés |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.2.0-borrador (2026-11-07):** La versión 1.1.0 quedó completa y desplegada (ver `evidence.md`). Con NV-14 los cuartos publicados son el catálogo real; esta versión cambia el objetivo de `propertyId` (NV-07) a `roomId` (cuarto `available` de un edificio `verified`). Para no romper la app actual hay una **transición**: los ids de las 12 propiedades se aceptan y se traducen a su cuarto con el mapeo del seed de demostración (`demoSourceId`), y las respuestas incluyen además los campos anteriores (`propertyId`, `propertyIds`, `property`) con el valor de esa propiedad o `null`. La versión 1.3.0 retirará la transición cuando Mobile confirme que ya usa cuartos. Un comando autorizado migra los datos existentes.

## 1. Contexto

Los favoritos de 1.1.0 guardan `propertyId` de las 12 propiedades de ejemplo. NV-14 ya publica cuartos reales de arrendadores aprobados y convirtió esas 12 propiedades en cuartos `demo` con un mapeo propiedad → cuarto. Mientras los favoritos apunten a propiedades, la app tiene dos catálogos que no se hablan.

## 2. Objetivo

Que los favoritos apunten a cuartos publicados, sin romper la app actual durante la transición y migrando los favoritos existentes.

## 3. Precondiciones

1. NV-15 1.1.0 y NV-14 desplegadas con el seed de demostración.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA»; la migración de datos en Production requiere autorización aparte.

## 4. Alcance

1. `PUT`/`DELETE /users/me/favorites/{roomId}` sobre cuartos publicados.
2. Listado y `ids` con datos públicos del cuarto (los de NV-14).
3. Transición: ids de propiedades traducidos a su cuarto y campos anteriores en las respuestas.
4. `/properties` responde con encabezados de obsolescencia.
5. Comando autorizado `--migrate-to-rooms` (compartido con NV-17) que convierte los favoritos existentes.
6. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Retirar la transición y `/properties`: NV-15/NV-17 1.3.0, cuando Mobile confirme.
- Comparación de cuartos (RF-33).
- Alertas de cambio de precio.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/users/me/favorites/{roomId}` | Autenticado | Agrega un cuarto publicado; si ya estaba, no cambia nada | 204, 400, 401, 404, 409, 429, 503 |
| DELETE | `/api/v1/users/me/favorites/{roomId}` | Autenticado | Quita de favoritos; si no estaba, no cambia nada | 204, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/favorites` | Autenticado | Favoritos paginados, más recientes primero | 200, 400, 401, 429, 503 |
| GET | `/api/v1/users/me/favorites/ids` | Autenticado | Ids de los cuartos favoritos | 200, 401, 429, 503 |

- `{roomId}` es el id de un cuarto de NV-14. Durante la transición también se acepta el id de una de las 12 propiedades; se traduce a su cuarto y se guarda el cuarto.
- Agregar un cuarto que no está publicado (`available` en edificio `verified`) o que no existe → 404; límite de 200 → 409 `favorites_limit`.
- Item del listado: `{ roomId, buildingId, title, price, currency, roomType, bathroom, city, approxLocation, demo, favoritedAt, propertyId (transición) }`; `approxLocation` es la celda de NV-14; `propertyId` es la propiedad de origen del cuarto `demo` o `null`.
- `ids` responde `{ roomIds: [..], propertyIds: [..] }`; `propertyIds` (transición) contiene solo las propiedades de origen de los cuartos `demo` favoritos.
- Un cuarto que deja de estar publicado (pausado, rentado, edificio suspendido) se omite del listado y de `ids`, pero no se borra: vuelve a aparecer si se publica otra vez.
- `GET /properties` y `GET /properties/{id}` siguen funcionando y agregan `Deprecation: true` y `Link: </api/v1/rooms/search>; rel="successor-version"`.
- Respuestas con `Cache-Control: no-store`; errores con `code` (M-BE-061).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/favorites/{roomId}
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo; roomId (o, en transición, propertyId) en la ruta
RESPONSE     204 sin cuerpo (también si ya era favorito)
STATUS CODES 204, 400 id inválido, 401, 404 cuarto no publicado, 409 code=favorites_limit, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/favorites/{roomId}
METHOD       DELETE
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     204 sin cuerpo (también si no era favorito)
STATUS CODES 204, 400, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/favorites?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      page, pageSize (1-50)
RESPONSE     200 { items: [{ roomId, buildingId, title, price, currency, roomType, bathroom, city, approxLocation, demo, favoritedAt, propertyId (transición) }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: sobre de NV-01; orden favoritedAt desc
```

```text
ENDPOINT     /api/v1/users/me/favorites/ids
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      sin parámetros
RESPONSE     200 { roomIds: [..], propertyIds: [..] (transición) }
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
| Usa (puertos de otros módulos) | Search `SearchService` y Listings `IRoomRepository`/`IBuildingRepository` (NV-14/NV-12) para validar y leer cuartos publicados |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `favorites`: `userId`, `roomId`, `createdAt`; índice único `{ userId, roomId }` e índice `{ userId, createdAt: -1, roomId: -1 }`. El índice único anterior `{ userId, propertyId }` se elimina en la migración.
- Traducción propiedad → cuarto: edificio con `demoSourceId` = id de la propiedad y su cuarto 101 (NV-14).

## 9. Reglas de negocio

1. El usuario sale siempre del `sub` del token.
2. Agregar y quitar son idempotentes; el índice único evita duplicados ante doble toque.
3. Solo se agregan cuartos publicados; los que dejan de estarlo se ocultan sin borrarse.
4. Migración (`--migrate-to-rooms`, autorizada e idempotente): cada favorito con `propertyId` recibe el `roomId` de su cuarto; si el usuario ya tenía ese cuarto se elimina el duplicado; los que no tienen mapeo se reportan y se conservan.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Sin `userId` del cliente; pruebas con dos usuarios.
- Las respuestas solo incluyen datos públicos de NV-14 (sin dirección ni dueño).
- Rate limit en Redis de 60 por minuto por usuario; Redis caído → 503.

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
| NV-15-R01 | Favoritos de cuartos publicados | `PUT` con un cuarto publicado responde 204 una sola vez; cuarto no publicado o inexistente 404; id inválido 400; límite 409. | F1 | T01 |
| NV-15-R02 | Quitar favorito | `DELETE` responde 204 exista o no; después el cuarto no aparece en el listado ni en `ids`. | F1 | T02 |
| NV-15-R03 | Listado e ids con datos de cuartos | Listado con los campos del §6 y `ids` con `roomIds`; cuartos no publicados omitidos sin borrarse. | F1 | T03 |
| NV-15-R04 | Transición desde propiedades | Un id de propiedad se traduce a su cuarto en `PUT`/`DELETE`; las respuestas incluyen `propertyId`/`propertyIds`; `/properties` agrega `Deprecation` y `Link`. | F2 | T04 |
| NV-15-R05 | Migración de datos | `--migrate-to-rooms` convierte favoritos existentes, elimina duplicados, reporta los no mapeados, cambia los índices y es idempotente. | F2 | T05 |
| NV-15-R06 | Autorización y límites | Pruebas con dos usuarios; rate limit 429; Redis caído 503; `no-store`. | F3 | T06 |
| NV-15-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; migración autorizada en Production; prueba HTTP real en Azure (agregar por cuarto y por propiedad, listar, ids, quitar). | F4 | T07 |
| NV-15-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Identificador | `roomId`; el id de propiedad se acepta como alias durante la transición | PROPUESTO |
| Respuestas de transición | Incluyen `propertyId`/`propertyIds` hasta 1.3.0 | PROPUESTO |
| Cuartos no publicados | Se omiten sin borrarse | PROPUESTO |
| Migración | Comando `--migrate-to-rooms` autorizado, idempotente y con reporte | PROPUESTO |
| `/properties` | Encabezados de obsolescencia; se retira en 1.3.0 de acuerdo con Mobile | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Sin colecciones nuevas |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-15-RK01 | Versión vieja de la app | Errores al marcar favoritos | Transición con ids de propiedad y campos anteriores |
| NV-15-RK02 | Migración parcial | Favoritos perdidos | Comando idempotente con reporte y prueba previa en el entorno efímero |
| NV-15-RK03 | Cuartos que dejan de publicarse | Favoritos que desaparecen | Se ocultan sin borrarse y regresan si se publican de nuevo |

## 16. Dependencias

- NV-14 (cuartos publicados y seed de demostración).
- NV-17 1.2.0 (comparte el comando de migración).
- Mobile (cambio a `roomId`).

## 17. Trazabilidad y cobertura

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

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
