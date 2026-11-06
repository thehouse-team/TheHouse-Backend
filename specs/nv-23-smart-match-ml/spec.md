# NV-23 — Smart Match — compatibilidad con Machine Learning

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-48 a RF-58, RF-64, RF-65, RF-68 (uso sin cuenta) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-13, RNF-14 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-11 y NV-14 |
| Relación | NV-24 explica los resultados; NV-25 mide su uso |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

Smart Match construye el perfil de búsqueda del usuario y calcula, para cada cuarto, un porcentaje de compatibilidad de 0 a 100 considerando presupuesto, distancia, servicios, modalidad, tipo de inmueble y preferencias adicionales; el usuario ordena por compatibilidad, da retroalimentación y descarta cuartos. Debe funcionar también sin cuenta (RF-68) y es una herramienta de apoyo, no una decisión automática (RES-05, RES-06).

## 2. Objetivo

Definir el cálculo de compatibilidad explicable y versionado, su exposición por API, la retroalimentación y la evolución de un modelo base a un modelo entrenado, a costo $0.

## 3. Precondiciones

1. NV-11 (preferencias) y NV-14 (cuartos publicables).
2. Decisión del enfoque de modelo (§12).
3. Autorización «EJECUTA».

## 4. Alcance

1. Perfil Smart Match a partir de las preferencias (RF-48).
2. Puntaje 0–100 por cuarto con desglose por factor (RF-49 a RF-56).
3. Recomendaciones ordenadas por compatibilidad (RF-57).
4. Recalculo al cambiar preferencias (RF-58).
5. Retroalimentación útil/no útil y descartes (RF-64, RF-65).
6. Uso sin cuenta enviando las preferencias en el cuerpo (RF-68).
7. Versionado y evaluación del modelo.

## 5. Fuera de alcance

- Explicación en lenguaje natural: NV-24.
- Métricas de administración: NV-25.
- Uso de datos personales no necesarios (nombre, correo, género) como variables (RNF-11).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/smart-match/scores` | Público o autenticado | Puntajes para `roomIds` (máximo 50) con preferencias del usuario o del cuerpo | 200, 400, 429 |
| GET | `/api/v1/smart-match/recommendations` | Autenticado | Cuartos ordenados por compatibilidad | 200, 400, 401 |
| POST | `/api/v1/smart-match/recommendations:query` | Público | Igual que el anterior para invitados con preferencias en el cuerpo | 200, 400, 429 |
| POST | `/api/v1/smart-match/feedback` | Autenticado | `{ roomId, useful }` | 204, 400, 401, 404 |
| POST | `/api/v1/smart-match/dismissals` | Autenticado | `{ roomId }` baja su prioridad | 204, 400, 401, 404 |

- Cada puntaje: `roomId`, `score` (entero 0–100), `factors { budget, distance, services, modality, propertyType, extras }` (0–100 cada uno), `modelVersion`.
- Los invitados envían el mismo objeto de preferencias de NV-11; no se persiste.

## 7. Modelo de datos

- Modelo base: suma ponderada de factores normalizados con pesos versionados en configuración (explicable por diseño).
- Modelo entrenado (fase 2): ML.NET con los mismos factores como variables, entrenado fuera de línea con retroalimentación; artefacto versionado e inmutable.
- `smart_match_feedback`: `userId`, `roomId`, `useful`, `modelVersion`, `createdAt`.
- `smart_match_dismissals`: `userId`, `roomId`, `createdAt`; índice único.

## 8. Reglas de negocio

1. El puntaje es determinista para las mismas preferencias, cuarto y `modelVersion` (RNF-13).
2. Cada factor y el total se acotan a 0–100.
3. Los cuartos descartados se mueven al final de las recomendaciones durante 30 días.
4. Cambiar las preferencias (`version` de NV-11) invalida cualquier caché de puntajes.
5. Un modelo entrenado solo reemplaza al base si mejora la métrica acordada (por ejemplo, NDCG@10) en un conjunto de validación, con evidencia.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Minimización de datos: las variables son solo preferencias de búsqueda y atributos públicos del cuarto (RNF-11).
- Rate limit 30/min por IP para invitados y 60/min por usuario.
- Máximo 50 cuartos por cálculo para acotar el costo de CPU.
- El modelo y sus pesos no se exponen; solo el desglose por factor.

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
| NV-23-R01 | Perfil Smart Match | Se arma desde las preferencias de NV-11 o del cuerpo para invitados, con las mismas validaciones. | F1 | T01 |
| NV-23-R02 | Cálculo de compatibilidad | Puntaje 0–100 con factores de presupuesto, distancia, servicios, modalidad, tipo y extras; determinista por versión; casos de prueba con resultados esperados. | F1 | T02 |
| NV-23-R03 | Recomendaciones ordenadas | Lista paginada ordenada por puntaje descendente con desempate estable; descartes al final. | F1 | T03 |
| NV-23-R04 | Retroalimentación y descartes | Se registran con `modelVersion`; idempotentes; descartes por 30 días. | F2 | T04 |
| NV-23-R05 | Rendimiento | p95 < 300 ms para 50 cuartos en el runner (RNF-14). | F2 | T05 |
| NV-23-R06 | Modelo entrenado y evaluación | Pipeline fuera de línea con ML.NET; métrica comparada contra el modelo base; promoción solo con evidencia y nueva `modelVersion`. | F2 | T06 |
| NV-23-R07 | Minimización y límites | Prueba que el cálculo no lee campos personales; límites de tamaño y rate limit verificados. | F3 | T07 |
| NV-23-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Enfoque del modelo | Fase 1: puntaje ponderado explicable; fase 2: ML.NET entrenado con retroalimentación | PENDIENTE |
| Pesos iniciales | Presupuesto 30, distancia 25, servicios 20, modalidad 10, tipo 10, extras 5 | PROPUESTO |
| Métrica de promoción | NDCG@10 sobre retroalimentación etiquetada | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| ML.NET y cálculo en el mismo contenedor | $0 | Sin servicios de ML administrados |
| Entrenamiento fuera de línea | $0 | En el runner de GitHub Actions o local |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-23-RK01 | Sesgo o discriminación | Recomendaciones injustas | Variables limitadas a preferencias y atributos del cuarto; revisión de factores |
| NV-23-RK02 | Pocos datos para entrenar | Modelo peor que el base | Mantener el base hasta tener evidencia |
| NV-23-RK03 | Costo de CPU | Latencia | Límite de 50 cuartos y caché por versión de preferencias |
| NV-23-RK04 | Usuarios lo toman como garantía | Expectativas falsas | Mensaje de apoyo (RES-06) en el cliente |

## 15. Dependencias

- NV-11 (preferencias) y NV-14 (cuartos).
- NV-24 (explicaciones) y NV-25 (métricas).

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
