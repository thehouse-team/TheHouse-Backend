# NV-24 — Smart Match — explicaciones con IA generativa

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-59, RF-60, RF-61, RF-62, RF-63 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-15, RNF-16 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-23 |
| Relación | Explica los puntajes de NV-23; si no está disponible, NV-23 sigue funcionando (RNF-16) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

La IA generativa debe explicar en lenguaje sencillo por qué un cuarto obtuvo su porcentaje, qué coincide y qué no con las preferencias, usando solo información registrada (RF-62) y sin inventar características, distancias, precios ni condiciones (RNF-15). Los modelos de lenguaje de pago no cumplen COST-001 y las descripciones escritas por propietarios pueden contener instrucciones maliciosas (prompt injection).

## 2. Objetivo

Definir explicaciones fundamentadas en datos, con validación de alucinaciones, respaldo por plantilla y protección contra inyección de instrucciones.

## 3. Precondiciones

1. NV-23 (puntaje y factores).
2. Decisión de proveedor de IA (§12).
3. Autorización «EJECUTA».

## 4. Alcance

1. Explicación por cuarto con resumen, ventajas y desventajas.
2. Hechos estructurados como única fuente.
3. Validador de alucinaciones.
4. Respaldo por plantilla determinista.
5. Caché por cuarto, versión de preferencias y versión del modelo.

## 5. Fuera de alcance

- Chat conversacional libre con la IA.
- Envío de datos personales al proveedor.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/smart-match/explanations/{roomId}` | Autenticado | Explicación con las preferencias guardadas | 200, 401, 404, 429 |
| POST | `/api/v1/smart-match/explanations:query` | Público | Explicación para invitados con preferencias en el cuerpo | 200, 400, 404, 429 |

- Respuesta: `summary`, `pros[]`, `cons[]`, `score`, `generatedBy` (`llm` o `template`), `modelVersion`, `promptVersion`.

## 7. Modelo de datos

- Hechos de entrada: precio, presupuesto, distancia, minutos a pie, servicios del cuarto y del usuario, modalidad, tipo y factores de NV-23; nunca la descripción libre del propietario ni datos personales.
- `smart_match_explanations` (caché): clave `{ roomId, preferencesVersion, modelVersion, promptVersion }`, TTL de 24 h.

## 8. Reglas de negocio

1. La explicación solo menciona hechos de la entrada; el validador rechaza cualquier número, servicio o condición que no esté en ella y usa la plantilla.
2. Si el proveedor tarda más de 3 s o falla, se responde con la plantilla (RNF-16).
3. Lenguaje sencillo, sin porcentajes técnicos por factor (RF-63); máximo 600 caracteres.
4. El prompt está versionado (`promptVersion`) y se prueba con casos fijos.

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Prompt injection: la entrada es JSON estructurado generado por el servidor; los textos libres del propietario se excluyen.
- Sin datos personales hacia el proveedor (RNF-11); credenciales del proveedor como secreto de Container Apps (M-BE-038).
- Rate limit 20/min por usuario y 10/min por IP de invitado; caché para no repetir llamadas.
- La salida se trata como texto no confiable: sin HTML ni enlaces.

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
| NV-24-R01 | Explicación fundamentada | Resumen, ventajas y desventajas derivados solo de los hechos de entrada; casos de prueba con hechos fijos. | F1 | T01 |
| NV-24-R02 | Validación anti-alucinación | Un validador detecta números, servicios o condiciones ausentes de los hechos y cambia a plantilla; pruebas con salidas manipuladas. | F1 | T02 |
| NV-24-R03 | Respaldo por plantilla | Con el proveedor caído o lento (> 3 s) responde la plantilla con `generatedBy=template`; el puntaje sigue disponible. | F2 | T03 |
| NV-24-R04 | Caché y costo | Caché por cuarto y versiones con TTL de 24 h; límites que mantienen el uso dentro del plan gratuito. | F2 | T04 |
| NV-24-R05 | Protección contra inyección | Pruebas con descripciones maliciosas que no llegan al prompt ni alteran la salida. | F3 | T05 |
| NV-24-R06 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T06, T07, T08 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Proveedor de IA generativa | Plantillas deterministas como base ($0); un modelo de lenguaje solo con plan gratuito, términos de no retención de datos y aprobación | PENDIENTE |
| Idioma | Español neutro de México | PROPUESTO |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Plantillas deterministas | $0 | Siempre disponibles |
| Proveedor de modelo de lenguaje | $0 solo con plan gratuito | PENDIENTE DE VERIFICAR; si no, solo plantillas (BLOCKED la parte `llm`) |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-24-RK01 | Alucinaciones | Información falsa al usuario | Hechos estructurados y validador |
| NV-24-RK02 | Prompt injection | Mensajes manipulados | Excluir textos libres y validar salida |
| NV-24-RK03 | Costo del proveedor | Incumplir COST-001 | Plantillas por defecto y caché |
| NV-24-RK04 | Fuga de datos al proveedor | Privacidad | Solo hechos no personales |

## 15. Dependencias

- NV-23 (puntajes y factores).
- NV-11 (versión de preferencias).

## 16. Trazabilidad y cobertura

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

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
