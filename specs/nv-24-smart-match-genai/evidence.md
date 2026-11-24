# NV-24 — Evidencia: explicaciones de Smart Match con TheHouse-GenAI

| Campo | Valor |
| --- | --- |
| Spec | `spec.md` 1.2.1 (M-BE-086) |
| Autorización | «Apruebo NV-24 1.2.1 y EJECUTA» — Victor Manuel Jose Jose, 2026-11-24 (T01–T11) |
| Rama | `feat/nv-24-genai-explanations` |
| Resultado parcial | **9 de 11** — T01–T07, T09 y T10 completas en local; T08 (despliegue, clave y prueba real con GenAI) y T11 esperan el despliegue |

## 1. Qué se implementó

| Pieza | Archivo |
| --- | --- |
| Reglas: ventajas y desventajas por umbral (≥ 80 % y < 40 % del máximo de cada factor, máximo 4), plantilla determinista sin porcentajes por factor, título neutro, mapeo de servicios, llave de caché y validación del texto de GenAI | `TheHouse.Application/Common/SmartMatchExplanationRules.cs` |
| Puntaje y desglose de un cuarto con el mismo cálculo de NV-23 (400, 422 y 404 sin revelar si el cuarto no está publicado o no es elegible) | `SmartMatchService.ExplanationContextAsync` |
| Caso de uso: caché → plantilla → GenAI si está activo → validación → plantilla si falla | `TheHouse.Application/UseCases/SmartMatchExplanationService.cs` |
| Cliente de `explain-match` (contrato congelado): `X-Service-Key`, `X-Request-ID`, 8 s, un reintento solo en red/5xx, 429 con `Retry-After`, circuit breaker (5 fallas → 60 s) y 401/422 registrados sin el cuerpo | `TheHouse.Infrastructure/GenAi/GenAiExplanationClient.cs` |
| Caché `smart_match_explanations` (TTL 24 h; llave SHA-256 de cuarto, preferencias y versiones) | `MongoSmartMatchExplanationCache.cs` |
| Rutas `GET /smart-match/explanations/{roomId}` (20/min por usuario) y `POST /smart-match/explanations/query` (10/min por IP, sesión opcional) | `TheHouse/Http/SmartMatchEndpoint.cs` |
| Configuración `GenAI` (desactivada por defecto; solo URL https y clave como secreto) y aviso al iniciar | `TheHouse/Configuration/SmartMatchConfiguration.cs`, `appsettings.json` |

Sin paquetes nuevos ni secretos en el repositorio. Script de la base de datos regenerado (35 colecciones, 72 índices).

## 2. Decisiones de implementación

- **Qué viaja a GenAI:** `matchScore`, código de la universidad, título neutro (por ejemplo «Cuarto con baño privado en residencia estudiantil cerca de UTTT»), precio, distancia aproximada con 1 decimal, servicios mapeados al español (más «baño privado» si el baño es propio), presupuesto máximo, distancia máxima y servicios esenciales. Nunca id del cuarto, nombre del edificio, reglas, dirección, coordenadas ni datos del estudiante.
- **Validación del texto de GenAI:** texto plano de 1 a 600 caracteres; sin HTML, Markdown ni enlaces; cada número debe ser uno de los hechos enviados (acepta «2,800», «2800» y «1,3»); cada servicio mencionado (con sinónimos como «wifi») debe estar entre los enviados; no puede nombrar otra universidad del catálogo. Si falla, plantilla y se registra solo el motivo.
- **Caché:** guarda las explicaciones de GenAI y las de plantilla cuando GenAI está desactivado; una plantilla por falla de GenAI no se guarda, para reintentar en la siguiente consulta.
- **Desempate de la plantilla:** el factor más bajo se elige por porcentaje y, en empate, por el orden de los factores de ML-04.

## 3. Pruebas

| Proyecto | Pruebas nuevas | Qué cubren |
| --- | --- | --- |
| Unitarias | `SmartMatchExplanationTests` (19) | Umbrales y máximo de 4; plantilla exacta, determinista y sin porcentajes por factor; título neutro; mapeo de los 11 servicios; llave de caché; textos de GenAI válidos e inválidos (número, servicio, universidad, HTML, enlace, Markdown, vacío, largo); hechos sin ids ni textos libres |
| Integración | `GenAiClientTests` (14) | Cuerpo exacto del contrato sin `roomId`; encabezados; `generated: false`; reintento único en 5xx y error de red; sin reintento en 401, 413, 422 y timeout; 429 abre el circuito durante `Retry-After`; 5 fallas lo abren 60 s; GenAI solo con https y clave |
| Integración | `SmartMatchExplanationHttpTests` (10) | Forma del contrato y puntaje igual a NV-23; GenAI válido usado y en caché; GenAI rechazado, sin generar, caído o con el circuito abierto → plantilla sin caché; invitado; 422, 400, 404 (no publicado o no elegible) y 401; inyección en el nombre y las reglas del edificio que no llega a GenAI; 429 por usuario e IP; 503 con MongoDB o Redis caídos |
| Seguridad | `SmartMatchSecurityTests` (+3) | Contrato estricto de la consulta; respuesta sin puntos por factor ni internos; la clave de GenAI no está en `appsettings` y GenAI viene desactivado |

Suite completa en Release (`dotnet build -warnaserror`: 0 advertencias, 0 errores): unitarias 459/459, integración 543/543, seguridad 169/169 — **1,171/1,171**. En la corrida completa falló una vez `HealthAndRateLimitTests.Slow_probe_has_bounded_readiness_and_does_not_affect_liveness` (prueba ya conocida como inestable bajo carga de la máquina: cliente de 5 s); corrida sola pasó en 2 s.

## 4. Pendiente (T08 y T11)

1. Merge y CD (GenAI desactivado): prueba HTTP real en Azure con plantilla.
2. Victor carga la clave como secreto `genai-service-key` y activa `GenAI__Enabled=true` y `GenAI__BaseUrl` (URL interna); prueba real con `generatedBy=genai` de punta a punta con el equipo de GenAI.
3. Contrato §6.1 a Mobile y cierre de la evidencia.
