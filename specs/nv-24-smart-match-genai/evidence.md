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

## 5. Despliegue y prueba real en Azure (T08 parcial) — 2026-11-24

| Paso | Resultado |
| --- | --- |
| PRs | [#83](https://github.com/thehouse-team/TheHouse-Backend/pull/83) (1.2.1 e implementación), [#84](https://github.com/thehouse-team/TheHouse-Backend/pull/84) (registro de `generated=false` con el id) y [#85](https://github.com/thehouse-team/TheHouse-Backend/pull/85) (caché por generador), mezclados en `main` |
| CD | #79 (`ee57b45`), #80 (`2b3c934`) y #81 (`2b2a7f6`): publish, verify-ephemeral, deploy y dast-baseline en verde; despliegues aprobados por un integrante distinto de quien hizo el merge |

### Prueba con plantilla (GenAI desactivado): **13/13 PASS**

`.azure-check/nv24_azure_check.py run` (fuera de git), sobre los cuartos de demostración:

| Bloque | Resultado |
| --- | --- |
| Invitado (E1–E10) | Contrato de la §6.1 con `no-store`; `generatedBy: template`, `explanation-1.0.0` y `baseline-1.0.0`; puntaje igual al de NV-23 (95); un solo «%» en el resumen (sin porcentajes por factor); 4 ventajas y 1 desventaja; 422 con `missing`; 404 para un cuarto no publicado y para uno no elegible; 400 por campo extra; 401 sin sesión |
| Con cuenta (U1–U3) | 422 con el perfil vacío (4 campos); explicación con las preferencias guardadas (405 ms); segunda consulta igual desde la caché (391 ms) |

### Activación de TheHouse-GenAI

| Paso | Resultado |
| --- | --- |
| Clave de servicio | Victor la cargó como secreto `genai-service-key` con `Read-Host -AsSecureString` (sin pantalla ni historial); el backend no guarda la clave de métricas de GenAI |
| Configuración | `GenAI__Enabled=true`, `GenAI__BaseUrl` = URL interna de `ca-thehouse-genai-u2` y `GenAI__ServiceKey=secretref:genai-service-key`; los CD posteriores la conservan (solo cambian la imagen) |
| Arranque | «NV-24: TheHouse-GenAI activo para las explicaciones» en cada revisión nueva |

### Diagnóstico de la prueba con GenAI (`generatedBy: template` en vez de `genai`)

| Hallazgo | Evidencia | Acción |
| --- | --- | --- |
| **Caché de plantillas viejas (backend)** | La primera prueba corrió segundos antes de que la revisión con GenAI recibiera tráfico; esas plantillas se guardaron y se sirvieron 24 h sin llamar a GenAI (respuestas de ~300 ms sin ningún registro) | [#85](https://github.com/thehouse-team/TheHouse-Backend/pull/85): el generador activo forma parte de la llave de caché |
| **Sin registro de `generated: false` (backend)** | GenAI podía devolver su plantilla sin dejar rastro en el backend | [#84](https://github.com/thehouse-team/TheHouse-Backend/pull/84): registro con `promptVersion` y `X-Request-ID`, sin el texto ni la clave |
| **Arranque en frío de GenAI** | GenAI estaba en cero réplicas; la primera llamada (17:15:02 UTC) creó su réplica a las 17:15:03, pero tardó más de 8 s: dos llamadas registraron «no respondió en 8 s» (requests `0ceb540e…` y `2e995b32…`) | Comportamiento previsto (RNF-16): plantilla sin guardar en caché; la siguiente consulta reintenta |
| **GenAI responde `generated: false` (equipo de GenAI)** | Con GenAI ya encendido, respondió en 0.9 s con `generated=false` (`explain_match_v3`; request `d24974e7c5094dd3812c3bf9bf34f0c4`, 17:15:52 UTC). Por la rapidez, no llamó a Gemini | Enviado al equipo de GenAI con el id para revisar sus registros (proveedor, presupuesto, moderación o validación) |

La conexión backend → GenAI funciona por la URL interna y solo HTTPS, con la clave correcta (sin 401) y el contrato correcto (sin 422). Lo que falta para cerrar T08 es que GenAI responda `generated: true` y repetir `.azure-check/nv24_azure_check.py genai`.

## 6. Estado

**9 de 11 — desplegada con plantilla.** NV-24 ya cumple RNF-16 en producción: explica el puntaje de NV-23 con plantilla y usará el texto de GenAI en cuanto responda `generated: true`, sin cambiar el contrato con Mobile. Pendiente: T08 (prueba con `generatedBy: genai` y contrato a Mobile) y T11 (cierre de la evidencia).
