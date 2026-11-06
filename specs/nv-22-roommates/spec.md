# NV-22 — Búsqueda de compañero de vivienda

| Campo | Valor |
| --- | --- |
| ID | NV-22 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-46, RF-47 (consulta de publicaciones de compañeros) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-03 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-11 y NV-16 |
| Relación | El filtro de cuartos compartidos (RF-45, RF-47) vive en NV-12 y NV-14; el contacto usa el chat de NV-16 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

Un estudiante puede publicar que busca a una persona para compartir vivienda, y otros estudiantes pueden consultar esas publicaciones por universidad y presupuesto y contactarlo por chat.

## 2. Objetivo

Definir publicaciones de búsqueda de compañero, su consulta y el contacto por chat, con privacidad y caducidad.

## 3. Precondiciones

1. NV-11 (universidad) y NV-16 (chat).
2. Autorización «EJECUTA».

## 4. Alcance

1. Crear, editar y retirar la publicación propia.
2. Consultar publicaciones con filtros.
3. Contacto por conversación de tipo compañero.

## 5. Fuera de alcance

- Emparejamiento automático de compañeros.
- Verificación de identidad de estudiantes más allá de la insignia (NV-10).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/roommate-posts` | Autenticado | Crea la publicación | 201, 400, 401, 409 |
| GET | `/api/v1/roommate-posts` | Autenticado | Consulta por `universityId` y `maxBudget` | 200, 400, 401 |
| GET/PATCH/DELETE | `/api/v1/roommate-posts/{id}` | Autenticado / autor | Detalle, edición y retiro | 200, 204, 400, 404 |
| POST | `/api/v1/roommate-posts/{id}/contact` | Autenticado | Abre conversación con el autor | 201, 404, 409, 429 |

- Publicación: `universityId`, `budgetMax`, `moveInFrom`, `livingWith`, `about` (≤ 500), `hasPlace` (si ya tiene vivienda y busca con quién compartirla).

## 7. Modelo de datos

- `roommate_posts`: `authorId`, campos, `status` (`active`, `closed`, `expired`), `expiresAt`, `createdAt`; índice `{ universityId, status, budgetMax }` y TTL lógico a 60 días.

## 8. Reglas de negocio

1. Una publicación activa por usuario (409).
2. Caduca a los 60 días; el autor puede renovarla.
3. La publicación muestra el nombre visible y la insignia de estudiante, nunca correo ni teléfono.
4. No se puede contactar la propia publicación.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Consulta solo con sesión.
- Rate limit: 10 contactos/día por usuario.
- Publicaciones reportables y ocultables por NV-21.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Publicación activa duplicada | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-22-R01 | Publicación propia | Crear, editar y retirar; una activa por usuario; validación de campos. | F1 | T01 |
| NV-22-R02 | Consulta con filtros | Por universidad y presupuesto, solo activas y no expiradas, paginada. | F1 | T02 |
| NV-22-R03 | Contacto por chat | Crea o reutiliza una conversación de tipo compañero con el autor; límites diarios. | F2 | T03 |
| NV-22-R04 | Caducidad y privacidad | Expiración a 60 días; ninguna respuesta incluye correo ni teléfono. | F3 | T04 |
| NV-22-R05 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T05, T06, T07 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Caducidad | 60 días con renovación | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | — |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-22-RK01 | Perfiles falsos | Riesgo personal | Insignia de estudiante visible y reportes |
| NV-22-RK02 | Contacto masivo | Acoso | Límites diarios y suspensión |

## 15. Dependencias

- NV-11, NV-16 y NV-21.
- NV-10 (insignia de estudiante).

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 5 |
| Fases del plan | 4 |
| Tasks | 7 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
