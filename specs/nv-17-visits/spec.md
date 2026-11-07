# NV-17 — Visitas: solicitudes (v1)

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-80 (parte del estudiante; la del arrendador en versión posterior) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Visits |
| Precondición | NV-07 (propiedades) y NV-10 (sesión) desplegadas |
| Relación | Usa las propiedades de NV-07; NV-12 agregará la confirmación del arrendador; NV-19 usará las visitas completadas |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-07):** versión 1 basada en el catálogo actual. Las propiedades de NV-07 no tienen arrendador, así que nadie puede confirmar una visita todavía: esta versión cubre la **solicitud de visita del estudiante** (crear, listar, consultar y cancelar) con idempotencia y reglas de conflicto. La confirmación, reprogramación y el calendario del arrendador, junto con la dirección exacta (RF-41), llegan con NV-12. Alcance pedido por el responsable de Mobile el 2026-11-07.

## 1. Contexto

Mobile tiene el botón de agendar visita y el flujo invitado/autenticado listos, pero no hay contratos. Las propiedades actuales no tienen dueño, por lo que el flujo completo (confirmar, reprogramar, calendario del arrendador y revelar la dirección exacta) no puede existir hasta NV-12. Mobile pidió reglas claras de estados, conflictos de horario, zona horaria e idempotencia ante doble toque o reintentos.

## 2. Objetivo

Permitir que el estudiante autenticado solicite, consulte y cancele visitas a propiedades, con estados y transiciones definidos, horarios validados en la zona horaria de México, sin duplicados por reintentos y sin conflictos de horario.

## 3. Precondiciones

1. NV-07 y NV-10 desplegadas.
2. Autorización «EJECUTA».

## 4. Alcance

1. `POST /visits` con encabezado `Idempotency-Key`.
2. `GET /users/me/visits` paginado y filtrable por estado.
3. `GET /visits/{id}` de una visita propia.
4. `POST /visits/{id}/cancel`.
5. Estados `requested` y `cancelled`; estados futuros reservados y documentados.
6. Reglas de horario, conflicto y límite de visitas activas.
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Confirmar, rechazar, reprogramar o completar visitas (rol arrendador): NV-12 / NV-17 versión posterior.
- Disponibilidad u horarios del arrendador administrados por el backend.
- Dirección exacta del inmueble (RF-41): solo con visita confirmada, en versión posterior.
- Recordatorios y notificaciones (NV-20).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/visits` | Autenticado + `Idempotency-Key` | Solicita una visita a una propiedad | 201, 200 (repetición), 400, 401, 404, 409, 422, 429, 503 |
| GET | `/api/v1/users/me/visits` | Autenticado | Visitas propias paginadas; filtro `status` | 200, 400, 401, 429, 503 |
| GET | `/api/v1/visits/{id}` | Autenticado (dueño de la visita) | Detalle de una visita propia | 200, 400, 401, 404, 429, 503 |
| POST | `/api/v1/visits/{id}/cancel` | Autenticado (dueño de la visita) | Cancela una visita `requested` futura | 200, 400, 401, 404, 409, 429, 503 |

- Visita: `{ id, property: { id, title, city }, scheduledAt, timeZone: "America/Mexico_City", status, message, createdAt, updatedAt, cancelledAt }`.
- `scheduledAt` se recibe en ISO 8601 **con desplazamiento** (por ejemplo `2026-11-20T17:00:00-06:00`) y se devuelve en UTC (`...Z`); sin desplazamiento → 400.
- Reglas de horario (validación de negocio → 422): al menos 2 horas en el futuro, como máximo 60 días, entre 08:00 y 20:00 hora de `America/Mexico_City`, en múltiplos de 15 minutos.
- `Idempotency-Key`: UUID obligatorio. La misma llave con el mismo cuerpo dentro de 24 h devuelve la visita original (200 y `Idempotent-Replayed: true`); con un cuerpo distinto → 409.
- Conflictos → 409: ya existe una visita `requested` del usuario para esa propiedad; el usuario ya tiene otra visita `requested` a menos de 60 minutos; el usuario tiene 5 visitas `requested`.
- Cancelar una visita ya cancelada responde 200 con la visita (idempotente); cancelar una visita pasada → 409.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/visits
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Idempotency-Key: <UUID> (obligatorio); Content-Type: application/json
REQUEST      { "propertyId": "<24 hex>", "scheduledAt": "2026-11-20T17:00:00-06:00", "message": "opcional, <= 300" }
RESPONSE     201 visita (ver §6) | 200 visita original si se repite la misma llave y cuerpo (Idempotent-Replayed: true)
STATUS CODES 201, 200, 400 formato, 401, 404 propiedad, 409 conflicto o llave reutilizada, 422 horario fuera de reglas, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/visits?status=requested&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      query status (requested|cancelled, opcional), page, pageSize
RESPONSE     200 { items: [visita], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: sobre uniforme de NV-01; orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/visits/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id en la ruta (24 hex)
RESPONSE     200 visita
STATUS CODES 200, 400 id inválido, 401, 404 inexistente o ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/visits/{id}/cancel
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { "reason": "opcional, <= 200" } o sin cuerpo
RESPONSE     200 visita con status cancelled y cancelledAt
STATUS CODES 200 (también si ya estaba cancelada), 400, 401, 404, 409 visita pasada, 429, 503
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
| Usa (puertos de otros módulos) | Properties `IPropertyRepository` (NV-07) para validar y resumir la propiedad |
| Publica (eventos de dominio) | `VisitRequested`, `VisitCancelled` |
| Consume (eventos de dominio) | `UserDeleted (NV-10, versión posterior)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `visits`: `userId`, `propertyId`, `scheduledAt` (UTC), `status`, `message`, `cancelReason`, `createdAt`, `updatedAt`, `cancelledAt`, `history[]`; índice único parcial `{ userId, propertyId }` cuando `status = requested`; índices `{ userId, scheduledAt }` y `{ userId, status }`.
- `visit_idempotency`: `userId`, `key`, `requestHash`, `visitId`, `createdAt` con índice único `{ userId, key }` e índice TTL de 24 h.

## 9. Reglas de negocio

1. Estados en v1: `requested` (creada por el estudiante) → `cancelled` (por el estudiante, antes del horario). Reservados para NV-12: `confirmed`, `rejected`, `rescheduled`, `completed`; no se emiten en v1.
2. Cualquier transición no listada responde 409; cada cambio se registra en `history` con fecha y actor.
3. La creación se serializa por usuario con un lock distribuido en Redis (5 s), de modo que dos solicitudes simultáneas con llaves distintas no eludan las reglas de conflicto (constitution §14).
4. El índice único parcial impide dos visitas `requested` del mismo usuario para la misma propiedad aun ante concurrencia.
5. Las horas se validan en `America/Mexico_City` y se guardan en UTC.
6. No se revela dirección exacta ni datos de contacto en ninguna respuesta.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- El usuario sale del `sub`; una visita ajena responde 404 (BOLA).
- Rate limit en Redis: 10 creaciones por día y 60 solicitudes por minuto por usuario; Redis caído → 503.
- El mensaje del estudiante se limita a 300 caracteres y no se registra en logs.
- `Idempotency-Key` se guarda solo con el hash del cuerpo, nunca el cuerpo completo.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Conflicto de horario, visita activa duplicada, límite de activas, llave reutilizada o transición inválida | 409 | Conflicto |
| Horario fuera de las reglas de negocio | 422 | Solicitud no procesable |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-17-R01 | Solicitar visita | `POST /visits` con propiedad existente y horario válido crea una visita `requested` (201); propiedad inexistente 404; formato inválido o fecha sin desplazamiento 400. | F1 | T01 |
| NV-17-R02 | Reglas de horario y zona | Rechaza con 422 horarios a menos de 2 h, a más de 60 días, fuera de 08:00–20:00 en `America/Mexico_City` o no múltiplos de 15 min; respuestas en UTC con `timeZone`. | F1 | T02 |
| NV-17-R03 | Estados y transiciones | Máquina de estados con `requested` → `cancelled`; estados futuros reservados; transiciones inválidas 409; `history` registrado. | F1 | T03 |
| NV-17-R04 | Consultar y listar | `GET /visits/{id}` (solo propias, ajena 404) y `GET /users/me/visits` paginado, filtrable por estado, orden `scheduledAt` asc. | F1 | T04 |
| NV-17-R05 | Cancelar | `POST /visits/{id}/cancel` cancela una visita `requested` futura (200); repetir responde 200; visita pasada 409. | F1 | T05 |
| NV-17-R06 | Idempotencia | Misma `Idempotency-Key` y mismo cuerpo dentro de 24 h devuelven la visita original (200, `Idempotent-Replayed`); cuerpo distinto 409; sin llave 400; prueba de reintentos y doble toque. | F2 | T06 |
| NV-17-R07 | Conflictos y concurrencia | Visita `requested` duplicada para la misma propiedad, otra visita a menos de 60 min o más de 5 activas → 409; pruebas concurrentes con el lock por usuario y el índice único parcial. | F2 | T07 |
| NV-17-R08 | Autorización y límites | Sin `userId` del cliente; pruebas con dos usuarios; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`. | F3 | T08 |
| NV-17-R09 | Contrato para Mobile y despliegue | Contrato de la §6.1 entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (crear, repetir con la misma llave, conflicto, listar, consultar, cancelar). | F4 | T09 |
| NV-17-R10 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T10, T11, T12 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Alcance v1 | Solo solicitudes del estudiante; el arrendador confirma en la versión con NV-12 | PROPUESTO |
| Ventana horaria | 08:00–20:00 en `America/Mexico_City`, múltiplos de 15 min, de 2 h a 60 días | PROPUESTO |
| Idempotencia | `Idempotency-Key` UUID obligatorio con vigencia de 24 h | PROPUESTO |
| Conflictos | Una activa por propiedad, separación de 60 min, máximo 5 activas | PROPUESTO |
| Disponibilidad del arrendador | No se administra en v1 | PROPUESTO |
| Estados con arrendador | `confirmed`, `rejected`, `rescheduled`, `completed` con NV-12 | PENDIENTE |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Documentos pequeños con TTL; un lock y un contador Redis por creación |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-17-RK01 | Doble toque o reintento de red | Visitas duplicadas | `Idempotency-Key`, lock por usuario e índice único parcial |
| NV-17-RK02 | Errores de zona horaria | Visitas a la hora equivocada | Desplazamiento obligatorio, UTC en servidor y pruebas en el límite de horario |
| NV-17-RK03 | Expectativa de confirmación | El estudiante espera respuesta del arrendador | La app muestra el estado `requested` como "Solicitud enviada"; confirmación con NV-12 |
| NV-17-RK04 | Spam de solicitudes | Datos basura | Límite de 5 activas y 10 creaciones por día |

## 16. Dependencias

- NV-07 (propiedades) y NV-10 (sesión).
- NV-12 (arrendadores) para confirmar visitas y revelar la dirección.
- NV-19 (reseñas) usará visitas completadas.
- NV-20 (notificaciones).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 10 |
| Fases del plan | 4 |
| Tasks | 12 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
