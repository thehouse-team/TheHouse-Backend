# NV-16 — Contacto, solicitudes y chat

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-34, RF-35 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-03 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Módulo | Messaging |
| Precondición | NV-12 y NV-14 |
| Relación | Una solicitud aceptada habilita visitas (NV-17) y cierre de trato (NV-18); los eventos van a NV-20 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El estudiante contacta al propietario desde un cuarto con preguntas rápidas, un mensaje y un horario propuesto, y conversa con él por chat dentro de la app sin compartir su teléfono. El propietario consulta las solicitudes por estado y las acepta o rechaza con motivo.

## 2. Objetivo

Definir las solicitudes de interés con su ciclo de estados y un chat entre las dos partes, privado y sin exponer datos de contacto.

## 3. Precondiciones

1. NV-12 (cuartos y dueño).
2. NV-14 (cuartos publicables).
3. Autorización «EJECUTA».

## 4. Alcance

1. Crear solicitud con preguntas rápidas, mensaje y horario propuesto.
2. Bandejas de solicitudes del estudiante y del propietario.
3. Aceptar o rechazar con motivo.
4. Conversaciones y mensajes con consulta incremental.

## 5. Fuera de alcance

- Tiempo real con WebSockets o SignalR (decisión futura; se empieza con consulta incremental).
- Adjuntos en el chat.
- Videollamadas.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/rooms/{id}/requests` | Autenticado | Crea solicitud y conversación | 201, 400, 401, 404, 409, 429 |
| GET | `/api/v1/users/me/requests` | Autenticado | Solicitudes enviadas | 200, 401 |
| GET | `/api/v1/landlord/requests` | Arrendador | Solicitudes recibidas por `status` | 200, 401, 403 |
| POST | `/api/v1/landlord/requests/{id}:accept \| :reject` | Dueño del cuarto | Acepta o rechaza con motivo | 200, 400, 404, 409 |
| GET | `/api/v1/conversations` | Participante | Conversaciones del usuario | 200, 401 |
| GET | `/api/v1/conversations/{id}/messages` | Participante | Mensajes desde un cursor `after` | 200, 304, 401, 404 |
| POST | `/api/v1/conversations/{id}/messages` | Participante | Envía mensaje `{ text }` | 201, 400, 401, 404, 409, 429 |

- Solicitud: `quickQuestions[]` (catálogo cerrado), `message` (≤ 500), `proposedVisitAt` opcional (futuro, ISO 8601).
- Rechazo: `reason` (`not_available`, `profile_mismatch`, `other`) y `note` opcional (≤ 300).
- Mensaje: `text` de 1 a 1000 caracteres; la respuesta de mensajes usa cursor y `ETag` para consultas incrementales baratas.

## 7. Módulo, datos propios e integración

Módulo **Messaging** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `rental_requests`, `conversations`, `messages` |
| Usa (puertos de otros módulos) | Search `IRoomCatalogQuery` (NV-14); Listings `IRoomQuery` (NV-12) para conocer al dueño |
| Publica (eventos de dominio) | `RequestCreated`, `RequestAccepted`, `RequestRejected`, `MessageSent` |
| Consume (eventos de dominio) | `AgreementActivated (NV-18)`, `UserSuspended (NV-21)`, `UserDeleted (NV-10)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `rental_requests`: `roomId`, `buildingId`, `studentId`, `ownerId`, `quickQuestions`, `message`, `proposedVisitAt`, `status` (`pending`, `accepted`, `rejected`, `cancelled`, `closed`), `reason`, fechas; índice único parcial `{ studentId, roomId }` para solicitudes activas.
- `conversations`: `participants[2]`, `requestId`, `lastMessageAt`, `unread` por participante.
- `messages`: `conversationId`, `senderId`, `text`, `createdAt`; índice `{ conversationId, _id }`.

## 9. Reglas de negocio

1. Una sola solicitud activa por estudiante y cuarto (409).
2. El propietario no puede solicitar su propio cuarto.
3. Solo solicitudes pendientes se aceptan o rechazan; rechazar exige motivo.
4. Cuando el cuarto pasa a Rentado (NV-18) las demás solicitudes activas se cierran con motivo `not_available`.
5. Los mensajes son inmutables; un usuario suspendido (NV-21) no puede enviar.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Solo los dos participantes leen o escriben una conversación (BOLA).
- Teléfono y correo nunca se incluyen en respuestas de solicitudes ni de chat (RNF-02).
- Rate limit: 10 solicitudes/día por estudiante y 30 mensajes/min por usuario.
- El contenido de los mensajes no se registra en logs; se puede reportar a moderación (NV-21).

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Solicitud activa duplicada o estado inválido | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-16-R01 | Crear solicitud | Valida preguntas, mensaje y horario futuro; crea solicitud y conversación; 409 con solicitud activa; 404 para cuartos no publicables. | F1 | T01 |
| NV-16-R02 | Bandejas y estados | Estudiante y propietario ven solo sus solicitudes, filtrables por estado y paginadas. | F1 | T02 |
| NV-16-R03 | Aceptar y rechazar | Transiciones válidas desde `pending`; rechazo con motivo; evento para NV-20. | F1 | T03 |
| NV-16-R04 | Chat por consulta incremental | Mensajes ordenados con cursor; `304` si no hay nuevos; envío solo por participantes. | F2 | T04 |
| NV-16-R05 | Persistencia e índices | Índice único parcial de solicitudes activas; índices de mensajes por conversación. | F2 | T05 |
| NV-16-R06 | Privacidad y abuso | Ninguna respuesta incluye teléfono ni correo; rate limits verificados; usuario suspendido no puede escribir. | F3 | T06 |
| NV-16-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Tiempo real | Consulta incremental en la primera versión; SignalR con backplane Redis como evolución | PROPUESTO |
| Retención de mensajes | Mientras exista la cuenta; se anonimizan al eliminarla | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Volumen de mensajes dentro del plan gratuito; vigilar el límite de comandos de Upstash |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-16-RK01 | Lectura de conversaciones ajenas | Privacidad | Filtro por participante y pruebas BOLA |
| NV-16-RK02 | Spam a propietarios | Abuso | Límites diarios y reportes |
| NV-16-RK03 | Consulta incremental costosa | Consumo de Redis y CPU | Cursor, `ETag` e intervalo mínimo sugerido al cliente |

## 16. Dependencias

- NV-12 y NV-14.
- NV-17 y NV-18 consumen la solicitud aceptada.
- NV-20 (notificaciones) y NV-21 (reportes y suspensión).

## 17. Trazabilidad y cobertura

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

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
