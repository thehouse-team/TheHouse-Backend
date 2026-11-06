# NV-25 — Estadísticas del propietario y métricas de Smart Match

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-86, RF-66 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-05, RNF-11 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-14, NV-16 y NV-23 |
| Relación | Consume eventos de vistas (NV-14), favoritos (NV-15), solicitudes (NV-16) y Smart Match (NV-23) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El propietario quiere ver vistas, contactos y recomendaciones para mejorar sus anuncios, y el administrador necesita métricas básicas de Smart Match (compatibilidad promedio, interacción con recomendaciones y retroalimentación).

## 2. Objetivo

Definir el registro de eventos agregados y los tableros de estadísticas sin exponer datos personales.

## 3. Precondiciones

1. NV-14, NV-15, NV-16 y NV-23.
2. Autorización «EJECUTA».

## 4. Alcance

1. Registro de vistas de detalle deduplicadas.
2. Agregados diarios por cuarto y edificio.
3. Estadísticas del propietario con recomendaciones por reglas.
4. Métricas de Smart Match para administradores.

## 5. Fuera de alcance

- Analítica de terceros o SDK de seguimiento.
- Estadísticas por usuario individual.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/landlord/stats` | Arrendador | Vistas, favoritos, solicitudes y conversión por rango (máximo 90 días) | 200, 400, 401, 403 |
| GET | `/api/v1/landlord/stats/recommendations` | Arrendador | Sugerencias para mejorar anuncios | 200, 401, 403 |
| GET | `/api/v1/admin/smart-match/metrics` | Admin | Puntaje promedio, CTR, utilidad y descartes por versión | 200, 401, 403 |

- Las recomendaciones son reglas transparentes (por ejemplo, "agrega fotos" si el cuarto tiene menos de 2, o "precio arriba de la mediana de la zona").

## 7. Modelo de datos

- `room_events`: `roomId`, `type` (`view`, `favorite`, `request`, `recommendation_shown`, `recommendation_clicked`), `day`, `actorHash` (HMAC diario para deduplicar sin identificar), TTL de 180 días.
- `room_stats_daily`: `roomId`, `buildingId`, `ownerId`, `day`, contadores; índice `{ ownerId, day }`.

## 8. Reglas de negocio

1. Una vista por usuario y cuarto por día.
2. Los agregados se calculan con una tarea diaria idempotente.
3. Los tableros solo muestran agregados; nunca quién vio o guardó un cuarto.
4. Las métricas de Smart Match se separan por `modelVersion`.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- El propietario solo ve estadísticas de sus cuartos (BOLA); métricas de Smart Match solo con rol admin (NV-21).
- `actorHash` con llave rotada diariamente para que no permita rastrear usuarios (RNF-11).
- Rate limit 30/min por usuario.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-25-R01 | Registro de eventos | Vistas deduplicadas por día y eventos de favoritos, solicitudes y recomendaciones con `actorHash` no reversible. | F1 | T01 |
| NV-25-R02 | Agregados diarios | Tarea idempotente que produce contadores por cuarto y día; reejecución sin duplicar. | F2 | T02 |
| NV-25-R03 | Estadísticas del propietario | Rango de hasta 90 días con vistas, favoritos, solicitudes y conversión; solo cuartos propios. | F1 | T03 |
| NV-25-R04 | Recomendaciones por reglas | Reglas documentadas y probadas con casos que disparan cada sugerencia. | F1 | T04 |
| NV-25-R05 | Métricas de Smart Match | Puntaje promedio, CTR, tasa de utilidad y descartes por versión de modelo; solo admin. | F2 | T05 |
| NV-25-R06 | Privacidad de la analítica | Ninguna respuesta permite identificar usuarios; prueba con eventos de un mismo usuario en dos días. | F3 | T06 |
| NV-25-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Ejecución de la tarea diaria | Container Apps Job programado (dentro de la concesión gratuita) o tarea en segundo plano del API | PENDIENTE |
| Retención de eventos | 180 días | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | TTL controla el volumen |
| Tarea diaria | $0 dentro de la concesión de Container Apps | PENDIENTE DE VERIFICAR según la opción elegida |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-25-RK01 | Reidentificación de usuarios | Privacidad | HMAC con llave rotada y solo agregados |
| NV-25-RK02 | Crecimiento de eventos | Límite de 512 MB | TTL y agregados |
| NV-25-RK03 | Métricas engañosas | Decisiones erróneas | Definiciones documentadas por métrica |

## 15. Dependencias

- NV-14, NV-15, NV-16 y NV-23 (eventos).
- NV-21 (rol admin).

## 16. Trazabilidad y cobertura

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

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
