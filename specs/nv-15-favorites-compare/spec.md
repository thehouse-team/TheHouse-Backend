# NV-15 — Favoritos y comparación

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-31, RF-32, RF-33 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-09 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-14 |
| Relación | Usa los cuartos públicos de NV-14; los favoritos alimentan las métricas de NV-25 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El estudiante guarda cuartos en favoritos, los consulta después y compara hasta cuatro opciones por precio, ubicación, servicios y características. Guardar favoritos requiere sesión (RF-69).

## 2. Objetivo

Definir una lista de favoritos idempotente por usuario y una comparación de cuartos con datos públicos.

## 3. Precondiciones

1. NV-14 (cuartos publicables).
2. Autorización «EJECUTA».

## 4. Alcance

1. Agregar y quitar favoritos (idempotente).
2. Listar favoritos con estado de disponibilidad.
3. Comparar de 2 a 4 cuartos.

## 5. Fuera de alcance

- Listas compartidas o carpetas.
- Alertas de cambio de precio (posible extensión de NV-20).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/users/me/favorites/{roomId}` | Autenticado | Agrega a favoritos (idempotente) | 204, 400, 401, 404, 409 |
| DELETE | `/api/v1/users/me/favorites/{roomId}` | Autenticado | Quita de favoritos (idempotente) | 204, 400, 401 |
| GET | `/api/v1/users/me/favorites` | Autenticado | Favoritos paginados con `available` | 200, 401 |
| POST | `/api/v1/rooms/compare` | Autenticado | Compara `{ roomIds: [2..4] }` | 200, 400, 401, 404 |

- La comparación devuelve, por cuarto, precio, distancia, minutos a pie, servicios, tipo, baño, tamaño, contrato mínimo y si está disponible.

## 7. Modelo de datos

- `favorites`: `userId`, `roomId`, `createdAt`; índice único `{ userId, roomId }` e índice `{ userId, createdAt: -1 }`.

## 8. Reglas de negocio

1. Máximo 200 favoritos por usuario (409 al exceder).
2. Un cuarto que deja de estar Libre permanece en favoritos con `available=false` y sin datos sensibles.
3. Agregar un favorito existente o quitar uno inexistente responde 204.
4. La comparación solo acepta cuartos publicables; ids duplicados o fuera de rango 400.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Favoritos visibles solo para su dueño.
- La comparación no expone campos distintos a los del detalle público de NV-14.
- Rate limit de escritura 60/min por usuario.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Límite de favoritos | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-15-R01 | Agregar y quitar favoritos | `PUT` y `DELETE` idempotentes con 204; cuarto inexistente o no publicable 404; límite de 200. | F1 | T01 |
| NV-15-R02 | Consultar favoritos | Lista paginada por fecha descendente con datos públicos y `available`. | F1 | T02 |
| NV-15-R03 | Comparación de cuartos | 2 a 4 ids únicos; respuesta con los mismos campos para cada cuarto; 404 si alguno no es publicable. | F1 | T03 |
| NV-15-R04 | Persistencia e índices | Índice único evita duplicados en concurrencia; consulta del listado sin N+1. | F2 | T04 |
| NV-15-R05 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T05, T06, T07 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Límite de favoritos | 200 por usuario | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Documentos pequeños |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-15-RK01 | Duplicados por doble toque | Datos inconsistentes | Índice único e idempotencia |
| NV-15-RK02 | N+1 al listar | Latencia | Consulta por lotes de cuartos |

## 15. Dependencias

- NV-14 (cuartos públicos).
- NV-10 (eliminar cuenta borra favoritos).
- NV-25 (conteo de favoritos).

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 5 |
| Fases del plan | 3 |
| Tasks | 7 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
