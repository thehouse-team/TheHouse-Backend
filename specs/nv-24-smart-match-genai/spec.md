# NV-24 — Smart Match — explicaciones con TheHouse-GenAI

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-59, RF-60, RF-61, RF-62, RF-63 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-15, RNF-16 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | SmartMatchExplanations |
| Precondición | Constitution 1.2.0 (M-BE-062) y NV-23 |
| Relación | Explica los puntajes de NV-23; consume `TheHouse-GenAI` (specs 003, 005 y 010); si no está disponible, responde con plantilla (RNF-16) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-10):** se adapta a la constitution 1.2.0 §6.1 (M-BE-062). El backend ya no llama a un proveedor de modelos de lenguaje: consume el servicio auxiliar `TheHouse-GenAI` (repositorio de Isai) por un puerto con timeout, un reintento, circuit breaker y **degradación segura** a una explicación por plantilla. Las ventajas y desventajas salen siempre del desglose de NV-23 (deterministas y verificables); GenAI solo redacta el resumen con el contrato `explain-match` de su spec 003, y el backend valida que no cite hechos ausentes. GenAI está desactivado por defecto: NV-24 se completa solo con plantillas y GenAI se activa cuando estén aprobados su hosting y su proveedor (decisiones M-BE con la evidencia de §17). Rutas `/query` en lugar de `:query`.

## 1. Contexto

El estudiante debe entender por qué un cuarto tiene su porcentaje: qué coincide y qué no con lo que busca (RF-59 a RF-63), sin datos inventados (RNF-15). La IA generativa vive en `TheHouse-GenAI`, un servicio aparte que solo redacta a partir de hechos que el backend ya calculó y nunca modifica el puntaje. Ese servicio todavía no se despliega ni tiene proveedor elegido, y COST-001 exige demostrar costo $0; por eso el backend debe funcionar completo con explicaciones por plantilla y usar GenAI solo cuando esté disponible.

## 2. Objetivo

Explicar en lenguaje sencillo el puntaje de un cuarto con ventajas y desventajas verificables y un resumen redactado por TheHouse-GenAI cuando esté disponible, o por plantilla cuando no.

## 3. Precondiciones

1. Constitution 1.2.0 aprobada (M-BE-062).
2. NV-23 desplegada.
3. Validación del contrato §6.1 con el responsable de Mobile.
4. Autorización «EJECUTA».
5. Para activar GenAI en Production (T08): `TheHouse-GenAI` desplegado con sus specs 003, 005 y 010, y decisiones M-BE de su hosting y de su proveedor con la evidencia de §17; sin ellas GenAI queda desactivado y NV-24 funciona con plantillas.

## 4. Alcance

1. Hechos estructurados del cuarto y del puntaje como única fuente (RF-62).
2. Ventajas y desventajas deterministas desde el desglose de NV-23.
3. Resumen por plantilla en español de México (siempre disponible).
4. Cliente de `TheHouse-GenAI` (`explain-match`) con credencial de servicio, timeout, reintento y circuit breaker.
5. Validación de la respuesta de GenAI y degradación segura a plantilla.
6. Caché de explicaciones.
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Llamar directamente a un proveedor de modelos de lenguaje desde el backend.
- Implementar o desplegar `TheHouse-GenAI` (su propio repositorio).
- Chat conversacional libre con la IA.
- Búsqueda en lenguaje natural (`parse-search` de GenAI): spec futura.
- Enviar datos personales o textos libres del arrendador a GenAI.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/smart-match/explanations/{roomId}` | Autenticado | Explicación con las preferencias guardadas | 200, 400, 401, 404, 422, 429, 503 |
| POST | `/api/v1/smart-match/explanations/query` | Público | Explicación para invitados con preferencias en el cuerpo | 200, 400, 404, 429, 503 |

- Respuesta: `{ roomId, score, summary, pros: [..], cons: [..], generatedBy: "genai" | "template", modelVersion, explanationVersion }`. `summary` máximo 600 caracteres; `pros` y `cons` de 0 a 4 frases cortas; sin porcentajes por factor (RF-63).
- `generatedBy: "template"` cuando GenAI está desactivado, falla, tarda más de 3 s, tiene el circuit breaker abierto o su texto no pasa la validación; el puntaje y las ventajas y desventajas son los mismos en ambos casos.
- `explanations/query`: `{ roomId, universityId, preferences }` con el formato de NV-11 (no se guarda). Un cuarto no publicado o no elegible responde 404.
- Contrato con `TheHouse-GenAI` (versión `v1`, su spec 003): el backend envía `match_score`, `property { id, title, price, currency, distance_km, services }` (servicios con su nombre en español), `university { name }` y `preferences { budget_max, required_services }`; recibe `explanation` (máximo 120 palabras) y `facts_used`. Nunca envía dirección, coordenadas, dueño, descripción libre ni datos del usuario.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/smart-match/explanations/{roomId}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      roomId en la ruta
RESPONSE     200 { roomId, score, summary, pros: [..], cons: [..], generatedBy: "genai" | "template", modelVersion, explanationVersion }
STATUS CODES 200, 400, 401, 404 cuarto no publicado o no elegible, 422 perfil incompleto, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/smart-match/explanations/query
METHOD       POST
AUTH         Público (sin token); rate limit 10/min por IP
HEADERS      —
REQUEST      { roomId, universityId, preferences }
RESPONSE     200 { roomId, score, summary, pros: [..], cons: [..], generatedBy: "genai" | "template", modelVersion, explanationVersion }
STATUS CODES 200, 400, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **SmartMatchExplanations** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `smart_match_explanations (caché)` |
| Usa (puertos de otros módulos) | SmartMatch `ICompatibilityModel` y el cálculo de NV-23 para el puntaje y el desglose; Search `SearchService` (NV-14) para los datos públicos del cuarto; TheHouse-GenAI (servicio auxiliar fuera del monolito, constitution §6.1) con el contrato `explain-match` v1 |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- Hechos de entrada: puntaje y desglose de NV-23, precio, presupuesto, distancia aproximada, minutos a pie, servicios del cuarto y esenciales del usuario, tipo de cuarto y de edificio, nombre de la universidad. Nunca la descripción libre del arrendador ni datos personales.
- `smart_match_explanations` (caché): clave `{ roomId, preferencesHash, modelVersion, explanationVersion }`, TTL de 24 h; guarda solo `summary`, `pros`, `cons` y `generatedBy`.
- Puerto `IExplanationWriter` con dos adaptadores: `TemplateExplanationWriter` (siempre disponible) y `GenAiExplanationClient` (HTTP a `TheHouse-GenAI` con credencial de servicio).
- Configuración `GenAi`: `Enabled` (falso por defecto), `BaseUrl`, credencial como secreto de Container Apps (constitution §31), `TimeoutSeconds` 3.

## 9. Reglas de negocio

1. Ventajas y desventajas salen del desglose de NV-23: un factor con al menos el 80 % de su máximo es ventaja, uno con menos del 40 % es desventaja; frases de un catálogo versionado.
2. El resumen por plantilla combina los dos factores más altos y el más bajo con frases fijas; es determinista.
3. GenAI: un intento más un reintento con 3 s de timeout en total por explicación; circuit breaker que abre tras 5 fallas seguidas durante 60 s; respuesta mayor a 120 palabras o 600 caracteres se rechaza.
4. Validación de la respuesta de GenAI: todo número, precio, distancia, servicio o nombre de universidad que aparezca debe estar en los hechos enviados, y `facts_used` solo puede nombrar campos enviados; si no, se usa la plantilla y se cuenta el rechazo (sin registrar el texto).
5. El texto se devuelve como texto plano: sin HTML, Markdown ni enlaces.
6. Cambiar las preferencias cambia `preferencesHash` y por tanto la explicación.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Solo el backend llama a `TheHouse-GenAI`, con credencial de servicio; nunca reenvía el JWT del usuario (constitution §6.1).
- Prompt injection: el contexto se arma en el servidor con campos estructurados; los textos libres del arrendador no se envían.
- Sin datos personales hacia GenAI (RNF-11) y sin prompts ni explicaciones completas en logs.
- Rate limit 20/min por usuario y 10/min por IP de invitado; caché para no repetir llamadas.
- Si GenAI no está disponible el endpoint sigue respondiendo 200 con plantilla (RNF-16); 503 solo si fallan MongoDB o Redis.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Perfil sin universidad o sin presupuesto | 422 | Solicitud no procesable (`code=smart_match_profile_incomplete`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-24-R01 | Hechos estructurados | Los hechos se arman solo desde NV-23 y los datos públicos del cuarto; prueba de que no incluyen descripción libre, dirección, coordenadas ni datos del usuario. | F1 | T01 |
| NV-24-R02 | Ventajas, desventajas y plantilla | Ventajas y desventajas por umbral del desglose y resumen por plantilla en español de México, deterministas, sin porcentajes por factor y con máximo de 600 caracteres; casos de prueba fijos. | F1 | T02 |
| NV-24-R03 | Endpoints de explicación | `explanations/{roomId}` con sesión y `explanations/query` para invitados; 404 cuarto no publicado o no elegible; 422 perfil incompleto. | F1 | T03 |
| NV-24-R04 | Cliente de TheHouse-GenAI | `GenAiExplanationClient` con el contrato `v1` de la §6, credencial de servicio, 3 s de timeout, un reintento y circuit breaker; probado con un doble de GenAI, sin red. | F2 | T04 |
| NV-24-R05 | Validación y degradación segura | Respuestas con números, servicios o universidades ausentes, demasiado largas o con formato inválido usan la plantilla; con GenAI caído, lento, desactivado o con el circuito abierto responde 200 con `generatedBy=template`. | F2 | T05 |
| NV-24-R06 | Caché y límites | Caché de 24 h por cuarto, preferencias y versiones; rate limits con 429; Redis caído 503. | F2 | T06 |
| NV-24-R07 | Protección contra inyección y minimización | Pruebas con descripciones maliciosas del arrendador que no llegan a GenAI ni alteran la salida; ningún dato personal en la solicitud a GenAI ni en logs. | F3 | T07 |
| NV-24-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD con GenAI desactivado; prueba HTTP real en Azure con plantilla; activar GenAI en Production solo con sus decisiones M-BE aprobadas y una prueba real adicional. | F4 | T08 |
| NV-24-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Generador | Plantillas siempre; `TheHouse-GenAI` solo para el resumen, desactivado por defecto | PROPUESTO |
| Contrato con GenAI | `explain-match` versión `v1` según su spec 003; cambios con nueva versión en ambos repositorios | PROPUESTO |
| Credencial de servicio | Clave compartida en un encabezado, guardada como secreto de Container Apps en el backend y en GenAI; rotación manual documentada | PROPUESTO |
| Hosting de TheHouse-GenAI | Azure Container Apps en el mismo entorno, escala a cero, ingreso interno; requiere decisión M-BE con evidencia de §17 | PENDIENTE |
| Proveedor de IA de TheHouse-GenAI | Plan sin método de pago o límite de gasto en 0, con cuotas documentadas; requiere decisión M-BE con evidencia de §17 | PENDIENTE |
| Idioma | Español neutro de México | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Plantillas en el backend | $0 | Siempre disponibles |
| `TheHouse-GenAI` (hosting y proveedor) | $0 solo con evidencia de §17 | PENDIENTE; mientras tanto desactivado y sin costo |
| Caché en MongoDB Atlas M0 | $0 | TTL de 24 h |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-24-RK01 | Alucinaciones | Información falsa al usuario | Ventajas y desventajas deterministas, hechos estructurados y validación del resumen |
| NV-24-RK02 | Prompt injection | Mensajes manipulados | No se envían textos libres; validación de la salida |
| NV-24-RK03 | Costo del proveedor de IA | Incumplir COST-001 | GenAI desactivado por defecto y activación solo con decisión M-BE y evidencia |
| NV-24-RK04 | GenAI caído o lento | Explicaciones más lentas | Timeout de 3 s, circuit breaker y plantilla |
| NV-24-RK05 | Contratos desalineados entre repositorios | Errores al integrar | Versión `v1` documentada en ambas specs y doble de prueba con fixtures de GenAI |

## 16. Dependencias

- Constitution 1.2.0.
- NV-23.
- `TheHouse-GenAI` specs 003, 005 y 010 para activar GenAI.

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
