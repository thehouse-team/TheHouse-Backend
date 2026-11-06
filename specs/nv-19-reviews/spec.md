# NV-19 — Reseñas y calificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-85 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-03, RNF-09 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-17 y NV-18 |
| Relación | Las reseñas se muestran en el detalle de NV-14 y se reportan en NV-21 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El estudiante que visitó o rentó un cuarto puede calificar la propiedad y al propietario y dejar una reseña. Las calificaciones deben ser confiables: solo quien tuvo una visita completada o una renta puede opinar.

## 2. Objetivo

Definir reseñas verificadas por elegibilidad, sus agregados y su consulta.

## 3. Precondiciones

1. NV-17 (visita completada) o NV-18 (renta).
2. Autorización «EJECUTA».

## 4. Alcance

1. Crear y editar la reseña propia (30 días).
2. Listar reseñas de un cuarto.
3. Promedios por cuarto y por propietario.

## 5. Fuera de alcance

- Respuesta del propietario a la reseña.
- Moderación automática de lenguaje (los reportes van a NV-21).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/rooms/{id}/reviews` | Autenticado y elegible | Crea reseña | 201, 400, 401, 403, 404, 409 |
| PATCH | `/api/v1/reviews/{id}` | Autor | Edita dentro de 30 días | 200, 400, 404, 409 |
| GET | `/api/v1/rooms/{id}/reviews` | Autenticado | Reseñas paginadas y promedio | 200, 401, 404 |

- Reseña: `propertyRating` y `landlordRating` (enteros 1–5) y `comment` opcional (≤ 1000).

## 7. Modelo de datos

- `reviews`: `roomId`, `buildingId`, `ownerId`, `authorId`, `propertyRating`, `landlordRating`, `comment`, `status` (`published`, `hidden`), fechas; índice único `{ authorId, roomId }`.
- Agregados en `rooms` y `landlord_profiles`: `ratingAvg`, `ratingCount`.

## 8. Reglas de negocio

1. Elegible: visita `completed` (NV-17) o convenio `active`/`ended` (NV-18) del autor para ese cuarto.
2. Una reseña por autor y cuarto; editable 30 días.
3. El propietario no puede reseñar sus propios cuartos.
4. Una reseña oculta por moderación no cuenta en los promedios.
5. El autor de una cuenta eliminada se muestra como "Usuario eliminado".

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Elegibilidad verificada en el servidor (no confiar en el cliente).
- Sin correo ni datos del autor más allá del nombre visible.
- Rate limit 10 reseñas/día por usuario.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| No elegible | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Reseña duplicada o fuera de plazo | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-19-R01 | Reseña verificada | Solo autores elegibles crean reseña; duplicado 409; propietario 403. | F1 | T01 |
| NV-19-R02 | Edición acotada | El autor edita dentro de 30 días; después 409. | F1 | T02 |
| NV-19-R03 | Consulta y promedios | Lista paginada por fecha y promedios recalculados al crear, editar u ocultar. | F2 | T03 |
| NV-19-R04 | Integridad de agregados | Promedios consistentes con las reseñas publicadas tras operaciones concurrentes. | F2 | T04 |
| NV-19-R05 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T05, T06, T07 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Visibilidad de reseñas | Requieren sesión, igual que el detalle (RF-24) | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | — |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-19-RK01 | Reseñas falsas | Pérdida de confianza | Elegibilidad estricta y reportes |
| NV-19-RK02 | Promedios desincronizados | Datos incorrectos | Recalculo transaccional y prueba |

## 15. Dependencias

- NV-17 y NV-18 (elegibilidad).
- NV-14 (detalle).
- NV-21 (ocultar reseñas).

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
