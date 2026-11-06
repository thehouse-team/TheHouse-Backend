# NV-21 — Verificación, reportes y moderación

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-37 (revisión), RF-38, RF-39, RF-40, RF-42, RF-43, RF-44, RF-79 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-03, RNF-19 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-12 y NV-13 |
| Relación | Aprueba edificios de NV-12, lee documentos de NV-13, oculta contenido de NV-14/NV-16/NV-19 y suspende cuentas (NV-10) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

Para prevenir fraude, el equipo de TheHouse revisa los documentos de cada edificio y lo aprueba, pide corrección o lo rechaza; los usuarios reportan publicaciones, cuentas, mensajes o reseñas sospechosas, y el administrador modera y suspende. Hoy no existen roles administrativos en el backend.

## 2. Objetivo

Definir roles administrativos, el flujo de revisión de edificios, los reportes y las acciones de moderación con auditoría inmutable.

## 3. Precondiciones

1. NV-12 y NV-13.
2. Decisión sobre la asignación de roles (§12).
3. Autorización «EJECUTA».

## 4. Alcance

1. Roles `reviewer` y `admin` en el token.
2. Cola y decisión de revisión de edificios.
3. Estado de verificación visible para el dueño.
4. Reportes de usuarios.
5. Suspender o retirar publicaciones y suspender cuentas.
6. Bitácora de auditoría.

## 5. Fuera de alcance

- Panel web de administración (cliente).
- Detección automática de fraude con ML.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/admin/buildings` | Revisor | Cola por `status` | 200, 401, 403 |
| GET | `/api/v1/admin/buildings/{id}` | Revisor | Detalle con enlaces temporales a documentos (5 min) | 200, 401, 403, 404 |
| POST | `/api/v1/admin/buildings/{id}:approve \| :request-changes \| :reject` | Revisor | Decisión con motivo | 200, 400, 403, 404, 409 |
| GET | `/api/v1/landlord/buildings/{id}/verification` | Dueño | Estado de revisión y motivo | 200, 404 |
| POST | `/api/v1/reports` | Autenticado | Reporta `{ targetType, targetId, reason, details }` | 201, 400, 401, 404, 429 |
| GET | `/api/v1/admin/reports` | Admin | Reportes por estado | 200, 401, 403 |
| POST | `/api/v1/admin/reports/{id}:resolve` | Admin | Resuelve con acción | 200, 400, 403, 404 |
| POST | `/api/v1/admin/buildings/{id}:suspend \| /admin/users/{id}:suspend` | Admin | Suspende publicación o cuenta | 200, 400, 403, 404, 409 |

- `targetType` = `room`, `building`, `user`, `message` o `review`; `reason` de catálogo (`fake`, `fraud`, `inappropriate`, `spam`, `other`); `details` ≤ 500.
- Las decisiones de revisión y moderación exigen `reason`.

## 7. Modelo de datos

- `users.roles[]` (`reviewer`, `admin`) incluido como claim en el JWT.
- `verification_reviews`: `buildingId`, `reviewerId`, `decision`, `reason`, `createdAt`.
- `reports`: `reporterId`, `targetType`, `targetId`, `reason`, `details`, `status` (`open`, `resolved`, `dismissed`), `resolution`.
- `audit_events`: `actorId`, `action`, `target`, `reason`, `createdAt`; solo inserción.

## 8. Reglas de negocio

1. Aprobar un edificio lo pasa a Verificado y sus cuartos Listos a Libres; pedir corrección lo regresa a Borrador con motivo; rechazar lo deja Rechazado.
2. Un revisor no puede revisar sus propios edificios.
3. Suspender una cuenta revoca todas sus sesiones (NV-10) y oculta sus publicaciones.
4. Un usuario solo reporta una vez el mismo objetivo mientras el reporte esté abierto.
5. Toda acción administrativa y toda lectura de documentos queda en `audit_events`.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Rutas `/admin/*` exigen rol; un usuario sin rol recibe 403 (OWASP API5, autorización a nivel de función).
- Los roles no se asignan por API: solo por una operación administrativa controlada (CLI o seed) con registro.
- Enlaces a documentos de 5 minutos, de un solo uso y auditados (RNF-19).
- `audit_events` no admite actualización ni borrado por la aplicación.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin el rol requerido | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Estado no permite la decisión | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-21-R01 | Roles administrativos | Claims `reviewer`/`admin` en el JWT; endpoints `/admin/*` responden 403 sin rol; asignación solo por CLI auditada. | F1 | T01 |
| NV-21-R02 | Revisión de edificios | Aprobar, pedir corrección y rechazar con motivo y transiciones válidas; efectos en cuartos; el dueño ve el estado y el motivo. | F1 | T02 |
| NV-21-R03 | Acceso auditado a documentos | El detalle de revisión entrega enlaces temporales de 5 minutos y registra cada acceso. | F2 | T03 |
| NV-21-R04 | Reportes | Crear reportes con catálogo de motivos; sin duplicados abiertos; cola de administración por estado. | F1 | T04 |
| NV-21-R05 | Moderación y suspensión | Suspender publicaciones y cuentas oculta el contenido, revoca sesiones y queda auditado; reversible por admin. | F2 | T05 |
| NV-21-R06 | Bitácora inmutable | `audit_events` solo de inserción; prueba que no existe ruta para modificarla. | F3 | T06 |
| NV-21-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Asignación de roles | Comando CLI `--grant-role` similar al seed, con registro en `audit_events` | PENDIENTE |
| Retención de reportes | 1 año | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | — |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-21-RK01 | Escalada de privilegios | Control total de la plataforma | Roles fuera de la API, pruebas de 403 en cada ruta |
| NV-21-RK02 | Revisión sesgada o autoaprobación | Fraude | Regla de no revisar lo propio y auditoría |
| NV-21-RK03 | Reportes abusivos | Moderación saturada | Rate limit y un reporte abierto por objetivo |

## 15. Dependencias

- NV-12 (estados), NV-13 (documentos), NV-10 (revocación).
- NV-14, NV-16 y NV-19 (contenido ocultable).
- NV-20 (notificar decisiones).

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
