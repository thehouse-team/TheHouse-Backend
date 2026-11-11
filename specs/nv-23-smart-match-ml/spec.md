# NV-23 — Smart Match — compatibilidad con el modelo de TheHouse-ML

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-23-smart-match-ml/`; el paquete `Microsoft.ML.OnnxRuntime` de la T06 requiere una autorización que lo nombre, y la T06 y la T07 esperan el release de `TheHouse-ML`) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-48 a RF-58, RF-64, RF-65, RF-68 (uso sin cuenta) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-13, RNF-14 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | SmartMatch |
| Precondición | Constitution 1.2.0 (M-BE-062); NV-11 y NV-14 desplegadas |
| Relación | Consume el artefacto de `TheHouse-ML` (ML-04 casos dorados, ML-06 ONNX); NV-24 explica los resultados; NV-25 medirá su uso |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-10):** se adapta a la constitution 1.2.0 §6.1 (M-BE-062). El modelo ya no se entrena con ML.NET dentro de una herramienta del backend: lo produce `TheHouse-ML` (satélite de artefacto) como un ONNX versionado que el backend carga **en proceso** con `Microsoft.ML.OnnxRuntime`, verifica su SHA-256 y prueba su paridad con los casos dorados. Mientras no exista ese artefacto, o si no es válido, se usa el **modelo base por reglas** en C#, con los mismos pesos que ML-04 de `TheHouse-ML`. Trabaja sobre los cuartos publicados de NV-14 (`roomId`), traduce los catálogos de NV-11 a los de NV-12 como contrato de features, aplica restricciones duras (presupuesto y convivencia, igual que el filtro de NV-14) y responde el desglose por factor en puntos que suman el total. Rutas `/query` en lugar de `:query`. Eventos de dominio fuera de v1.

## 1. Contexto

Con NV-11 el estudiante guarda presupuesto, distancia, servicios, con quién vivir, tipo de lugar, mascotas, transporte y tipos de inmueble; con NV-14 existen cuartos publicados con precio, tipo, baño, servicios y distancia aproximada. Falta el porcentaje de compatibilidad que ordena los cuartos (RF-48 a RF-57). El profesor pide que el Machine Learning sea un proyecto separado: `TheHouse-ML` entrena y evalúa en Python y entrega un ONNX; el backend solo lo ejecuta. Como ese modelo tardará, el backend arranca con el modelo base por reglas, explicable y con los mismos pesos, para que la app tenga Smart Match desde ahora.

## 2. Objetivo

Calcular y exponer un puntaje de compatibilidad 0–100 con desglose por factor para cuartos publicados, con modelo base por reglas y, cuando exista, el modelo ONNX de TheHouse-ML, determinista, explicable y a costo $0.

## 3. Precondiciones

1. Constitution 1.2.0 aprobada (M-BE-062).
2. NV-11 y NV-14 desplegadas.
3. Validación del contrato §6.1 con el responsable de Mobile.
4. Autorización «EJECUTA» que nombre el paquete `Microsoft.ML.OnnxRuntime` para la T06.
5. Para la T06 y la T07: release del modelo publicado por `TheHouse-ML` (ML-06) y casos dorados (ML-04); sin ellos esas tasks quedan BLOCKED y el resto se completa con el modelo base.

## 4. Alcance

1. Perfil Smart Match desde las preferencias guardadas (NV-11) o enviadas por un invitado (RF-48, RF-68).
2. Traducción de catálogos NV-11 → NV-12 y restricciones duras.
3. Modelo base por reglas en C# con desglose por factor (RF-49 a RF-56).
4. `POST /smart-match/scores` para hasta 50 cuartos.
5. Recomendaciones ordenadas por compatibilidad, con y sin cuenta (RF-57, RF-68).
6. Retroalimentación útil/no útil y descartes (RF-64, RF-65).
7. Carga del modelo ONNX de `TheHouse-ML` con verificación de hash y esquema, y respaldo al modelo base.
8. Paridad con los casos dorados de `TheHouse-ML`.
9. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Entrenamiento, datos y evaluación del modelo: `TheHouse-ML` (ML-02 a ML-07).
- Explicación en lenguaje natural: NV-24.
- Métricas de uso: NV-25.
- Exportación anonimizada de señales para ML-07: spec futura.
- Uso de datos personales (nombre, correo, género, edad) como variables (RNF-11).
- Eventos de dominio (`RecommendationShown`, etc.): fuera de v1.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/smart-match/scores` | Público o autenticado | Puntajes para `roomIds` (1–50) | 200, 400, 401, 422, 429, 503 |
| GET | `/api/v1/smart-match/recommendations` | Autenticado | Cuartos ordenados por compatibilidad con las preferencias guardadas | 200, 400, 401, 422, 429, 503 |
| POST | `/api/v1/smart-match/recommendations/query` | Público | Lo mismo para invitados con preferencias en el cuerpo | 200, 400, 404, 429, 503 |
| POST | `/api/v1/smart-match/feedback` | Autenticado | `{ roomId, useful }` | 204, 400, 401, 404, 429, 503 |
| POST | `/api/v1/smart-match/dismissals` | Autenticado | `{ roomId }`: lo manda al final por 30 días | 204, 400, 401, 404, 429, 503 |
| DELETE | `/api/v1/smart-match/dismissals/{roomId}` | Autenticado | Deshace el descarte | 204, 400, 401, 429, 503 |

- Cada puntaje: `{ roomId, eligible, excludedBy: null | "budget" | "living_with", score: 0-100 | null, factors: [{ factor, points, max }], modelVersion }`. Los factores son `budget` (30), `distance` (25), `services` (20), `placeType` (10), `propertyType` (10), `extras` (5): `points` de cada factor suman `score`; `max` es su peso. Un cuarto que viola una restricción dura responde `eligible: false`, `score: null` y el motivo en `excludedBy`.
- `scores`: con token se usan las preferencias guardadas y la universidad del perfil, y el cuerpo solo trae `roomIds`; sin token el cuerpo trae además `universityId` y `preferences` con el mismo formato y validaciones de NV-11 (no se guarda). Un id que no es un cuarto publicado responde en `items` con `eligible: false` y `excludedBy: "unavailable"`, sin revelar si existió.
- `recommendations` y `recommendations/query`: `page`, `pageSize` (máximo 50); cada item: `{ room: <item de NV-14 search>, score, factors, modelVersion }`, orden `score` desc y después id; los descartados van al final; los no elegibles no aparecen. Sin universidad o sin presupuesto en el perfil → 422 `code=smart_match_profile_incomplete`.
- `modelVersion`: `baseline-1.0.0` o la versión del release de `TheHouse-ML` (por ejemplo `model-1.0.0`).
- Respuestas con `no-store`; los puntajes no se guardan: cambiar las preferencias cambia el siguiente cálculo (RF-58).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/smart-match/scores
METHOD       POST
AUTH         Público o Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { roomIds: [..] (1-50) } y, sin token, { universityId, preferences }
RESPONSE     200 { items: [{ roomId, eligible, excludedBy: null | "budget" | "living_with", score: 0-100 | null, factors: [{ factor, points, max }], modelVersion }] }
STATUS CODES 200, 400, 401 token inválido, 422 perfil incompleto, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/smart-match/recommendations?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [{ room, score, factors, modelVersion }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 422 code=smart_match_profile_incomplete, 429, 503
PAGINATION   Sí: sobre de NV-01; orden score desc, id
```

```text
ENDPOINT     /api/v1/smart-match/recommendations/query?page=1&pageSize=20
METHOD       POST
AUTH         Público (sin token); rate limit 30/min por IP
HEADERS      —
REQUEST      { universityId, preferences }
RESPONSE     igual que recommendations
STATUS CODES 200, 400, 404 universidad, 429, 503
PAGINATION   Sí
```

```text
ENDPOINT     /api/v1/smart-match/feedback
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { roomId, useful: true|false }
RESPONSE     204
STATUS CODES 204, 400, 401, 404 cuarto no publicado, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/smart-match/dismissals y /dismissals/{roomId}
METHOD       POST | DELETE
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { roomId } | roomId en la ruta
RESPONSE     204
STATUS CODES 204, 400, 401, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **SmartMatch** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `smart_match_feedback`, `smart_match_dismissals` |
| Usa (puertos de otros módulos) | Profile `IPreferencesRepository` y `IProfileRepository` (NV-11) para las preferencias y la universidad; Search `SearchService` (NV-14) para los cuartos candidatos publicados; Favorites `RoomCatalog` (NV-15 1.2.0) para resolver cuartos publicados por id; Catalog `IUniversityQuery` (NV-08) para la universidad de los invitados |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `smart_match_feedback`: `userId`, `roomId`, `useful`, `modelVersion`, `createdAt`, `updatedAt`; único `{userId, roomId}` (la última opinión reemplaza a la anterior).
- `smart_match_dismissals`: `userId`, `roomId`, `createdAt`; único `{userId, roomId}` e índice TTL de 30 días.
- Puerto `ICompatibilityModel` en Application con dos adaptadores en Infrastructure: `BaselineCompatibilityModel` (siempre disponible) y `OnnxCompatibilityModel` (`Microsoft.ML.OnnxRuntime`, CPU).
- Configuración `SmartMatch:Model`: `Enabled` (falso por defecto), ruta del `.onnx`, SHA-256 esperado y versión de esquema. El paquete de entrega del release de `TheHouse-ML` se incluye en la imagen del contenedor en el build, verificado con su `SHA256SUMS` (§13).

## 9. Reglas de negocio

1. Traducción de catálogos (contrato de features con `TheHouse-ML` ML-03): `placeType` `room` → cuarto `single` con baño `shared`; `room_private_bath` → baño `private`; `studio` → edificio `full_apartment`; `shared` → cuarto `double` o `shared`. `propertyTypes` `house` → `house_rooms`; `apartment` → `full_apartment`; `student_residence` → `rooms_building`. Servicios con el mismo catálogo de NV-11.
2. Restricciones duras: renta mayor que `budget.max` → `excludedBy: budget`; `livingWith` incompatible con `allowedTenants` (misma regla que el filtro de NV-14: `women` admite edificios `women` y `any`; `men` admite `men` y `any`; `any` admite todos) → `excludedBy: living_with`.
3. Mascotas: NV-12 no registra si el cuarto las admite; mientras tanto la preferencia `pets` es neutral (no suma ni excluye) y el desglose lo indica (§13).
4. Modelo base `baseline-1.0.0`: pesos `budget` (30), `distance` (25), `services` (20), `placeType` (10), `propertyType` (10), `extras` (5). Presupuesto completo dentro de `[min, max]` y decae por debajo de `min`; distancia completa hasta la mitad de `maxDistanceKm` y cae a 0 en `maxDistanceKm`, con distancia efectiva según `transport`; servicios = proporción de esenciales cubiertos; tipo de lugar y de inmueble por coincidencia; extras = servicios no esenciales. Las curvas exactas son las de ML-04 de `TheHouse-ML`; si difieren, mandan sus casos dorados.
5. El puntaje es determinista para las mismas preferencias, cuarto y `modelVersion` (RNF-13); cada factor y el total se redondean a entero y se acotan a su máximo.
6. Modelo ONNX: solo se usa si `Enabled`, el hash coincide, el esquema de features coincide y la prueba de arranque con un caso dorado pasa; si algo falla se registra una advertencia y se usa el modelo base, sin error para el usuario.
7. Recomendaciones: candidatos = cuartos publicados de la búsqueda de NV-14 para la universidad del perfil y `maxDistanceKm`, máximo 500 candidatos; se puntúan, se ordenan y se paginan.
8. Los descartes duran 30 días; la retroalimentación guarda la `modelVersion` para evaluar después.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Minimización (RNF-11): el cálculo solo lee preferencias de búsqueda y atributos públicos del cuarto; prueba que lo verifica.
- Rate limit 30/min por IP para invitados y 60/min por usuario; máximo 50 cuartos por `scores` y 500 candidatos por recomendación.
- El modelo y sus pesos no se exponen; solo el desglose por factor.
- El artefacto ONNX se verifica con SHA-256 antes de cargarse; nunca se carga pickle ni código ejecutable (constitution §6.1).
- La distancia usa la celda aproximada de NV-14, nunca la ubicación real del edificio.

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
| NV-23-R01 | Perfil y traducción de catálogos | El perfil se arma desde NV-11 o desde el cuerpo para invitados con las mismas validaciones; la traducción NV-11 → NV-12 de la §9 tiene una prueba por valor. | F1 | T01 |
| NV-23-R02 | Modelo base explicable | `baseline-1.0.0` con los pesos de la §9: desglose en puntos que suma el total, restricciones duras, mascotas neutral; determinista; casos de prueba propios con resultados esperados. | F1 | T02 |
| NV-23-R03 | Puntajes por cuarto | `scores` con hasta 50 cuartos, con token o como invitado; ids no publicados como `unavailable` sin revelar existencia; 422 con perfil incompleto. | F1 | T03 |
| NV-23-R04 | Recomendaciones ordenadas | Lista paginada sobre los candidatos de NV-14, orden `score` desc y id, descartes al final, no elegibles fuera; con y sin cuenta. | F2 | T04 |
| NV-23-R05 | Retroalimentación y descartes | Feedback con `modelVersion` y reemplazo de la opinión anterior; descartes con TTL de 30 días y opción de deshacer; idempotentes. | F2 | T05 |
| NV-23-R06 | Carga del modelo de TheHouse-ML | `OnnxCompatibilityModel` carga el paquete de entrega, verifica SHA-256 y esquema y hace la prueba de arranque; cualquier falla usa el modelo base y lo registra; desactivado por configuración. | F2 | T06 |
| NV-23-R07 | Paridad con TheHouse-ML | Los casos dorados de ML-04 dan el mismo desglose en `baseline-1.0.0` y los de ML-06 el mismo puntaje en ONNX dentro de la tolerancia publicada. | F3 | T07 |
| NV-23-R08 | Rendimiento y límites | p95 < 300 ms para 50 cuartos (RNF-14) y < 500 ms para recomendaciones con 500 candidatos en Azure; rate limits con 429; Redis caído 503. | F3 | T08 |
| NV-23-R09 | Minimización y privacidad | Prueba de que el cálculo no lee campos personales ni ubicación real; el desglose no expone pesos internos del modelo entrenado. | F3 | T09 |
| NV-23-R10 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (scores con y sin cuenta, recomendaciones, feedback y descartes). | F4 | T10 |
| NV-23-R11 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T11, T12, T13 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Enfoque | Modelo base por reglas en C# desde ahora; modelo ONNX de `TheHouse-ML` cuando se publique (constitution 1.2.0 §6.1) | APROBADO (M-BE-068) |
| Pesos iniciales | Presupuesto 30, distancia 25, servicios 20, tipo de lugar 10, tipo de inmueble 10, extras 5 (iguales a ML-04) | APROBADO (M-BE-068) |
| Distribución del artefacto | Release de `TheHouse-ML` descargado en el build de la imagen y verificado con `SHA256SUMS`; versión y hash fijados en el repositorio | APROBADO (M-BE-068) |
| Mascotas | Neutral hasta que NV-12 registre si el cuarto las admite (versión futura de NV-12 y de ML-03) | APROBADO (M-BE-068) |
| Convivencia | Misma regla que el filtro `allowedTenants` de NV-14 | APROBADO (M-BE-068) |
| Métrica de promoción | NDCG@10, decidida y medida en `TheHouse-ML` (ML-05) | APROBADO (M-BE-068) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Modelo base y ONNX en el mismo contenedor (CPU) | $0 | Sin servicios de ML administrados ni GPU |
| Entrenamiento | $0 | En `TheHouse-ML` (CPU local o GitHub Actions) |
| MongoDB Atlas M0 | $0 | Dos colecciones pequeñas con índices únicos y TTL |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-23-RK01 | Diferencias entre el modelo base en C# y el de Python | Puntajes distintos en la app y en la evaluación | Casos dorados de ML-04 en las pruebas del backend |
| NV-23-RK02 | Artefacto inválido o alterado | Puntajes erróneos | SHA-256, esquema y prueba de arranque; respaldo al modelo base |
| NV-23-RK03 | Modelo entrenado con datos sintéticos | Recomendaciones poco útiles | Se promueve solo si supera al modelo base (ML-05) y se reevalúa con señales reales (ML-07) |
| NV-23-RK04 | Mascotas sin dato en los cuartos | Recomendar un cuarto que no las admite | Factor neutral visible y versión futura de NV-12 |
| NV-23-RK05 | Latencia con muchos candidatos | Recomendaciones lentas | Límite de 500 candidatos y prueba p95 en Azure |

## 16. Dependencias

- Constitution 1.2.0.
- NV-11 y NV-14.
- `TheHouse-ML` ML-04 (casos dorados) y ML-06 (ONNX) para la T06 y la T07.
- NV-24 (explicaciones) y NV-25 (métricas).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 11 |
| Fases del plan | 4 |
| Tasks | 13 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T13 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
