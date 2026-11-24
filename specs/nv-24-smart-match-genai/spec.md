# NV-24 — Smart Match — explicaciones con TheHouse-GenAI

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `spec.md` |
| Versión | 1.2.1 |
| Fecha | 2026-11-24 |
| Estado documental | **✅ APROBADA — 1.2.1** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-24 («Apruebo NV-24 1.2.1 y EJECUTA»: hosting en Container Apps interno, costo $0 con la evidencia del equipo de GenAI del 2026-11-22 y privacidad del nivel gratuito; «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas, `scripts/db/` (regenerado por la prueba de NV-26) y `specs/nv-24-smart-match-genai/`; la clave de servicio la carga Victor como secreto; sin paquetes nuevos) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.1: T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-59, RF-60, RF-61, RF-62, RF-63 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-15, RNF-16 |
| Plan | `plan.md` 1.2.1 |
| Tasks | `task.md` 1.2.1 |
| Módulo | SmartMatchExplanations |
| Precondición | Constitution 1.2.0 (M-BE-062) y NV-23 |
| Relación | Explica los puntajes de NV-23 1.2.0; consume `TheHouse-GenAI` (`explain-match`, contrato congelado del 2026-11-18) por su URL interna; si no está disponible, responde con plantilla (RNF-16) |

Spec aprobada (1.2.1, M-BE-086) y autorizada («EJECUTA», T01–T11) en las rutas indicadas.

**Cambio 1.2.1 (2026-11-24):** actualización de hechos del equipo de GenAI (mensaje y guía del 2026-11-22) y de NV-23 1.2.0, sin cambiar el contrato con GenAI. (1) **Hosting:** `TheHouse-GenAI` corre en Azure Container Apps (`ca-thehouse-genai-u2`) dentro del mismo entorno `cae-thehouse-u2`, con ingreso **interno** y solo HTTPS (`https://ca-thehouse-genai-u2.internal.grayisland-cb722b5f.eastus2.azurecontainerapps.io`), 0–1 réplicas (se apaga tras 5 min sin uso); ya no App Service. (2) **Costo $0 con evidencia:** proyecto de Gemini `thehouse-genai-prod` en nivel gratuito sin facturación configurada (15 RPM, ~250 K TPM, 500 RPD; al agotarse GenAI responde plantilla) y Container Apps dentro de la concesión gratuita. (3) **Privacidad:** en el nivel gratuito Google puede usar lo enviado para mejorar sus productos; NV-24 solo envía hechos públicos del cuarto y las preferencias de búsqueda, con un título neutro armado por el backend (nunca el nombre ni textos del arrendador). (4) Con NV-23 1.2.0 el puntaje y su desglose usan los factores de ML-04 y el perfil mínimo; `explanations/query` responde 422 con `missing` igual que NV-23. El contrato de la §6.1 con Mobile no cambia salvo ese 422. La primera llamada tras el apagado de GenAI puede pasar de 8 s: se responde con plantilla, como en cualquier timeout.

## 1. Contexto

El estudiante debe entender por qué un cuarto tiene su porcentaje: qué coincide y qué no con lo que busca (RF-59 a RF-63), sin datos inventados (RNF-15). La IA generativa vive en `TheHouse-GenAI`, un servicio aparte que solo redacta a partir de hechos que el backend ya calculó y nunca modifica el puntaje. Ese servicio todavía no se despliega ni tiene proveedor elegido, y COST-001 exige demostrar costo $0; por eso el backend debe funcionar completo con explicaciones por plantilla y usar GenAI solo cuando esté disponible.

## 2. Objetivo

Explicar en lenguaje sencillo el puntaje de un cuarto con ventajas y desventajas verificables y un resumen redactado por TheHouse-GenAI cuando esté disponible, o por plantilla cuando no.

## 3. Precondiciones

1. Constitution 1.2.0 aprobada (M-BE-062).
2. NV-23 1.2.0 desplegada (2026-11-24).
3. El contrato §6.1 con Mobile no cambia respecto a 1.1.0 salvo el 422 de `explanations/query`, igual que NV-23 1.2.0 (aceptado por Mobile).
4. «Apruebo NV-24 1.2.1 y EJECUTA».
5. Para activar GenAI en Production (T08): la clave de servicio entregada por canal privado y cargada por Victor como secreto; URL interna y evidencia de costo ya disponibles (2026-11-22).

## 4. Alcance

1. Hechos estructurados del cuarto y del puntaje como única fuente (RF-62).
2. Ventajas y desventajas deterministas desde el desglose de NV-23.
3. Resumen por plantilla en español de México (siempre disponible).
4. Cliente de `TheHouse-GenAI` (`POST /api/v1/generative/explain-match`) con el contrato congelado, clave de servicio, timeout de 8 s, un reintento en red/5xx y circuit breaker.
5. Mapeo de los servicios del catálogo del backend a sus nombres en español para GenAI.
6. Validación de la respuesta de GenAI y degradación segura a plantilla.
7. Caché de explicaciones.
8. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Llamar directamente a un proveedor de modelos de lenguaje desde el backend.
- Implementar o desplegar `TheHouse-GenAI` (su propio repositorio).
- Chat conversacional libre con la IA.
- Búsqueda en lenguaje natural (`parse-search` de GenAI): spec futura; las respuestas a sus preguntas quedan en `genai-contract.md`.
- Enviar datos personales, el id del cuarto, la dirección o textos libres del arrendador a GenAI.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/smart-match/explanations/{roomId}` | Autenticado | Explicación con las preferencias guardadas | 200, 400, 401, 404, 422, 429, 503 |
| POST | `/api/v1/smart-match/explanations/query` | Público | Explicación para invitados con preferencias en el cuerpo | 200, 400, 404, 429, 503 |

- Respuesta: `{ roomId, score, summary, pros: [..], cons: [..], generatedBy: "genai" | "template", modelVersion, explanationVersion }`. `summary` máximo 600 caracteres; `pros` y `cons` de 0 a 4 frases cortas; sin porcentajes por factor (RF-63).
- `generatedBy: "genai"` solo si GenAI respondió 200 con `generated: true` y su texto pasa la validación; en cualquier otro caso (desactivado, sin URL o clave, timeout, 4xx, 5xx, 429, circuito abierto, `generated: false` o texto rechazado) `generatedBy: "template"`. El puntaje y las ventajas y desventajas son los mismos en ambos casos.
- `explanations/query`: `{ roomId, universityId, preferences }` con el formato de NV-11 (no se guarda). Un cuarto no publicado o no elegible responde 404; un perfil sin universidad, `budget.max`, `livingWith` o `placeType` responde 422 `smart_match_profile_incomplete` con `missing` (igual que NV-23).
- Contrato con `TheHouse-GenAI` (congelado el 2026-11-18): `POST {GenAI__BaseUrl}/api/v1/generative/explain-match` con `X-Service-Key`, `Content-Type: application/json` y `X-Request-ID` (id de correlación del backend). Cuerpo en camelCase, sin campos extra (si no, 422): `{ matchScore (0–100), locale: "es-MX", university (≤120), property: { title (≤120), price (≥0, 2 decimales), currency: "MXN", distanceKm (≥0), services: [≤20, cada uno ≤40] }, preferences: { budgetMax (≥0), currency (igual a la del cuarto), maxDistanceKm (≥0), essentialServices: [..] } }`. Respuesta 200: `{ explanation, generated, promptVersion }`. Errores con el sobre `{ error: { code, message, request_id } }`: 401, 413, 422, 429 (`Retry-After`) y 500. Nunca se envían id del cuarto, dirección, coordenadas, dueño, descripción libre, datos del estudiante ni tokens.

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
STATUS CODES 200, 400, 404, 422 code=smart_match_profile_incomplete (missing), 429, 503
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
| Usa (puertos de otros módulos) | SmartMatch `SmartMatchService` (NV-23 1.2.0) para el perfil, el cuarto, el puntaje y el desglose; Search `SearchService` (NV-14) para los datos públicos del cuarto; TheHouse-GenAI (servicio auxiliar fuera del monolito, constitution §6.1) con el contrato `explain-match` congelado del 2026-11-18, por su URL interna |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- Hechos de entrada: puntaje y desglose de NV-23, precio, presupuesto, distancia aproximada, minutos a pie, servicios del cuarto y esenciales del usuario, tipo de cuarto y de edificio, nombre de la universidad. Nunca la descripción libre del arrendador ni datos personales.
- `smart_match_explanations` (caché): clave `{ roomId, preferencesHash, modelVersion, explanationVersion }`, TTL de 24 h; guarda solo `summary`, `pros`, `cons` y `generatedBy`.
- Puerto `IExplanationWriter` con dos adaptadores: `TemplateExplanationWriter` (siempre disponible) y `GenAiExplanationClient` (HTTP a `TheHouse-GenAI` con clave de servicio).
- Configuración `GenAI`: `Enabled` (falso por defecto), `BaseUrl` (https; en Production `https://ca-thehouse-genai-u2.internal.grayisland-cb722b5f.eastus2.azurecontainerapps.io`), `TimeoutSeconds` 8; la clave como secreto de Container Apps `genai-service-key` expuesto como `GenAI__ServiceKey` (constitution §31; nunca en el repositorio ni en `appsettings.json`). El backend no usa la clave de métricas de GenAI.
- Mapeo de servicios para GenAI: `wifi`→«internet», `water`→«agua», `electricity`→«luz», `gas`→«gas», `parking`→«estacionamiento», `laundry`→«lavadora», `furnished`→«amueblado», `hot_water`→«agua caliente», `kitchen`→«cocina», `security`→«seguridad», `cleaning`→«limpieza»; baño propio del cuarto→«baño privado».

## 9. Reglas de negocio

1. Ventajas y desventajas salen del desglose de NV-23: un factor con al menos el 80 % de su máximo es ventaja, uno con menos del 40 % es desventaja; frases de un catálogo versionado.
2. El resumen por plantilla combina los dos factores más altos y el más bajo con frases fijas; es determinista.
3. GenAI: un intento con 8 s de timeout (GenAI corta su llamada a la IA a los 6 s y responde con plantilla); un reintento solo ante error de red o 5xx, nunca ante timeout ni 4xx; 429 abre el circuito durante `Retry-After`; circuit breaker que abre tras 5 fallas seguidas durante 60 s; 401 y 422 se registran como error de configuración o de código (sin el cuerpo).
4. Validación de la respuesta de GenAI (defensa adicional a la suya): texto plano de máximo 600 caracteres; todo número, precio, distancia, servicio o nombre de universidad que aparezca debe estar en los hechos enviados; si no, se usa la plantilla y se cuenta el rechazo (sin registrar el texto).
5. El texto se devuelve como texto plano: sin HTML, Markdown ni enlaces.
6. Cambiar las preferencias cambia `preferencesHash` y por tanto la explicación. La caché de 24 h guarda las explicaciones de GenAI y las de plantilla cuando GenAI está desactivado; una plantilla por falla de GenAI no se guarda, para reintentar en la siguiente consulta (el circuit breaker evita insistir).
7. Título enviado a GenAI: neutro y armado por el backend desde catálogos (tipo de cuarto y de inmueble y el código de la universidad); nunca el nombre del edificio ni otro texto del arrendador.

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
- Nivel gratuito de Gemini: Google puede usar lo enviado; por eso solo viajan hechos públicos del cuarto (precio, distancia aproximada, servicios y tipos) y las preferencias de búsqueda, sin ids, nombres ni textos libres.
- La URL de GenAI es interna del entorno `cae-thehouse-u2` y solo HTTPS; la clave de métricas de GenAI no se guarda en el backend.

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
| NV-24-R04 | Cliente de TheHouse-GenAI | `GenAiExplanationClient` con el contrato congelado (camelCase, sin campos extra, sin id del cuarto), `X-Service-Key`, `X-Request-ID`, 8 s de timeout, reintento solo en red/5xx, 429 con `Retry-After` y circuit breaker; mapeo de servicios; probado con un doble de GenAI que reproduce sus respuestas y errores, sin red. | F2 | T04 |
| NV-24-R05 | Validación y degradación segura | `generated: false`, respuestas con números, servicios o universidades ausentes, demasiado largas o con formato inválido usan la plantilla; con GenAI caído, lento, desactivado, sin clave o con el circuito abierto responde 200 con `generatedBy=template`. | F2 | T05 |
| NV-24-R06 | Caché y límites | Caché de 24 h por cuarto, preferencias y versiones; rate limits con 429; Redis caído 503. | F2 | T06 |
| NV-24-R07 | Protección contra inyección y minimización | Pruebas con descripciones maliciosas del arrendador que no llegan a GenAI ni alteran la salida; ningún dato personal en la solicitud a GenAI ni en logs. | F3 | T07 |
| NV-24-R08 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD con GenAI desactivado; prueba HTTP real en Azure con plantilla; activar GenAI en Production con la URL, la clave como secreto y la evidencia de costo, y una prueba real adicional con `generatedBy=genai`. | F4 | T08 |
| NV-24-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Generador | Plantillas siempre; `TheHouse-GenAI` solo para el resumen, desactivado por defecto | APROBADO (M-BE-068) |
| Contrato con GenAI | `explain-match` congelado por el equipo de GenAI el 2026-11-18 (camelCase, `generated`, `promptVersion`, sin id del cuarto); cambios con nueva versión en ambos repositorios | APROBADO (M-BE-081) |
| Credencial de servicio | Encabezado `X-Service-Key`; secreto `genai-service-key` en la Container App del backend como `GenAI__ServiceKey`; entrega por canal privado y rotación manual | APROBADO (M-BE-081) |
| Timeout | 8 s por llamada (GenAI responde plantilla a los 6 s); reintento solo en red/5xx; la caché de 24 h evita la espera en las siguientes consultas | APROBADO (M-BE-081) |
| Hosting de TheHouse-GenAI | Azure Container Apps `ca-thehouse-genai-u2` en `cae-thehouse-u2`, ingreso interno solo HTTPS (`https://ca-thehouse-genai-u2.internal.grayisland-cb722b5f.eastus2.azurecontainerapps.io`), 0–1 réplicas; operado por el equipo de GenAI | APROBADO (M-BE-086) |
| Proveedor de IA de TheHouse-GenAI | Gemini 3.5 Flash Lite en el proyecto `thehouse-genai-prod`, nivel gratuito sin facturación (evidencia del 2026-11-22) | APROBADO (M-BE-086) |
| Idioma | Español de México (`locale: es-MX`) | APROBADO (M-BE-068) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Plantillas en el backend | $0 | Siempre disponibles |
| `TheHouse-GenAI` (Container Apps) | $0 | Escala a cero (0–1 réplicas, 0.25 vCPU / 0.5 GiB) dentro de la concesión gratuita del entorno compartido |
| Gemini (`thehouse-genai-prod`) | $0 | Nivel gratuito sin facturación configurada; capturas del 2026-11-22; al agotar la cuota responde plantilla |
| Caché en MongoDB Atlas M0 | $0 | TTL de 24 h |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-24-RK01 | Alucinaciones | Información falsa al usuario | Ventajas y desventajas deterministas, hechos estructurados y validación del resumen |
| NV-24-RK02 | Prompt injection | Mensajes manipulados | No se envían textos libres; validación de la salida |
| NV-24-RK03 | Costo del proveedor de IA | Incumplir COST-001 | GenAI desactivado por defecto y activación solo con decisión M-BE y evidencia |
| NV-24-RK04 | GenAI caído o lento | Explicación más lenta (hasta 8 s la primera vez) | Caché de 24 h, circuit breaker, 429 respetado y plantilla |
| NV-24-RK05 | Contratos desalineados entre repositorios | Errores al integrar (422) | Contrato congelado copiado en `genai-contract.md` y doble de prueba con sus fixtures |
| NV-24-RK06 | Filtración de la clave de servicio | Uso indebido de GenAI | Solo como secreto de Container Apps, entrega por canal privado, rotación documentada |
| NV-24-RK07 | Uso de datos por el nivel gratuito de Gemini | Contenido enviado usado por Google | Solo hechos públicos y preferencias de búsqueda, título neutro, sin ids ni textos libres; prueba de minimización |
| NV-24-RK08 | Arranque en frío de GenAI (se apaga tras 5 min) | Primera explicación sin IA | Plantilla en el timeout de 8 s y caché solo de respuestas válidas |

## 16. Dependencias

- Constitution 1.2.0.
- NV-23 1.2.0 (desplegada el 2026-11-24).
- `TheHouse-GenAI` desplegado en `cae-thehouse-u2` con su contrato congelado del 2026-11-18.

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA — 1.2.1** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.1: T01–T11 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
