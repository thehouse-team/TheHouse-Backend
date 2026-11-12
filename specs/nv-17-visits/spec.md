# NV-17 — Visitas: solicitud, confirmación y calendario

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `spec.md` |
| Versión | 1.3.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-11 («apruebo y ejecuta»: aprobación de 1.3.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-17-visits/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.3.0: T01–T07 completas; T08–T11 esperan CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-80 (estudiante y arrendador); RNF-02 (dirección al confirmar) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.3.0-borrador |
| Tasks | `task.md` 1.3.0-borrador |
| Módulo | Visits |
| Precondición | NV-17 1.2.0, NV-12, NV-14, NV-16 y NV-21 desplegadas |
| Relación | Completa RF-80; la dirección exacta habilita llegar a la visita; NV-18 cerrará el trato y NV-20 notificará los cambios |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.3.0-borrador (2026-11-11):** las versiones 1.1.0 y 1.2.0 quedaron completas y desplegadas (ver `evidence.md`). Esta versión agrega la parte del arrendador de RF-80: ve las visitas de sus cuartos en un calendario y las **confirma**, **reprograma** o **cancela**. Al confirmar, el estudiante ve la **dirección exacta** y la ubicación del edificio (RNF-02); antes nunca. Los estados siguen siendo `requested`, `confirmed` y `cancelled`; la cancelación indica quién la hizo y por qué. Las respuestas agregan campos sin quitar ninguno, así que la app actual sigue funcionando. NV-16 muestra también las visitas confirmadas en la solicitud. La retirada de la transición `propertyId` (prevista para 1.3.0 en M-BE-067) se mueve a una versión posterior, cuando Mobile confirme que ya no la usa.

## 1. Contexto

Hoy el estudiante solicita visitas a cuartos publicados (NV-17 1.2.0), pero el arrendador no las ve ni puede responder, y la dirección exacta nunca se muestra, así que el estudiante no sabe a dónde ir. RF-80 pide que el propietario confirme, reprograme o cancele desde su calendario, y RNF-02 dice que la dirección exacta solo se muestra al confirmar una visita o al cerrar trato. Con NV-16 el arrendador ya tiene bandeja de solicitudes y chat.

## 2. Objetivo

Que el arrendador vea las visitas de sus cuartos y las confirme, reprograme o cancele, y que el estudiante vea la dirección exacta solo cuando su visita está confirmada.

## 3. Precondiciones

1. NV-17 1.2.0, NV-12, NV-14, NV-16 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Calendario del arrendador: visitas de sus cuartos por rango de fechas, estado y cuarto.
2. Detalle de una visita para el arrendador, con el nombre y la universidad del estudiante.
3. Confirmar una visita `requested`.
4. Reprogramar una visita `requested` o `confirmed` a otro horario válido (queda confirmada en el nuevo horario).
5. Cancelar una visita `requested` o `confirmed` con motivo.
6. El estudiante puede cancelar también visitas `confirmed`.
7. Dirección exacta y ubicación en la visita del estudiante solo cuando está `confirmed` (RNF-02).
8. NV-16: la visita activa que muestra la solicitud incluye las `confirmed`.
9. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Retirar la transición `propertyId`: versión posterior, cuando Mobile confirme.
- Notificaciones push o por correo de los cambios: NV-20.
- Que el estudiante acepte o rechace una reprogramación: la cancela si no le conviene.
- Disponibilidad o agenda configurable del arrendador; varias visitas al mismo cuarto a la misma hora están permitidas.
- Marcar la visita como realizada o no asistida.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/visits` | Autenticado + `Idempotency-Key` | Solicita una visita (sin cambios) | 201, 200, 400, 401, 404, 409, 422, 429, 503 |
| GET | `/api/v1/users/me/visits` | Autenticado | Mis visitas; filtro `status` con `confirmed` | 200, 400, 401, 429, 503 |
| GET | `/api/v1/visits/{id}` | Autenticado (estudiante) | Detalle; con dirección exacta si está `confirmed` | 200, 400, 401, 404, 429, 503 |
| POST | `/api/v1/visits/{id}/cancel` | Autenticado (estudiante) | Cancela una visita `requested` o `confirmed` futura | 200, 400, 401, 404, 409, 429, 503 |
| GET | `/api/v1/landlord/visits` | Arrendador | Calendario: visitas de mis cuartos por `from`, `to`, `status` y `roomId` | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/landlord/visits/{id}` | Arrendador (dueño del cuarto) | Detalle de una visita a mi cuarto | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/landlord/visits/{id}/confirm` | Arrendador (dueño del cuarto) | Confirma una visita `requested` | 200, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/visits/{id}/reschedule` | Arrendador (dueño del cuarto) | Mueve la visita a `{ scheduledAt, note }` y la deja confirmada | 200, 400, 401, 403, 404, 409, 422, 429, 503 |
| POST | `/api/v1/landlord/visits/{id}/cancel` | Arrendador (dueño del cuarto) | Cancela con `{ reason, note }` | 200, 400, 401, 403, 404, 409, 429, 503 |

- Visita del estudiante: `{ id, room: { id, buildingId, title, city }, property: { id, title, city } | null, scheduledAt, previousScheduledAt | null, timeZone, status: "requested" | "confirmed" | "cancelled", message, landlord: { name }, address: { street, number, neighborhood, city, state, postalCode } | null, location: { lat, lng } | null, cancellation: { by: "student" | "landlord", reason, note } | null, createdAt, updatedAt, confirmedAt, cancelledAt }`. Campos nuevos respecto a 1.2.0: `previousScheduledAt`, `landlord`, `address`, `location`, `cancellation` y `confirmedAt`; ninguno se quita.
- `address` y `location` (coordenadas reales del edificio) solo aparecen para el estudiante dueño de la visita y solo si está `confirmed`; en cualquier otro estado son `null`.
- Visita del arrendador: `{ id, room: { id, buildingId, title, city }, scheduledAt, previousScheduledAt | null, timeZone, status, message, student: { name, university }, cancellation | null, createdAt, updatedAt, confirmedAt, cancelledAt }`. Nunca incluye teléfono, correo, apellidos ni ids de usuario.
- Calendario: `from` y `to` (fechas ISO 8601 con zona; por defecto hoy y 30 días después; máximo 62 días), `status` (`requested`, `confirmed`, `cancelled`), `roomId`, `page`, `pageSize` ≤ 50; orden `scheduledAt` asc e id.
- Reprogramar: `{ scheduledAt, note }` con las mismas reglas de horario de 1.1.0 (`America/Mexico_City`, 08:00–20:00, cuartos de hora, de 2 h a 60 días); `note` opcional ≤ 300; `previousScheduledAt` guarda el horario anterior.
- Cancelar (arrendador): `reason` (`not_available`, `schedule_conflict`, `other`) y `note` ≤ 300, obligatoria con `other`. Cancelar (estudiante): `{ reason? }` como en 1.1.0, ahora también para `confirmed`.
- Códigos nuevos: 409 `visit_state` (transición inválida), 409 `visit_in_past` (la visita ya pasó), 409 `student_schedule_overlap` (al reprogramar choca con otra visita activa del estudiante), 422 de horario como en 1.1.0, 403 `landlord_required`.
- Todas las respuestas con `no-store`.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/visits/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 { id, room: { id, buildingId, title, city }, property: { id, title, city } | null, scheduledAt, previousScheduledAt | null, timeZone, status: "requested" | "confirmed" | "cancelled", message, landlord: { name }, address: { street, number, neighborhood, city, state, postalCode } | null, location: { lat, lng } | null, cancellation: { by: "student" | "landlord", reason, note } | null, createdAt, updatedAt, confirmedAt, cancelledAt }
STATUS CODES 200, 400, 401, 404 ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/visits?status=confirmed&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      status (requested|confirmed|cancelled), page, pageSize
RESPONSE     200 { items: [visita], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/visits/{id}/cancel
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { reason? }
RESPONSE     200 visita (cancellation.by = student)
STATUS CODES 200, 400, 401, 404, 409 code=visit_state | visit_in_past, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits?from=2026-11-12T00:00:00-06:00&to=2026-11-30T23:59:59-06:00&status=requested&roomId=..
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      from, to (≤ 62 días), status, roomId, page, pageSize
RESPONSE     200 { items: [{ id, room: { id, buildingId, title, city }, scheduledAt, previousScheduledAt | null, timeZone, status, message, student: { name, university }, cancellation | null, createdAt, updatedAt, confirmedAt, cancelledAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403 code=landlord_required, 429, 503
PAGINATION   Sí: orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 visita del arrendador
STATUS CODES 200, 400, 401, 403, 404 ajena, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/confirm
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 visita del arrendador (confirmed)
STATUS CODES 200, 401, 403, 404, 409 code=visit_state | visit_in_past, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/reschedule
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { scheduledAt, note? }
RESPONSE     200 visita del arrendador (confirmed, previousScheduledAt)
STATUS CODES 200, 400, 401, 403, 404, 409 code=visit_state | visit_in_past | student_schedule_overlap, 422 horario, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/visits/{id}/cancel
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      { reason: not_available|schedule_conflict|other, note? }
RESPONSE     200 visita del arrendador (cancelled, cancellation.by = landlord)
STATUS CODES 200, 400, 401, 403, 404, 409 code=visit_state | visit_in_past, 429, 503
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
| Usa (puertos de otros módulos) | Listings `IRoomRepository`, `IBuildingRepository` e `ILandlordProfileRepository` (NV-12) para el cuarto, el dueño y la dirección; Profile `IProfileRepository` (NV-11) para el nombre y la universidad del estudiante |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `visits` agrega `previousScheduledAt`, `confirmedAt`, `cancelledBy` (`student` | `landlord`), `cancelNote`; `history[]` registra cada confirmación, reprogramación y cancelación con su actor; índice nuevo `{ roomId, scheduledAt }`.
- El calendario del arrendador consulta las visitas de los cuartos que le pertenecen (lista de sus cuartos de NV-12 y `$in` por `roomId`), sin migrar datos ni guardar el dueño en la visita.
- La dirección y la ubicación se leen del edificio (NV-12) al responder; no se copian a la visita.

## 9. Reglas de negocio

1. Activas = `requested` o `confirmed`: una activa por estudiante y cuarto, separación de 60 min y máximo 5, como en 1.2.0.
2. Transiciones: `requested` → `confirmed` (arrendador); `requested` | `confirmed` → `confirmed` en otro horario (reprogramar, arrendador); `requested` | `confirmed` → `cancelled` (estudiante o arrendador). Cualquier otra → 409 `visit_state`; sobre una visita cuyo horario ya pasó → 409 `visit_in_past`.
3. Reprogramar valida el horario con las reglas de 1.1.0 y que no choque con otra visita activa del mismo estudiante (60 min) → 409 `student_schedule_overlap`.
4. Solo el dueño del cuarto ve y cambia las visitas de ese cuarto; las visitas de cuartos que dejaron de publicarse siguen visibles y se pueden cancelar, pero no confirmar ni reprogramar → 409 `visit_state`.
5. La dirección exacta solo se entrega al estudiante dueño de la visita y solo en estado `confirmed` (RNF-02).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: el estudiante solo ve sus visitas y el arrendador solo las de sus cuartos; lo ajeno responde 404.
- La dirección exacta nunca aparece en listados públicos ni en visitas no confirmadas; prueba que lo verifica.
- El arrendador ve el nombre del perfil y la universidad del estudiante, nunca su correo, teléfono ni id.
- Rate limit 60/min por usuario en el calendario y las acciones; Redis caído → 503.
- Notas y mensajes fuera de logs; entradas con campos desconocidos → 400.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Transición inválida, visita pasada o choque al reprogramar | 409 | Conflicto (`code`: `visit_state`, `visit_in_past`, `student_schedule_overlap`) |
| Horario fuera de las reglas al reprogramar | 422 | Solicitud no procesable |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-17-R01 | Calendario del arrendador | Lista las visitas de sus cuartos por rango (≤ 62 días), estado y cuarto, ordenadas por horario; detalle con nombre y universidad del estudiante; lo ajeno 404; sin modo arrendador 403. | F1 | T01 |
| NV-17-R02 | Confirmar | `requested` futura → `confirmed` con `confirmedAt` e historial; cualquier otro estado 409 `visit_state`; visita pasada 409 `visit_in_past`; cuarto no publicado 409. | F1 | T02 |
| NV-17-R03 | Reprogramar | Nuevo horario válido con las reglas de 1.1.0, `previousScheduledAt` guardado, estado `confirmed`; 422 horario inválido; 409 `student_schedule_overlap`. | F1 | T03 |
| NV-17-R04 | Cancelar por cualquiera de las partes | Arrendador con motivo del catálogo (nota obligatoria con `other`); estudiante también sobre `confirmed`; `cancellation.by` correcto; visita pasada 409. | F1 | T04 |
| NV-17-R05 | Dirección exacta al confirmar | La visita del estudiante incluye `address` y `location` solo si está `confirmed`; en `requested` y `cancelled` son `null`; nunca en vistas del arrendador ni públicas. | F2 | T05 |
| NV-17-R06 | Compatibilidad y NV-16 | Las respuestas de 1.2.0 conservan todos sus campos; el filtro `status` acepta `confirmed`; la solicitud de NV-16 muestra la visita activa `requested` o `confirmed`. | F2 | T06 |
| NV-17-R07 | Privacidad, autorización y límites | Pruebas con estudiante, arrendador y un tercero; sin datos de contacto; rate limits 429; Redis caído 503. | F3 | T07 |
| NV-17-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitar, calendario, confirmar con dirección, reprogramar, cancelar por ambas partes). | F4 | T08 |
| NV-17-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Estados | Se conservan `requested`, `confirmed` y `cancelled`; `cancellation.by` distingue quién canceló (no hay estado `declined`) | APROBADO (M-BE-069) |
| Reprogramar | El arrendador mueve la visita y queda confirmada; el estudiante la cancela si no le conviene | APROBADO (M-BE-069) |
| Dirección exacta | Solo al estudiante y solo con la visita `confirmed` (RNF-02); se lee del edificio al responder | APROBADO (M-BE-069) |
| Calendario | Consulta por los cuartos del arrendador, sin migrar visitas; rango máximo de 62 días | APROBADO (M-BE-069) |
| Varias visitas al mismo cuarto a la misma hora | Permitidas; el arrendador decide si confirma | APROBADO (M-BE-069) |
| Transición `propertyId` | Se retira en una versión posterior, cuando Mobile confirme (cambia lo previsto en M-BE-067) | APROBADO (M-BE-069) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Sin colecciones nuevas; un índice más en `visits` |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-17-RK01 | Dirección expuesta antes de tiempo | Seguridad del arrendador | Prueba de que solo sale en `confirmed` y al dueño de la visita |
| NV-17-RK02 | Reprogramación que no le conviene al estudiante | Visitas perdidas | El estudiante ve `previousScheduledAt` y puede cancelar; NV-20 notificará |
| NV-17-RK03 | Arrendador con muchos cuartos | Calendario lento | Rango máximo de 62 días e índice `{ roomId, scheduledAt }` |
| NV-17-RK04 | App anterior sin los nuevos estados | Pantallas incompletas | Campos solo agregados; `confirmed` documentado en el contrato |

## 16. Dependencias

- NV-17 1.2.0, NV-12, NV-14, NV-16 y NV-21.
- NV-18 (cierre de trato) y NV-20 (notificaciones).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 7 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.3.0: T01–T07 completas; T08–T11 esperan CI, despliegue y Azure** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
