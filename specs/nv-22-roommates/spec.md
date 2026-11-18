# NV-22 — Búsqueda de compañero de vivienda

| Campo | Valor |
| --- | --- |
| ID | NV-22 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-22-roommates/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-46, RF-47 (compañero de vivienda); RF-74 (menú «Buscar compañero») (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Roommates |
| Precondición | NV-11, NV-16, NV-20 y NV-21 desplegadas |
| Relación | Usa la universidad del perfil (NV-11) y el chat (NV-16, conversación `roommate`); NV-20 avisa del mensaje; NV-21 recibe reportes del usuario; Smart Match (NV-23) podrá dar el porcentaje de afinidad |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-17):** se adapta al prototipo «Buscar compañero» y a lo ya desplegado. En vez de publicaciones sueltas, cada estudiante tiene un **perfil de compañero voluntario** («Quiero aparecer como compañero») con presupuesto, zona, mes de llegada, duración, si ya tiene lugar y 6 hábitos. Solo quien activó su perfil ve a los demás (reciprocidad), siempre de su misma universidad (NV-11). Los resultados se ordenan por **coincidencias de hábitos** y presupuesto, una regla simple y explicable; el porcentaje de afinidad del prototipo queda para Smart Match (NV-23). El contacto abre una conversación de NV-16 de tipo nuevo `roommate` (sin cuarto). El perfil vence a los 60 días sin actualizarse.

## 1. Contexto

El prototipo «Buscar compañero» muestra un perfil con «Quiero aparecer como compañero», preferencias (presupuesto aproximado, zona cerca del campus, hábitos como horario, limpieza, ruido, fumar, mascotas y vida social, y el periodo), una lista de compañeros compatibles con su presupuesto y hábitos, y el botón «Contactar» que manda un mensaje interno sin compartir el teléfono. RF-46 pide publicar que se busca compañero y RF-47 consultar y filtrar opciones de vivienda compartida. El chat ya existe en NV-16 y las notificaciones en NV-20.

## 2. Objetivo

Que un estudiante active su perfil de compañero, vea a otros estudiantes de su universidad que también lo activaron, ordenados por coincidencias, y los contacte por el chat de TheHouse sin exponer sus datos personales.

## 3. Precondiciones

1. NV-11, NV-16, NV-20 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».

## 4. Alcance

1. Mi perfil de compañero: crear o actualizar, consultar y desactivar.
2. Lista de compañeros de mi universidad con perfil visible, con filtros de presupuesto, zona, mes de llegada y «tiene lugar».
3. Orden por coincidencias: hábitos iguales y traslape de presupuesto; sin porcentaje de afinidad.
4. Detalle de un compañero («Resumen del candidato»).
5. Contacto: abre (o reutiliza) una conversación de NV-16 de tipo `roommate` con un primer mensaje.
6. NV-16: conversaciones de tipo `roommate` sin cuarto; NV-20 avisa del mensaje como cualquier otro.
7. Vencimiento: el perfil deja de aparecer 60 días después de su última actualización.
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Porcentaje de afinidad o emparejamiento automático: Smart Match (NV-23).
- Carrera, semestre e insignia de estudiante verificado (NV-11 no los guarda todavía).
- Bloquear a otro usuario: se reporta con NV-21.
- Grupos de más de dos personas o rentar en conjunto (NV-18 es un convenio por estudiante).
- Fotos en el perfil de compañero.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/users/me/roommate-profile` | Autenticado con universidad en NV-11 | Crea o actualiza mi perfil de compañero | 200, 201, 400, 401, 409, 429, 503 |
| GET | `/api/v1/users/me/roommate-profile` | Autenticado | Mi perfil de compañero | 200, 401, 404, 429, 503 |
| DELETE | `/api/v1/users/me/roommate-profile` | Autenticado | Desactiva y borra mi perfil | 204, 401, 429, 503 |
| GET | `/api/v1/roommates` | Autenticado con perfil visible | Compañeros de mi universidad, ordenados por coincidencias | 200, 400, 401, 403, 429, 503 |
| GET | `/api/v1/roommates/{id}` | Autenticado con perfil visible | Resumen de un compañero | 200, 400, 401, 403, 404, 429, 503 |
| POST | `/api/v1/roommates/{id}/contact` | Autenticado con perfil visible | Abre la conversación `roommate` con `{ message }` | 201, 200, 400, 401, 403, 404, 409, 429, 503 |

- Perfil (entrada): `{ visible, budgetMin, budgetMax, zone: "walking" | "near_campus" | "any", moveIn: "YYYY-MM", durationMonths, hasPlace, habits: { schedule: "early" | "night" | "mixed", cleaning: "daily" | "weekly" | "relaxed", noise: "quiet" | "moderate" | "social", smoking: "no" | "outside" | "yes", pets: "none" | "cats" | "dogs" | "any", social: "rare" | "occasional" | "frequent" }, about? }`. `budgetMin` y `budgetMax` enteros MXN de 500 a 50000 con `budgetMin ≤ budgetMax`; `moveIn` entre el mes actual y 12 meses después; `durationMonths` de 1 a 24; `about` ≤ 300. La universidad sale del perfil de NV-11 (sin universidad → 409 `university_required`).
- Mi perfil (salida): la entrada más `id`, `university`, `expiresAt`, `createdAt`, `updatedAt`. PUT responde 201 la primera vez y 200 después; cada PUT renueva `expiresAt` (+60 días).
- Tarjeta de compañero: `{ id, name, university: { id, name }, budgetMin, budgetMax, zone, moveIn, durationMonths, hasPlace, habits, about, matches, updatedAt }`. `id` es el del perfil, nunca el del usuario; `name` es solo el primer nombre; sin correo, teléfono, apellidos ni ubicación. `matches` = hábitos iguales (0–6) + 1 si los presupuestos se traslapan.
- Lista: `budgetMax` (los que aceptan pagar hasta ese monto o menos), `zone`, `moveInFrom` y `moveInTo` (`YYYY-MM`), `hasPlace` (`true`/`false`), `page`, `pageSize` ≤ 50; orden `matches` desc, `updatedAt` desc e id. Sin perfil visible propio → 403 `roommate_profile_required`.
- Contacto: `{ message }` de 1 a 500 caracteres. La primera vez responde 201 `{ conversationId }`; si ya existe la conversación entre ambos, agrega el mensaje y responde 200 con el mismo id. El chat sigue en `/api/v1/conversations/{id}/messages` (NV-16).
- NV-16: `GET /conversations` agrega `kind` (`rental` | `roommate`); en `roommate`, `requestId` es `""`, `room` es `null` y `counterpart.role` es `student`. Las conversaciones de renta no cambian.
- Códigos nuevos: 403 `roommate_profile_required`, 409 `university_required`, 409 `own_profile` (contactarse a sí mismo), 409 `participant_suspended` (como NV-16).
- Todas las respuestas con `no-store`.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/roommate-profile
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { visible, budgetMin, budgetMax, zone: "walking" | "near_campus" | "any", moveIn: "YYYY-MM", durationMonths, hasPlace, habits: { schedule: "early" | "night" | "mixed", cleaning: "daily" | "weekly" | "relaxed", noise: "quiet" | "moderate" | "social", smoking: "no" | "outside" | "yes", pets: "none" | "cats" | "dogs" | "any", social: "rare" | "occasional" | "frequent" }, about? }
RESPONSE     201 | 200 mi perfil + { id, university, expiresAt, createdAt, updatedAt }
STATUS CODES 200, 201, 400, 401, 409 code=university_required, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/roommate-profile
METHOD       GET | DELETE
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 mi perfil | 204
STATUS CODES 200, 204, 401, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/roommates?budgetMax=2500&zone=walking&moveInFrom=2027-01&hasPlace=false&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      budgetMax, zone, moveInFrom, moveInTo, hasPlace, page, pageSize
RESPONSE     200 { items: [{ id, name, university: { id, name }, budgetMin, budgetMax, zone, moveIn, durationMonths, hasPlace, habits, about, matches, updatedAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403 code=roommate_profile_required, 429, 503
PAGINATION   Sí: orden matches desc, updatedAt desc, id
```

```text
ENDPOINT     /api/v1/roommates/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id del perfil
RESPONSE     200 { id, name, university: { id, name }, budgetMin, budgetMax, zone, moveIn, durationMonths, hasPlace, habits, about, matches, updatedAt }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/roommates/{id}/contact
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { message }
RESPONSE     201 | 200 { conversationId }
STATUS CODES 200, 201, 400, 401, 403, 404, 409 code=own_profile | participant_suspended, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Roommates** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `roommate_profiles` |
| Usa (puertos de otros módulos) | Profile `IProfileRepository` (NV-11) para el nombre y la universidad; Messaging `IConversationRepository` (NV-16) para abrir o reutilizar la conversación `roommate`; Moderation `IAccountSuspensionQuery` (NV-21) para ocultar y bloquear cuentas suspendidas |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `roommate_profiles` (colección nueva del módulo Roommates): `userId` (único), `universityId`, `visible`, `budgetMin`, `budgetMax`, `zone`, `moveIn`, `durationMonths`, `hasPlace`, los 6 hábitos, `about`, `expiresAt`, `createdAt`, `updatedAt`.
- Índices: único `{ userId }`; `{ universityId, visible, expiresAt }` para la lista.
- NV-16 `conversations` agrega `kind` (`rental` por defecto para las existentes, sin migración: un documento sin `kind` se lee como `rental`) y una llave única parcial `{ kind, pairKey }` para `roommate` (los dos ids ordenados), para no duplicar la conversación entre las mismas dos personas.
- Desactivar borra el perfil; las conversaciones ya abiertas se conservan.

## 9. Reglas de negocio

1. Solo estudiantes con universidad en NV-11 crean perfil; el perfil usa siempre esa universidad (si la cambian, el siguiente PUT la actualiza).
2. Para ver la lista, el detalle o contactar, el usuario necesita su propio perfil visible y vigente (403 `roommate_profile_required`).
3. La lista muestra solo perfiles visibles, vigentes (`expiresAt` futuro), de la misma universidad, distintos al propio y de cuentas no suspendidas (NV-21).
4. `matches` cuenta los hábitos iguales (máximo 6) más 1 si los rangos de presupuesto se traslapan; se calcula en el servidor y no es un porcentaje.
5. Contactar: no a uno mismo (409 `own_profile`); ninguno de los dos suspendido (409 `participant_suspended`); el mensaje se guarda en NV-16 y NV-20 avisa con `message_received`; una sola conversación `roommate` por pareja.
6. El perfil vence 60 días después de su último PUT; vencido no aparece, pero el dueño lo ve y lo renueva con otro PUT.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Reciprocidad: solo quien también aparece ve a los demás, y solo de su universidad; reduce la recolección masiva de perfiles.
- Nunca se exponen el id de usuario, el correo, el teléfono, los apellidos ni una ubicación; el contacto es solo por el chat de TheHouse (RNF-02).
- BOLA: el perfil propio sale del token; el detalle de otro solo si es visible, vigente y de la misma universidad (si no, 404).
- Rate limit: 60/min por usuario en lectura y edición; 10 contactos nuevos al día; Redis caído → 503.
- Entradas con campos desconocidos → 400; textos `about` y mensajes fuera de logs.
- Los usuarios se reportan con NV-21 (objetivo `user`) y la suspensión los oculta de la lista.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de compañero visible | 403 | Acceso denegado (`code`: `roommate_profile_required`) |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Sin universidad, contactarse a sí mismo o cuenta suspendida | 409 | Conflicto (`code`: `university_required`, `own_profile`, `participant_suspended`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-22-R01 | Mi perfil de compañero | PUT crea (201) o actualiza (200) con validación de presupuesto, zona, mes, duración, hábitos y `about`; usa la universidad de NV-11 (409 `university_required`); renueva `expiresAt`; GET y DELETE. | F1 | T01 |
| NV-22-R02 | Lista y coincidencias | Solo perfiles visibles, vigentes, de mi universidad, no propios y no suspendidos; filtros; `matches` = hábitos iguales + traslape de presupuesto; orden por `matches` y `updatedAt`; sin perfil propio 403. | F1 | T02 |
| NV-22-R03 | Detalle del compañero | Tarjeta con el id del perfil, primer nombre y universidad; ajeno de otra universidad, oculto o vencido 404. | F2 | T03 |
| NV-22-R04 | Contacto por chat | Abre una conversación `roommate` de NV-16 con el primer mensaje (201) o la reutiliza (200); no a uno mismo; suspensión 409; NV-20 avisa con `message_received`. | F2 | T04 |
| NV-22-R05 | Compatibilidad con NV-16 | `kind` en las conversaciones; las de renta siguen igual; las `roommate` sin cuarto ni solicitud; leer, enviar y marcar leído funcionan igual. | F2 | T05 |
| NV-22-R06 | Privacidad, autorización y límites | Sin id de usuario, correo, teléfono ni apellidos en ninguna respuesta; rate limits 429; Redis caído 503; campos desconocidos 400. | F3 | T06 |
| NV-22-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (dos estudiantes de la misma universidad: perfiles, lista con coincidencias, contacto y chat). | F4 | T07 |
| NV-22-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Modelo | Perfil de compañero voluntario por usuario (como el prototipo) en lugar de publicaciones sueltas; cambia la propuesta de 1.0.0 | APROBADO (M-BE-074) |
| Reciprocidad | Para ver a otros hay que tener el perfil propio visible; solo la misma universidad | APROBADO (M-BE-074) |
| Orden | Coincidencias de hábitos y presupuesto calculadas con una regla simple; el porcentaje de afinidad del prototipo queda para Smart Match (NV-23) | APROBADO (M-BE-074) |
| Contacto | Conversación de NV-16 de tipo `roommate` (sin cuarto), una por pareja; cambio aditivo en NV-16 (`kind`) | APROBADO (M-BE-074) |
| Vencimiento | 60 días desde la última actualización; se renueva con PUT | APROBADO (M-BE-074) |
| Datos mostrados | Primer nombre y universidad; sin carrera ni insignia hasta que NV-11 los tenga | APROBADO (M-BE-074) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección pequeña (`roommate_profiles`) y un campo e índice en `conversations` |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-22-RK01 | Acoso o mensajes no deseados | Seguridad de estudiantes | Reciprocidad, 10 contactos al día, reporte y suspensión con NV-21, sin teléfono |
| NV-22-RK02 | Recolección de perfiles | Privacidad | Solo con perfil propio, misma universidad, sin ids de usuario ni apellidos |
| NV-22-RK03 | Conversaciones duplicadas | Ruido | Llave única por pareja en `conversations` |
| NV-22-RK04 | La app anterior no conoce `kind` | Pantallas de chat incompletas | Campo agregado; las conversaciones de renta no cambian |

## 16. Dependencias

- NV-11 (universidad del perfil), NV-16 (chat), NV-20 (avisos) y NV-21 (reportes y suspensión).
- NV-23 (porcentaje de afinidad en una versión posterior).

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
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
