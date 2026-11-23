# NV-23 — Evidencia: Smart Match con el modelo base de TheHouse-ML

| Campo | Valor |
| --- | --- |
| Spec | `spec.md` 1.2.0 (M-BE-085) |
| Autorización | «EJECUTA» de M-BE-068 trasladada a T01–T14 por «Apruebo NV-23 1.2.0» (Victor, 2026-11-20) |
| Rama | `feat/nv-23-smart-match` |
| Resultado parcial | **9 de 14** — T01–T05, T09, T10, T12 y T13 completas en local; T08, T11 y T14 esperan el despliegue; T06 y T07 **BLOCKED** por `TheHouse-ML` |

## 1. Qué se implementó

| Pieza | Archivo |
| --- | --- |
| Modelo base `baseline-1.0.0`: 10 features del esquema 2.0.0, HC01–HC03, curvas y redondeo de ML-04 | `TheHouse.Application/Common/SmartMatchModel.cs` |
| Parámetros copiados de `TheHouse-ML` (`src/thehouse_ml/models/baseline_params.json`, commit `85d1e4b`) como recurso incrustado con SHA-256 fijo | `TheHouse.Application/SmartMatch/baseline-1.0.0.json` |
| Casos de uso: puntajes, recomendaciones (con y sin cuenta), retroalimentación, descartes y métricas RF-66 | `TheHouse.Application/UseCases/SmartMatchService.cs` |
| Candidatos de NV-14 con distancia sin redondear y miniaturas solo de la página | `SearchService.SmartMatchCandidatesAsync` y `WithThumbnailsAsync` |
| `smart_match_feedback`, `smart_match_dismissals` (TTL 30 días) y `smart_match_daily_stats` (TTL 400 días, `$inc`, sin ids) | `TheHouse.Infrastructure/Persistence/Mongo/MongoSmartMatchRepository.cs` |
| 7 rutas, límites 60/min por usuario, 30/min por IP y 30/min para métricas; 422 con `missing` | `TheHouse/Http/SmartMatchEndpoint.cs` |
| Sesión opcional: `RevocationGuard.OptionalInstance` (sin encabezado → invitado; token inválido → 401) | `TheHouse/Http/RevocationGuard.cs` y `TokenRevocationMiddleware.cs` |
| Borrado con la cuenta (NV-10) sin cambiar el resumen previo | `SmartMatchDeletionParticipant` |
| Script de la base de datos regenerado: 34 colecciones y 71 índices | `scripts/db/01-schema.mongodb.js` (prueba de NV-26) |

Sin paquetes nuevos, secretos, workflows ni comandos en Production.

## 2. Paridad con TheHouse-ML (evidencia provisional de la T07)

- `TheHouse-ML` no ha publicado los casos dorados (ML-04 T05) ni el ONNX (ML-06): **T06 y T07 siguen BLOCKED**.
- Mientras tanto se ejecutó su implementación de referencia (`thehouse_ml.models.baseline_score.score_features`, commit `85d1e4b`, solo biblioteca estándar de Python, sin modificar su repositorio) sobre **2,000 vectores de features** con semilla fija, incluidos los bordes de las curvas y valores «redondos» (por ejemplo 0.555).
- `SmartMatchParityTests`: el C# da **exactamente** los mismos puntos por factor, la misma suma sin redondear (bit a bit) y el mismo `matchScore` en los 2,000 casos. Corpus: `TheHouse.UnitTests/Fixtures/smart-match-parity-ml-85d1e4b.json`.
- Ejemplo de la spec ML-04 §6.4: 25.5 / 17.0 / 13.3 / 10 / 10 / 2.1 y 77.976190 → **78**.

## 3. Pruebas

| Proyecto | Pruebas nuevas | Qué cubren |
| --- | --- | --- |
| Unitarias | `SmartMatchModelTests` (31) y `SmartMatchParityTests` (1) | Parámetros y hash; ejemplo de ML-04; redondeo (13.35 → 13.4; 50.5 → 51, nunca mitad al par); curvas; traducción de NV-12; perfil mínimo y valores por omisión; HC01–HC03 con todos los motivos; mascotas desconocidas; fórmulas y bordes de las features; perfil sin datos personales; participante de borrado; paridad con ML |
| Integración | `SmartMatchHttpTests` (18) | Forma del contrato; puntaje igual al cálculo directo; `unavailable` sin revelar existencia; convivencia estricta y presupuesto; sesión con preferencias guardadas y 400 si se mezclan; 422 con `missing`; orden, paginación, descartes al final y deshacer; invitado y 404 de universidad; feedback que reemplaza; BOLA de descartes; 400 por campos extra, catálogos, ids o más de 50 cuartos; 401 con token inválido; 429 por IP y por usuario; 503 con MongoDB o Redis caídos; métricas que fallan no afectan la respuesta; métricas solo admin y sin ids; borrado con la cuenta |
| Seguridad | `SmartMatchSecurityTests` (12) | Respuestas sin datos personales ni internos del modelo; métricas sin ids; contratos estrictos; guardia de sesión opcional separada de la obligatoria |

Suite completa en Release (`dotnet build -warnaserror`: 0 advertencias, 0 errores): unitarias 440/440, integración 519/519, seguridad 166/166 — **1,125/1,125** (las 2 pruebas de la lista de rutas de OpenAPI se actualizaron con las 7 rutas nuevas y se volvieron a ejecutar en verde).

## 4. Pendiente

| Task | Qué falta |
| --- | --- |
| T06 | Release ONNX de `TheHouse-ML` (ML-06) y autorización que nombre `Microsoft.ML.OnnxRuntime` |
| T07 | Casos dorados de ML-04 T05 (la paridad provisional de §2 no la sustituye) |
| T08 | p95 en Azure (< 300 ms para 50 cuartos, < 500 ms para 500 candidatos) después del despliegue |
| T11 | Despliegue por el CD, prueba HTTP real en Azure y aviso a Mobile con el contrato §6.1 |
| T14 | Cierre de evidencia con el CI de `main`, el despliegue y el costo |

## 5. Revisión de coherencia y seguridad

| Punto | Resultado |
| --- | --- |
| Spec §9 reglas 1–10 | Implementadas; `BaselineCompatibilityModel` vive en Application porque es cálculo puro (las pruebas unitarias lo cubren); el adaptador ONNX irá en Infrastructure (T06) |
| Sesión opcional | Nueva variante de la guardia de NV-06 solo en `scores` y `recommendations/query`; las demás rutas siguen exigiendo token |
| Minimización (RNF-11) | Solo preferencias de búsqueda y atributos públicos; distancia desde la celda aproximada; desglose sin valores de features |
| NV-10 | El resumen previo de eliminación no cambia (el contrato con Mobile sigue igual); el borrado es idempotente |
| NV-26 | Script regenerado por su propia prueba |
| Costo | $0: mismo contenedor y tres colecciones pequeñas en Atlas M0 |
