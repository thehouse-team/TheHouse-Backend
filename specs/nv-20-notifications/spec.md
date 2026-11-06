# NV-20 — Notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-75 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Módulo | Notifications |
| Precondición | NV-16 a NV-18 (eventos) |
| Relación | Recibe eventos de NV-12, NV-16, NV-17, NV-18 y NV-21 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El usuario consulta sus notificaciones, filtra las no leídas y las marca como leídas. Los eventos vienen de solicitudes, mensajes, visitas, cierre de trato y verificación de edificios.

## 2. Objetivo

Definir un buzón de notificaciones en la app alimentado por eventos de dominio, con consulta eficiente del contador de no leídas.

## 3. Precondiciones

1. Specs emisoras de eventos aprobadas.
2. Autorización «EJECUTA».

## 4. Alcance

1. Registro de notificaciones a partir de eventos de dominio.
2. Listado con filtro de no leídas.
3. Marcar una o todas como leídas.
4. Contador de no leídas.

## 5. Fuera de alcance

- Notificaciones push (FCM): decisión pendiente.
- Correo y SMS.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me/notifications` | Autenticado | Lista paginada; `unread=true` opcional | 200, 401 |
| GET | `/api/v1/users/me/notifications/unread-count` | Autenticado | Contador | 200, 304, 401 |
| PATCH | `/api/v1/users/me/notifications/{id}` | Dueño | Marca `{ read: true }` | 200, 400, 404 |
| POST | `/api/v1/users/me/notifications:mark-all-read` | Autenticado | Marca todas como leídas | 204, 401 |

- Notificación: `id`, `type`, `title`, `body`, `target { kind, id }`, `read`, `createdAt`; `type` de catálogo cerrado (`request_received`, `request_accepted`, `message_received`, `visit_confirmed`, `agreement_active`, `building_verified`, etc.).

## 7. Módulo, datos propios e integración

Módulo **Notifications** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `notifications` |
| Usa (puertos de otros módulos) | — |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | `RequestCreated, RequestAccepted, RequestRejected y MessageSent (NV-16)`, `VisitConfirmed, VisitRescheduled y VisitCancelled (NV-17)`, `AgreementIssued, AgreementActivated y AgreementEnded (NV-18)`, `BuildingReviewDecided (NV-21)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `notifications`: `userId`, `type`, `title`, `body`, `target`, `readAt`, `createdAt`; índices `{ userId, createdAt: -1 }` y `{ userId, readAt }`; TTL de 90 días.

## 9. Reglas de negocio

1. Los textos no incluyen dirección exacta, teléfono ni contenido completo de mensajes.
2. Mensajes de chat se agrupan: una notificación por conversación mientras haya no leídos.
3. Las notificaciones se crean en el mismo flujo del evento o con reintento idempotente (clave del evento).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Solo el dueño lee y marca sus notificaciones (BOLA).
- Sin datos sensibles en el cuerpo (RNF-02).
- El contador usa `ETag` con `Cache-Control: private, no-cache` para que el sondeo del cliente sea barato sin que ninguna caché compartida lo almacene (constitution §23).

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-20-R01 | Generación por eventos | Cada evento del catálogo crea una notificación idempotente para el destinatario correcto. | F1 | T01 |
| NV-20-R02 | Consulta y filtro | Lista paginada por fecha con filtro de no leídas. | F1 | T02 |
| NV-20-R03 | Marcar como leídas | Una o todas; idempotente. | F1 | T03 |
| NV-20-R04 | Contador eficiente | Contador con índice y `ETag`; p95 < 100 ms. | F2 | T04 |
| NV-20-R05 | Retención | TTL de 90 días; sin datos sensibles en el cuerpo. | F2 | T05 |
| NV-20-R06 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T06, T07, T08 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Notificaciones push | FCM (gratuito) como extensión; el cuerpo push sin datos personales | PENDIENTE |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | TTL controla el volumen |
| FCM | $0 | Solo si se aprueba la decisión |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-20-RK01 | Fuga de datos en notificaciones | Privacidad | Plantillas sin datos sensibles |
| NV-20-RK02 | Notificaciones duplicadas | Ruido | Clave idempotente por evento |

## 16. Dependencias

- NV-12, NV-16, NV-17, NV-18 y NV-21 (emisores).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 6 |
| Fases del plan | 3 |
| Tasks | 8 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
