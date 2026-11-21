# NV-23 — Smart Match — compatibilidad con el modelo de TheHouse-ML

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `spec.md` |
| Versión | 1.2.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA — 1.2.0** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo NV-23 1.2.0»: decisiones de la §13; Mobile aceptó el cambio de contrato §6.1; la autorización «EJECUTA» de M-BE-068 pasa a T01–T14 con las mismas rutas: `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas, `scripts/db/` (regenerado por la prueba de NV-26) y `specs/nv-23-smart-match-ml/`; la T06 requiere además autorización que nombre `Microsoft.ML.OnnxRuntime`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-48 a RF-58, RF-64, RF-65, RF-66 (métricas, desde NV-25 1.1.0), RF-68 (uso sin cuenta) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-11, RNF-12, RNF-13, RNF-14 |
| Plan | `plan.md` 1.2.0 |
| Tasks | `task.md` 1.2.0 |
| Módulo | SmartMatch |
| Precondición | Constitution 1.2.0 (M-BE-062); NV-11, NV-12, NV-14 y NV-21 desplegadas; `TheHouse-ML` ML-04 T01–T03 en `main` |
| Relación | Reproduce el modelo base de `TheHouse-ML` ML-04 1.0.0 (`baseline-1.0.0`, esquema 2.0.0) y cargará su ONNX (ML-06); NV-24 explica los resultados; NV-10 borra sus datos con la cuenta; NV-26 incluye sus colecciones |

Spec aprobada (1.2.0, M-BE-085); la autorización «EJECUTA» de M-BE-068 cubre T01–T14 en las rutas indicadas.

**Cambio 1.2.0 (2026-11-20):** `TheHouse-ML` aprobó e implementó ML-04 1.0.0 después de NV-23 1.1.0 (`baseline_params.json` `baseline-1.0.0`, `feature_schema.json` **2.0.0** y `baseline_score.py`, merge #25 de `TheHouse-ML`). Para que el backend dé exactamente el mismo puntaje que ML, esta versión: (1) adopta los nombres de factores, las 10 features, las curvas y el **redondeo de ML-04** (cada factor con 1 decimal y el total con `floor(round6(Σ) + 0.5)`, mitad hacia arriba), que reemplazan el redondeo a entero de 1.1.0; (2) adopta las restricciones duras HC01–HC03 de ML-03 (**convivencia estricta** y mascotas como restricción con dato desconocido = no excluye); (3) exige universidad, `budget.max`, `livingWith` y `placeType` para calcular (perfil mínimo de ML) y aplica sus valores por omisión; (4) **cambia el contrato con Mobile**: `matchScore`, `breakdown` con `maxPoints` y `excludedBy` como lista (Mobile aceptó el cambio; valida la versión final de la §6.1); (5) agrega las métricas básicas de Smart Match para el administrador (RF-66, que NV-25 1.1.0 pasó a NV-23, M-BE-075); (6) borra la retroalimentación y los descartes al eliminar la cuenta (NV-10) y (7) suma las colecciones nuevas al script de la base de datos (NV-26). Mientras 1.2.0 no se apruebe sigue vigente 1.1.0 (autorizada, sin código escrito); al aprobarse, la autorización «EJECUTA» de M-BE-068 pasa a T01–T14 de 1.2.0.

## 1. Contexto

Con NV-11 el estudiante guarda presupuesto, distancia, servicios, con quién vivir, tipo de lugar, mascotas, transporte y tipos de inmueble; con NV-14 existen cuartos publicados con precio, tipo, baño, servicios y distancia aproximada. Falta el porcentaje de compatibilidad que ordena los cuartos (RF-48 a RF-57). El profesor pide que el Machine Learning sea un proyecto separado: `TheHouse-ML` entrena y evalúa en Python y entrega un ONNX; el backend solo lo ejecuta. Como ese modelo tardará, el backend arranca con el modelo base por reglas, explicable y con los mismos pesos, para que la app tenga Smart Match desde ahora.

## 2. Objetivo

Calcular y exponer un puntaje de compatibilidad 0–100 con desglose por factor para cuartos publicados, con modelo base por reglas y, cuando exista, el modelo ONNX de TheHouse-ML, determinista, explicable y a costo $0.

## 3. Precondiciones

1. Constitution 1.2.0 aprobada (M-BE-062).
2. NV-11, NV-12, NV-14 y NV-21 desplegadas.
3. Validación de la versión final del contrato §6.1 por el responsable de Mobile (aceptó el cambio el 2026-11-20).
4. «Apruebo» de NV-23 1.2.0 con las decisiones de la §13; la autorización «EJECUTA» de M-BE-068 se traslada a T01–T14.
5. Para la T06: release del modelo publicado por `TheHouse-ML` (ML-06) y una autorización que nombre el paquete `Microsoft.ML.OnnxRuntime`; para la T07: casos dorados de ML-04 (T05). Sin ellos esas tasks quedan BLOCKED y el resto se completa con el modelo base.

## 4. Alcance

1. Perfil Smart Match desde las preferencias guardadas (NV-11) o enviadas por un invitado (RF-48, RF-68), con el perfil mínimo y los valores por omisión del esquema 2.0.0 de ML.
2. Traducción de cuartos y edificios de NV-12 a los catálogos del esquema de ML y restricciones duras HC01–HC03.
3. Las 10 features del esquema 2.0.0 y el modelo base `baseline-1.0.0` en C# con desglose por factor y redondeo de ML-04 (RF-49 a RF-56).
4. `POST /smart-match/scores` para hasta 50 cuartos.
5. Recomendaciones ordenadas por compatibilidad, con y sin cuenta (RF-57, RF-68).
6. Retroalimentación útil/no útil y descartes (RF-64, RF-65), borrados al eliminar la cuenta (NV-10).
7. Métricas básicas de Smart Match para el administrador (RF-66).
8. Carga del modelo ONNX de `TheHouse-ML` con verificación de hash y esquema, y respaldo al modelo base.
9. Paridad con los casos dorados de `TheHouse-ML`.
10. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Entrenamiento, datos y evaluación del modelo: `TheHouse-ML` (ML-02 a ML-07).
- Explicación en lenguaje natural: NV-24.
- Estadísticas del arrendador: NV-25.
- Exportación anonimizada de señales para ML-07: spec futura.
- Registrar en NV-12 si un cuarto admite mascotas: versión futura de NV-12 (mientras tanto HC03 no excluye).
- Uso de datos personales (nombre, correo, género, edad) como variables (RNF-11).
- Guardar puntajes o el historial de recomendaciones de cada usuario.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/smart-match/scores` | Público o autenticado | Puntajes para `roomIds` (1–50) | 200, 400, 401, 422, 429, 503 |
| GET | `/api/v1/smart-match/recommendations` | Autenticado | Cuartos ordenados por compatibilidad con las preferencias guardadas | 200, 400, 401, 422, 429, 503 |
| POST | `/api/v1/smart-match/recommendations/query` | Público | Lo mismo para invitados con preferencias en el cuerpo | 200, 400, 404, 422, 429, 503 |
| POST | `/api/v1/smart-match/feedback` | Autenticado | `{ roomId, useful }` | 204, 400, 401, 404, 429, 503 |
| POST | `/api/v1/smart-match/dismissals` | Autenticado | `{ roomId }`: lo manda al final por 30 días | 204, 400, 401, 404, 429, 503 |
| DELETE | `/api/v1/smart-match/dismissals/{roomId}` | Autenticado | Deshace el descarte | 204, 400, 401, 429, 503 |
| GET | `/api/v1/admin/smart-match/metrics` | Staff con rol `admin` (NV-21) | Métricas básicas de Smart Match (RF-66) | 200, 400, 401, 403, 429, 503 |

- Cada puntaje: `{ roomId, eligible, excludedBy: [] | ["budget" | "living_with" | "pets" | "unavailable"], matchScore: 0-100 | null, breakdown: [{ factor, label, points, maxPoints }], modelVersion, featureSchemaVersion }`. Los factores van siempre en este orden: `budget` Presupuesto (30), `distance` Distancia (25), `essential_services` Servicios esenciales (20), `place_type` Tipo de lugar (10), `property_type` Tipo de inmueble (10), `extras` Extras (5). `points` tiene 1 decimal; `matchScore` es el total redondeado (la suma de `points` puede diferir del total hasta en 0.8, como en ML-04 §6.3). Un cuarto que viola una restricción dura responde `eligible: false`, `matchScore: null`, `breakdown: []` y los motivos en `excludedBy`.
- `scores`: con token se usan las preferencias guardadas y la universidad del perfil, y el cuerpo solo trae `roomIds`; sin token el cuerpo trae además `universityId` y `preferences` con el mismo formato y validaciones de NV-11 (no se guarda). Un id que no es un cuarto publicado responde en `items` con `eligible: false` y `excludedBy: ["unavailable"]`, sin revelar si existió.
- `recommendations` y `recommendations/query`: `page`, `pageSize` (máximo 50); cada item: `{ room: <item de NV-14 search>, matchScore, breakdown, dismissed, modelVersion, featureSchemaVersion }`; primero los no descartados y después los descartados (`dismissed: true`), cada grupo por puntaje sin redondear desc y después `roomId`; los no elegibles no aparecen.
- Perfil incompleto (sin universidad, `budget.max`, `livingWith` o `placeType`) → 422 `code=smart_match_profile_incomplete` con la extensión `missing` (lista de esos campos), con o sin cuenta.
- `modelVersion`: `baseline-1.0.0` o la versión del release de `TheHouse-ML` (por ejemplo `model-1.0.0`); `featureSchemaVersion`: `2.0.0`.
- Respuestas con `no-store`; los puntajes no se guardan: cambiar las preferencias cambia el siguiente cálculo (RF-58).
- Métricas (RF-66): `from` y `to` en `YYYY-MM-DD` (por omisión los últimos 30 días; máximo 90); respuesta `{ from, to, totals: { scoreRequests, recommendationRequests, guestRequests, roomsScored, eligibleRooms, averageMatchScore, scoreBuckets: { "0-39", "40-59", "60-79", "80-100" }, feedbackUseful, feedbackNotUseful, activeDismissals }, byModelVersion: [{ modelVersion, roomsScored, averageMatchScore, feedbackUseful, feedbackNotUseful }] }`; sin datos de usuarios.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/smart-match/scores
METHOD       POST
AUTH         Público o Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { roomIds: [..] (1-50) } y, sin token, { universityId, preferences }
RESPONSE     200 { items: [{ roomId, eligible, excludedBy: [] | ["budget" | "living_with" | "pets" | "unavailable"], matchScore: 0-100 | null, breakdown: [{ factor, label, points, maxPoints }], modelVersion, featureSchemaVersion }] }
STATUS CODES 200, 400, 401 token inválido, 422 code=smart_match_profile_incomplete (missing), 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/smart-match/recommendations?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [{ room: <item de NV-14 search>, matchScore, breakdown, dismissed, modelVersion, featureSchemaVersion }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 422 code=smart_match_profile_incomplete (missing), 429, 503
PAGINATION   Sí: sobre de NV-01; no descartados primero, puntaje desc, roomId
```

```text
ENDPOINT     /api/v1/smart-match/recommendations/query?page=1&pageSize=20
METHOD       POST
AUTH         Público (sin token); rate limit 30/min por IP
HEADERS      —
REQUEST      { universityId, preferences }
RESPONSE     igual que recommendations (dismissed siempre false)
STATUS CODES 200, 400, 404 universidad, 422 code=smart_match_profile_incomplete (missing), 429, 503
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
| Colecciones propias | `smart_match_feedback`, `smart_match_dismissals`, `smart_match_daily_stats` |
| Usa (puertos de otros módulos) | Profile `IPreferencesRepository` y `IProfileRepository` (NV-11) para las preferencias y la universidad; Search `SearchService` (NV-14) para los cuartos candidatos publicados y su distancia aproximada; Favorites `RoomCatalog` (NV-15 1.2.0) para resolver cuartos publicados por id; Catalog `IUniversityQuery` (NV-08) para la universidad de los invitados; Identity `IAccountDeletionParticipant` (NV-10) para borrar sus datos con la cuenta; Moderation roles de staff (NV-21) para las métricas |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `smart_match_feedback`: `userId`, `roomId`, `useful`, `modelVersion`, `createdAt`, `updatedAt`; único `{userId, roomId}` (la última opinión reemplaza a la anterior).
- `smart_match_dismissals`: `userId`, `roomId`, `createdAt`; único `{userId, roomId}` e índice TTL de 30 días.
- `smart_match_daily_stats` (RF-66): un documento por día UTC y `modelVersion` con contadores (`scoreRequests`, `recommendationRequests`, `guestRequests`, `roomsScored`, `eligibleRooms`, `scoreSum` y 4 rangos de puntaje) actualizados con `$inc`; sin ids de usuario ni de cuarto; TTL de 400 días.
- Puerto `ICompatibilityModel` en Application con dos adaptadores en Infrastructure: `BaselineCompatibilityModel` (siempre disponible) y `OnnxCompatibilityModel` (`Microsoft.ML.OnnxRuntime`, CPU).
- Parámetros de `baseline-1.0.0` (pesos, curvas lineales por tramos y redondeo) en un recurso versionado copiado de `TheHouse-ML` (`baseline_params.json`) con su commit de origen; una prueba falla si se edita sin cambiar la versión.
- Configuración `SmartMatch:Model`: `Enabled` (falso por defecto), ruta del `.onnx`, SHA-256 esperado y versión de esquema. El paquete de entrega del release de `TheHouse-ML` se incluye en la imagen del contenedor en el build, verificado con su `SHA256SUMS` (§13).

## 9. Reglas de negocio

1. Cuarto en el esquema de ML (traducción desde NV-12): `placeType` = `studio` si el edificio es `full_apartment`; si no, `shared` si el cuarto es `double` o `shared`; si no, `room_private_bath` si el baño es `private`; si no, `room`. `propertyType`: `house_rooms` → `house`, `full_apartment` → `apartment`, `rooms_building` → `student_residence`. `livingWith` = `allowedTenants` del edificio; `services` = servicios del edificio (mismo catálogo de 11 de NV-11); `price_mxn` = renta del cuarto (entero 500–50,000).
2. Perfil mínimo (esquema 2.0.0): universidad, `budget.max`, `livingWith` y `placeType`; si falta alguno → 422 con `missing`. Valores por omisión: `budget.min` nulo → 0; `maxDistanceKm` nulo → 30; `essentialServices` nulo → vacío; `pets` nulo → false; `transport` nulo → `public`; `propertyTypes` nulo o vacío → acepta todos.
3. Restricciones duras (ML-03, se reportan todas las que apliquen): HC01 renta > `budget.max` → `budget`; HC02 `livingWith` del estudiante distinto de `any` y distinto del `allowedTenants` del edificio → `living_with` (regla estricta: `women` solo admite `women`, `men` solo `men`, `any` admite todos; el filtro de búsqueda de NV-14 no cambia); HC03 estudiante con mascota y cuarto que no la admite → `pets` (NV-12 no registra el dato: desconocido = no excluye, así que hoy nunca excluye).
4. Features (esquema 2.0.0, en su `feature_order`): `distance_km` (distancia publicada de NV-14 desde el centro de la celda aproximada del edificio hasta el campus de la universidad del estudiante), `effective_distance_km` (× 1.0 a pie, 0.5 bici, 0.4 transporte público, 0.25 auto), `price_to_budget`, `services_coverage`, `place_type_match`, `property_type_match`, `pets_compatible`, `distance_ratio` (`distance_km / maxDistanceKm`), `price_below_min` y `extra_services_ratio`, con las fórmulas exactas de `feature_schema.json` 2.0.0.
5. Modelo base `baseline-1.0.0` (ML-04 §6.2): `budget` Presupuesto (30), `distance` Distancia (25), `essential_services` Servicios esenciales (20), `place_type` Tipo de lugar (10), `property_type` Tipo de inmueble (10), `extras` Extras (5); cada factor vale `peso × curva(feature)`: presupuesto `1 − price_below_min`; distancia 1 hasta `distance_ratio` 0.5, baja lineal a 0 en 1 y 0 después; servicios, tipo de lugar, tipo de inmueble y extras con la identidad. Las features `distance_km`, `effective_distance_km`, `price_to_budget` y `pets_compatible` no puntúan (quedan para el ONNX).
6. Redondeo (ML-04 §6.3, en `double` como Python): cada factor = `Math.Round(Math.Round(puntos, 6, AwayFromZero), 1, AwayFromZero)`; `matchScore` = `Math.Round(Math.Round(Σ puntos sin redondear, 6, AwayFromZero), AwayFromZero)` acotado a 0–100; nunca `Math.Round` por omisión (mitad al par). Determinista para las mismas preferencias, cuarto y `modelVersion` (RNF-13).
7. Modelo ONNX: solo se usa si `Enabled`, el hash coincide, el esquema de features coincide y la prueba de arranque con un caso dorado pasa; si algo falla se registra una advertencia y se usa el modelo base, sin error para el usuario. El desglose con ONNX lo define ML-06.
8. Recomendaciones: candidatos = cuartos publicados de la búsqueda de NV-14 para la universidad del perfil y `maxDistanceKm` resuelto; si hay más de 500 se toman los 500 más cercanos (empate por id); se aplican HC01–HC03, se puntúan, se ordenan (no descartados primero; puntaje sin redondear desc; `roomId` asc) y se paginan.
9. Los descartes duran 30 días; la retroalimentación guarda la `modelVersion` para evaluar después. Al eliminar la cuenta (NV-10) un participante de `IAccountDeletionParticipant` borra la retroalimentación y los descartes del usuario.
10. Métricas (RF-66): cada cálculo suma a los contadores del día con `$inc`; si esa escritura falla se registra una advertencia y la respuesta al usuario no cambia. La retroalimentación por versión y los descartes activos se cuentan al pedir las métricas.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Minimización (RNF-11): el cálculo solo lee preferencias de búsqueda y atributos públicos del cuarto y del edificio; prueba que lo verifica.
- Rate limit 30/min por IP para invitados, 60/min por usuario y 30/min para las métricas; máximo 50 cuartos por `scores` y 500 candidatos por recomendación.
- El desglose expone `factor`, `label`, `points` y `maxPoints` (pesos públicos del modelo base); los valores de las features y los parámetros internos de un modelo entrenado no se exponen.
- Las métricas no guardan ids de usuario ni de cuarto; solo staff con rol `admin` las consulta (403 para otros roles).
- El artefacto ONNX se verifica con SHA-256 antes de cargarse; nunca se carga pickle ni código ejecutable (constitution §6.1).
- La distancia usa la celda aproximada de NV-14, nunca la ubicación real del edificio.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Métricas sin rol `admin` | 403 | Prohibido |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Perfil sin universidad, `budget.max`, `livingWith` o `placeType` | 422 | Solicitud no procesable (`code=smart_match_profile_incomplete`, extensión `missing`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-23-R01 | Perfil y traducción al esquema de ML | El perfil se arma desde NV-11 o desde el cuerpo para invitados con las mismas validaciones, el perfil mínimo y los valores por omisión del esquema 2.0.0; la traducción de NV-12 de la §9 tiene una prueba por valor. | F1 | T01 |
| NV-23-R02 | Modelo base alineado con ML-04 | Las 10 features y `baseline-1.0.0` con nombres, curvas, orden y redondeo de ML-04; HC01–HC03 con todos los motivos; desglose de 6 factores; determinista; casos propios con resultados calculados a mano, incluidos los límites de redondeo (por ejemplo 13.35 → 13.4). | F1 | T02 |
| NV-23-R03 | Puntajes por cuarto | `scores` con hasta 50 cuartos, con token o como invitado; ids no publicados como `unavailable` sin revelar existencia; 422 con `missing`. | F1 | T03 |
| NV-23-R04 | Recomendaciones ordenadas | Lista paginada sobre hasta 500 candidatos de NV-14, no descartados primero, puntaje sin redondear desc y `roomId`; no elegibles fuera; con y sin cuenta. | F2 | T04 |
| NV-23-R05 | Retroalimentación y descartes | Feedback con `modelVersion` y reemplazo de la opinión anterior; descartes con TTL de 30 días y opción de deshacer; idempotentes; borrados al eliminar la cuenta (NV-10); colecciones en el script de NV-26. | F2 | T05 |
| NV-23-R06 | Carga del modelo de TheHouse-ML | `OnnxCompatibilityModel` carga el paquete de entrega, verifica SHA-256 y esquema y hace la prueba de arranque; cualquier falla usa el modelo base y lo registra; desactivado por configuración. | F2 | T06 |
| NV-23-R07 | Paridad con TheHouse-ML | Los casos dorados de ML-04 (T05) dan el mismo vector de features, el mismo desglose y el mismo `matchScore` en `baseline-1.0.0`, y los de ML-06 el mismo puntaje en ONNX dentro de la tolerancia publicada. | F3 | T07 |
| NV-23-R08 | Rendimiento y límites | p95 < 300 ms para 50 cuartos (RNF-14) y < 500 ms para recomendaciones con 500 candidatos en Azure; rate limits con 429; Redis caído 503. | F3 | T08 |
| NV-23-R09 | Minimización y privacidad | Prueba de que el cálculo no lee campos personales ni ubicación real; el desglose no expone valores de features ni parámetros de un modelo entrenado; las métricas no guardan ids. | F3 | T09 |
| NV-23-R10 | Métricas de Smart Match (RF-66) | `GET /admin/smart-match/metrics` con rango de fechas (máximo 90 días), totales, promedio y rangos de puntaje por `modelVersion`, retroalimentación y descartes activos; solo rol `admin`; contadores diarios sin ids; una falla al contar no afecta la respuesta al usuario. | F3 | T10 |
| NV-23-R11 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (scores con y sin cuenta, recomendaciones, feedback, descartes y métricas). | F4 | T11 |
| NV-23-R12 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T12, T13, T14 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Enfoque | Modelo base por reglas en C# desde ahora; modelo ONNX de `TheHouse-ML` cuando se publique (constitution 1.2.0 §6.1) | APROBADO (M-BE-068) |
| Pesos iniciales | Presupuesto 30, distancia 25, servicios esenciales 20, tipo de lugar 10, tipo de inmueble 10, extras 5 (iguales a ML-04) | APROBADO (M-BE-068) |
| Distribución del artefacto | Release de `TheHouse-ML` descargado en el build de la imagen y verificado con `SHA256SUMS`; versión y hash fijados en el repositorio | APROBADO (M-BE-068) |
| Métrica de promoción | NDCG@10, decidida y medida en `TheHouse-ML` (ML-05) | APROBADO (M-BE-068) |
| Alineación con ML-04 1.0.0 | Nombres de factores, 10 features del esquema 2.0.0, curvas, orden del ranking y redondeo de ML-04 (1 decimal por factor y `floor(round6(Σ) + 0.5)`), en lugar del redondeo a entero de 1.1.0 | APROBADO (M-BE-085) |
| Convivencia | Regla estricta de ML-03 (HC02) en Smart Match: `women` solo con `women`, `men` solo con `men`, `any` con todos; reemplaza la regla de NV-14 en esta spec (el filtro de búsqueda no cambia) | APROBADO (M-BE-085) |
| Mascotas | HC03 implementada; mientras NV-12 no registre el dato, el cuarto cuenta como que sí admite mascotas (no excluye), igual al «neutral» de 1.1.0 | APROBADO (M-BE-085) |
| Distancia | `distance_km` = distancia publicada de NV-14 desde la celda aproximada (radio 6371.0088 km); la paridad con ML se mide sobre el vector de features de los casos dorados | APROBADO (M-BE-085) |
| Perfil mínimo | Universidad, `budget.max`, `livingWith` y `placeType` obligatorios (422 con `missing`) y valores por omisión del esquema 2.0.0 (`maxDistanceKm` nulo → 30) | APROBADO (M-BE-085) |
| Contrato con Mobile | `matchScore`, `breakdown` `{ factor, label, points, maxPoints }`, `excludedBy` como lista, `dismissed` y `featureSchemaVersion`; sin los valores de las features (los usa NV-24 dentro del backend) | APROBADO (M-BE-085) |
| Métricas RF-66 | Contadores diarios agregados con `$inc` (sin ids, TTL 400 días) más conteos de retroalimentación y descartes al consultar; solo rol `admin` | APROBADO (M-BE-085) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Modelo base y ONNX en el mismo contenedor (CPU) | $0 | Sin servicios de ML administrados ni GPU |
| Entrenamiento | $0 | En `TheHouse-ML` (CPU local o GitHub Actions) |
| MongoDB Atlas M0 | $0 | Tres colecciones pequeñas con índices únicos y TTL; un documento de métricas por día y versión |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-23-RK01 | Diferencias entre el modelo base en C# y el de Python | Puntajes distintos en la app y en la evaluación | Mismos parámetros que `baseline_params.json`, redondeo `AwayFromZero` con round6 y casos dorados de ML-04 en las pruebas |
| NV-23-RK02 | ML-04 T05 (casos dorados) o ML-06 (ONNX) no llegan antes de la evaluación | T06 y T07 BLOCKED; el ML visible es el modelo base | El resto funciona con `baseline-1.0.0`; mientras tanto se compara con `baseline_score.py` de `TheHouse-ML` en un commit fijado (evidencia, no sustituye la T07) |
| NV-23-RK03 | Artefacto inválido o alterado | Puntajes erróneos | SHA-256, esquema y prueba de arranque; respaldo al modelo base |
| NV-23-RK04 | Convivencia estricta distinta del filtro de búsqueda | Un cuarto `any` aparece en la búsqueda para `women` pero no en Smart Match | Decisión explícita (§13) y motivo `living_with` visible en `scores` |
| NV-23-RK05 | Mascotas sin dato en los cuartos | Recomendar un cuarto que no las admite | HC03 lista para cuando NV-12 registre el dato |
| NV-23-RK06 | Latencia con muchos candidatos | Recomendaciones lentas | Límite de 500 candidatos y prueba p95 en Azure |

## 16. Dependencias

- Constitution 1.2.0.
- NV-11, NV-12, NV-14 y NV-21 (rol `admin`).
- `TheHouse-ML` ML-04 1.0.0 (`baseline_params.json`, `feature_schema.json` 2.0.0; casos dorados de T05 para la T07) y ML-06 (ONNX) para la T06.
- NV-10 (`IAccountDeletionParticipant`) y NV-26 (script de la base de datos).
- NV-24 (explicaciones): consume `matchScore` y el desglose interno.

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 12 |
| Fases del plan | 4 |
| Tasks | 14 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA — 1.2.0** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
