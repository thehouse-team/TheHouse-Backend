# NV-17 — Agenda de visitas

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-80, RF-41 (dirección exacta al confirmar) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Módulo | Visits |
| Precondición | NV-16 |
| Relación | Una visita completada habilita la reseña (NV-19); los cambios generan notificaciones (NV-20) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El estudiante propone un horario de visita y el propietario lo confirma, reprograma o cancela desde su calendario. La dirección exacta del inmueble solo se muestra al estudiante cuando la visita está confirmada (RF-41, RNF-02).

## 2. Objetivo

Definir el ciclo de vida de las visitas, el calendario del propietario y la revelación controlada de la dirección exacta.

## 3. Precondiciones

1. NV-16 (solicitud aceptada o activa).
2. Autorización «EJECUTA».

## 4. Alcance

1. Proponer visita.
2. Confirmar, reprogramar y cancelar.
3. Calendario del propietario y lista del estudiante.
4. Dirección exacta solo en visitas confirmadas.
5. Marcar visita como realizada.

## 5. Fuera de alcance

- Sincronización con calendarios externos.
- Recordatorios por SMS o correo (solo notificación en la app, NV-20).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/visits` | Autenticado | Propone visita `{ requestId, proposedAt }` | 201, 400, 401, 404, 409 |
| GET | `/api/v1/users/me/visits` | Autenticado | Visitas del estudiante | 200, 401 |
| GET | `/api/v1/landlord/visits` | Arrendador | Calendario por rango `from`/`to` (máximo 62 días) | 200, 400, 401, 403 |
| GET | `/api/v1/visits/{id}` | Participante | Detalle; incluye dirección exacta solo si está confirmada | 200, 401, 404 |
| POST | `/api/v1/visits/{id}:confirm \| :reschedule \| :cancel \| :complete` | Participante según la acción | Cambia el estado | 200, 400, 404, 409 |

- Fechas en UTC con desplazamiento; el cliente muestra la hora de `America/Mexico_City`.
- `:reschedule` recibe `proposedAt` y queda pendiente de aceptación de la otra parte.
- `:cancel` recibe `reason`.

## 7. Módulo, datos propios e integración

Módulo **Visits** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `visits` |
| Usa (puertos de otros módulos) | Messaging `IRequestQuery` (NV-16); Listings `IRoomQuery` (NV-12) para la dirección exacta |
| Publica (eventos de dominio) | `VisitConfirmed`, `VisitRescheduled`, `VisitCancelled`, `VisitCompleted` |
| Consume (eventos de dominio) | `UserDeleted (NV-10)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `visits`: `requestId`, `roomId`, `studentId`, `ownerId`, `proposedAt`, `status` (`proposed`, `confirmed`, `rescheduled`, `cancelled`, `completed`), `history[]`, `updatedAt`; índices `{ ownerId, proposedAt }` y `{ studentId, proposedAt }`.

## 9. Reglas de negocio

1. Solo el estudiante de la solicitud propone; el horario debe ser futuro y dentro de 60 días.
2. El propietario confirma o reprograma; el estudiante acepta la reprogramación; cualquiera cancela con motivo.
3. Máximo 3 reprogramaciones por visita.
4. La dirección exacta se incluye solo para el estudiante con la visita confirmada y hasta 24 horas después del horario.
5. Solo el propietario marca `completed` después del horario; habilita la reseña (NV-19).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Participantes únicamente (BOLA).
- La dirección exacta no aparece en listados ni en notificaciones; solo en el detalle confirmado (RNF-02).
- Rate limit de propuestas 10/día por estudiante.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin perfil de arrendador | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Transición inválida o límite de reprogramaciones | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-17-R01 | Proponer visita | Solo con solicitud activa propia, horario futuro ≤ 60 días; 409 si ya hay una visita activa para la solicitud. | F1 | T01 |
| NV-17-R02 | Ciclo de estados | Confirmar, reprogramar (máximo 3), aceptar reprogramación, cancelar con motivo y completar, con transiciones válidas y `history`. | F1 | T02 |
| NV-17-R03 | Calendario y listas | Calendario del propietario por rango de hasta 62 días y lista del estudiante, paginados. | F2 | T03 |
| NV-17-R04 | Dirección exacta controlada | Visible solo para el estudiante con visita confirmada y dentro de la ventana; pruebas en cada estado. | F3 | T04 |
| NV-17-R05 | Persistencia e índices | Índices por propietario y estudiante con fecha; concurrencia optimista en cambios de estado. | F2 | T05 |
| NV-17-R06 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T06, T07, T08 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Ventana de dirección exacta | Desde la confirmación hasta 24 h después de la visita | PROPUESTO |
| Zona horaria | UTC en servidor; `America/Mexico_City` en cliente | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | — |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-17-RK01 | Revelar la dirección antes de tiempo | Seguridad del propietario | Regla en dominio y pruebas por estado |
| NV-17-RK02 | Conflictos de horario | Mala experiencia | Calendario y validación de traslapes como advertencia |
| NV-17-RK03 | Errores de zona horaria | Visitas a la hora equivocada | UTC con desplazamiento y pruebas |

## 16. Dependencias

- NV-16 (solicitudes).
- NV-19 (reseña tras visita completada).
- NV-20 (notificaciones).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 6 |
| Fases del plan | 4 |
| Tasks | 8 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
