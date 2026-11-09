# NV-17 — Visitas: solicitudes (v1)

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `spec.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-17-visits/`; la migración `--migrate-to-rooms` en Production requiere autorización aparte) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0, T01–T10 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-80 (parte del estudiante; la del arrendador en versión posterior) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |
| Módulo | Visits |
| Precondición | NV-17 1.1.0, NV-12, NV-14 y NV-21 desplegadas; seed de demostración aplicado |
| Relación | Las visitas apuntan a cuartos con dueño: base para que el arrendador las confirme en una versión posterior |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.2.0-borrador (2026-11-07):** La versión 1.1.0 quedó completa y desplegada (ver `evidence.md`). Con NV-14 los cuartos publicados son el catálogo real; esta versión cambia el objetivo de `propertyId` (NV-07) a `roomId` (cuarto `available` de un edificio `verified`). Para no romper la app actual hay una **transición**: los ids de las 12 propiedades se aceptan y se traducen a su cuarto con el mapeo del seed de demostración (`demoSourceId`), y las respuestas incluyen además los campos anteriores (`propertyId`, `propertyIds`, `property`) con el valor de esa propiedad o `null`. La versión 1.3.0 retirará la transición cuando Mobile confirme que ya usa cuartos. Un comando autorizado migra los datos existentes. Las reglas de horario, idempotencia, conflictos y lock por usuario de 1.1.0 (y los ajustes M-BE-061) no cambian; ahora se aplican por cuarto.

## 1. Contexto

Las visitas de 1.1.0 guardan `propertyId` de propiedades sin dueño, por eso nadie puede confirmarlas. Con NV-14 los cuartos publicados tienen arrendador; mover las visitas a cuartos es el paso previo para que el arrendador las vea y las confirme.

## 2. Objetivo

Que las visitas apunten a cuartos publicados con las mismas reglas de 1.1.0, sin romper la app actual y migrando las visitas existentes.

## 3. Precondiciones

1. NV-17 1.1.0 y NV-14 desplegadas con el seed de demostración.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA»; la migración de datos en Production requiere autorización aparte.

## 4. Alcance

1. `POST /visits` con `roomId` (o `propertyId` en transición).
2. Respuestas con `room` y, en transición, `property`.
3. Las reglas de conflicto (misma visita activa, separación de 60 min, máximo 5) se aplican por cuarto.
4. Migración de visitas existentes con el comando `--migrate-to-rooms` (compartido con NV-15).
5. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Confirmar, rechazar o reprogramar visitas (arrendador): versión posterior.
- Retirar la transición: 1.3.0.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/visits` | Autenticado + `Idempotency-Key` | Solicita una visita a un cuarto publicado | 201, 200 (repetición), 400, 401, 404, 409, 422, 429, 503 |
| GET | `/api/v1/users/me/visits` | Autenticado | Visitas propias paginadas; filtro `status` | 200, 400, 401, 429, 503 |
| GET | `/api/v1/visits/{id}` | Autenticado (dueño de la visita) | Detalle de una visita propia | 200, 400, 401, 404, 429, 503 |
| POST | `/api/v1/visits/{id}/cancel` | Autenticado (dueño de la visita) | Cancela una visita `requested` futura | 200, 400, 401, 404, 409, 429, 503 |

- Entrada: `{ roomId, scheduledAt, message }`; en transición se acepta `propertyId` en lugar de `roomId` (exactamente uno de los dos; ambos o ninguno → 400). El id de propiedad se traduce a su cuarto.
- Visita: `{ id, room: { id, buildingId, title, city }, property: { id, title, city } | null (transición), scheduledAt, timeZone, status, message, createdAt, updatedAt, cancelledAt }`; `room.city` es la ciudad del edificio (nunca la dirección).
- Cuarto no publicado o inexistente → 404. Horario, idempotencia (24 h), conflictos y cancelación igual que 1.1.0, con `code` en 409/422 y fechas UTC `+00:00` (M-BE-061).
- Las visitas ya creadas cuyo cuarto deja de publicarse se siguen viendo y cancelando.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/visits
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Idempotency-Key: <UUID> (obligatorio); Content-Type: application/json
REQUEST      { "roomId": "<24 hex>", "scheduledAt": "2026-11-20T17:00:00-06:00", "message": "opcional, <= 300" } (transición: propertyId en lugar de roomId)
RESPONSE     201 { id, room: { id, buildingId, title, city }, property: { id, title, city } | null (transición), scheduledAt, timeZone, status, message, createdAt, updatedAt, cancelledAt } | 200 con Idempotent-Replayed: true
STATUS CODES 201, 200, 400, 401, 404 cuarto, 409 code=..., 422 code=..., 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/visits?status=requested&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      status (requested|cancelled), page, pageSize
RESPONSE     200 { items: [visita], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: sobre de NV-01; orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/visits/{id} | /api/v1/visits/{id}/cancel
METHOD       GET | POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id en la ruta; cancel: { reason? }
RESPONSE     200 visita
STATUS CODES 200, 400, 401, 404, 409 code=visit_in_past, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Visits** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `visits`, `visit_idempotency` |
| Usa (puertos de otros módulos) | Listings `IRoomRepository`/`IBuildingRepository` (NV-12) para validar el cuarto y resumirlo |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `visits`: `userId`, `roomId`, `scheduledAt` (UTC), `status`, `message`, `cancelReason`, `createdAt`, `updatedAt`, `cancelledAt`, `history[]`; índices `{ userId, scheduledAt }` y `{ userId, status }`.
- `visit_idempotency`: sin cambios; el hash del cuerpo se calcula con el `roomId` ya traducido.

## 9. Reglas de negocio

1. Estados, transiciones, horario en `America/Mexico_City`, lock por usuario (5 s) e idempotencia: iguales a 1.1.0.
2. Conflictos por cuarto: una visita `requested` futura por cuarto, separación de 60 min y máximo 5 activas por usuario.
3. Solo se solicitan visitas a cuartos publicados.
4. Migración (`--migrate-to-rooms`): cada visita con `propertyId` recibe el `roomId` de su cuarto; las que no tienen mapeo se reportan y se conservan.
5. No se revela dirección ni datos de contacto.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- El usuario sale del `sub`; visita ajena → 404 (BOLA).
- Rate limit: 10 creaciones por día y 60 por minuto por usuario; Redis caído → 503.
- Mensaje ≤ 300 caracteres y fuera de logs; `Idempotency-Key` solo con el hash del cuerpo.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Conflicto de horario, visita activa duplicada, límite, llave reutilizada o transición inválida | 409 | Conflicto |
| Horario fuera de las reglas | 422 | Solicitud no procesable |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-17-R01 | Visitas a cuartos publicados | `POST /visits` con `roomId` de un cuarto publicado crea la visita (201); cuarto no publicado o inexistente 404; ambos ids o ninguno 400. | F1 | T01 |
| NV-17-R02 | Respuesta con cuarto | Las respuestas incluyen `room { id, buildingId, title, city }` y, en transición, `property` o `null`. | F1 | T02 |
| NV-17-R03 | Reglas por cuarto | Horario, idempotencia y conflictos de 1.1.0 aplicados por cuarto, con sus pruebas. | F1 | T03 |
| NV-17-R04 | Transición desde propiedades | `propertyId` se traduce a su cuarto y produce la misma visita que con `roomId`; misma llave con el id de propiedad o de cuarto es el mismo cuerpo. | F2 | T04 |
| NV-17-R05 | Migración de visitas | `--migrate-to-rooms` convierte las visitas existentes, reporta las no mapeadas y es idempotente. | F2 | T05 |
| NV-17-R06 | Autorización y límites | Pruebas con dos usuarios; rate limits 429; Redis caído 503; `no-store`. | F3 | T06 |
| NV-17-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado; migración autorizada en Production; prueba HTTP real en Azure (crear por cuarto y por propiedad, repetir, conflicto, listar, cancelar). | F4 | T07 |
| NV-17-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Identificador | `roomId`; `propertyId` como alias en transición | APROBADO (M-BE-067) |
| Respuesta | `room` nuevo y `property` de transición hasta 1.3.0 | APROBADO (M-BE-067) |
| Migración | Mismo comando `--migrate-to-rooms` que NV-15 | APROBADO (M-BE-067) |
| Visitas de cuartos que dejan de publicarse | Se siguen viendo y cancelando | APROBADO (M-BE-067) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Sin colecciones nuevas |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-17-RK01 | Versión vieja de la app | Errores al agendar | Transición con `propertyId` |
| NV-17-RK02 | Idempotencia entre versiones | Duplicados al reintentar con otro id | El hash usa el `roomId` traducido |
| NV-17-RK03 | Migración parcial | Visitas perdidas | Comando idempotente con reporte |

## 16. Dependencias

- NV-14 (cuartos publicados y mapeo).
- NV-15 1.2.0 (comando compartido).
- Versión posterior: confirmación del arrendador.

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
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0, T01–T10 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
