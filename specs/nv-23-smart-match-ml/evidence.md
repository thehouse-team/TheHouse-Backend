# NV-23 — Evidencia: Smart Match con el modelo base de TheHouse-ML

| Campo | Valor |
| --- | --- |
| Spec | `spec.md` 1.2.0 (M-BE-085) |
| Autorización | «EJECUTA» de M-BE-068 trasladada a T01–T14 por «Apruebo NV-23 1.2.0» (Victor, 2026-11-20) |
| Rama | `feat/nv-23-smart-match` |
| Resultado | **PASS WITH WARNINGS — 12 de 14** (2026-11-24); T06 y T07 **BLOCKED** por `TheHouse-ML` |

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

## 6. Despliegue y prueba real en Azure (T08, T11, T14) — 2026-11-24

| Paso | Resultado |
| --- | --- |
| PRs | [#80](https://github.com/thehouse-team/TheHouse-Backend/pull/80) (spec 1.2.0) y [#81](https://github.com/thehouse-team/TheHouse-Backend/pull/81) (implementación), mezclados en `main` |
| CI de `main` | Verde en `f73beb1` (merge del #81) y `6ee59bf` |
| Hallazgo del CD | El CD del #81 quedó en cola detrás de un CD antiguo que esperaba aprobación desde el 2026-11-20 (#71) y se canceló; el siguiente (#76, merge del #79) **omitió el despliegue** porque la regla NV-09-R17 compara solo contra el commit anterior y ese merge solo cambiaba documentación. Se resolvió con un CD manual desde `main` |
| CD manual #77 (`workflow_dispatch`, `6ee59bf`) | resolve, publish, verify-ephemeral, deploy, dast-baseline y cd-summary en verde; despliegue aprobado por un integrante distinto de quien lo lanzó |
| Revisión activa | `ca-thehouse-u2--c6ee59bf-77-1`, Healthy, 100 % del tráfico; `/health/ready` 200 |
| Rol admin temporal (M-BE-065) | Autorizado por Victor el 2026-11-24 («Autorizo rol»): `--grant-role` a la cuenta de prueba de admin (usuario `6b05aa6c…`) antes de las métricas y `--revoke-role` al terminar, vía `az containerapp exec`; ambos en la bitácora con actor `cli`. El archivo local con las credenciales de prueba se borró |

### Prueba HTTP real (`.azure-check/nv23_azure_check.py`, fuera de git): **30/30 PASS**

| Bloque | Resultado |
| --- | --- |
| Invitado (G1–G9) | Contrato con `no-store`; `baseline-1.0.0`, esquema 2.0.0 y 6 factores en orden (ejemplo: 95 = 30 + 25 + 20 + 10 + 10 + 0); id inexistente `unavailable`; convivencia estricta (`any` excluido para `women`); 11 recomendaciones ordenadas (96 … 68); 422 `smart_match_profile_incomplete` con `missing`; 400 por campo extra; 401 con token inválido |
| Con cuenta (U1–U13) | 422 con perfil vacío (4 campos); recomendaciones y puntajes con las preferencias guardadas; 400 si se mezclan preferencias con sesión; feedback 204 que reemplaza; descarte al final y deshacer; otro usuario no toca ni ve descartes ajenos (BOLA); 404 en un cuarto no publicado |
| Métricas RF-66 (M1–M4) | 403 sin rol admin; 200 con rol admin: `scoreRequests` 3, `recommendationRequests` 5, `feedbackNotUseful` 1, promedio 82.3; sin correos ni ids |
| Tiempos (T1–T2) | Ver abajo |
| Limpieza (Z) | Las cuentas de prueba del estudiante y del otro usuario se eliminaron con NV-10 (204), lo que borra su retroalimentación y descartes |

Hay 11 cuartos publicados cerca de la UTTT (12 sembrados; uno ya no está disponible).

### T08 — Rendimiento: **PASS WITH WARNINGS**

| Medición (desde la computadora de Victor, 20 llamadas cada una) | p95 | Mediana |
| --- | --- | --- |
| `POST /smart-match/scores` con 50 ids | 498 ms | 399 ms |
| `GET /smart-match/recommendations` (11 candidatos) | 445 ms | 303 ms |
| `/health/live` (solo red) | 226 ms | — |

Restando la ida y vuelta de la red (~226 ms), el tiempo del servidor queda en ~272 ms (p95) para 50 cuartos, debajo de los 300 ms de RNF-14, y en ~219 ms para recomendaciones, debajo de 500 ms. **Advertencia:** la medición es desde el cliente y la producción tiene 11 candidatos, no 500; la prueba con 500 candidatos queda para una prueba de carga (NV-09 T16).

### T11 — Contrato con Mobile

Contrato §6.1 de 1.2.0 entregado a Mobile (el responsable aceptó el cambio el 2026-11-20); `DEPLOYED URL` y `TESTS` completados en la spec.

### Costo

$0: el mismo contenedor (escala 0–2) y tres colecciones pequeñas en Atlas M0. Cost Management del 2026-11-19 al 2026-11-23: solo `rg-thehouse-u2`, 0.00 USD por día.

## 7. Resultado

**PASS WITH WARNINGS — 12 de 14.** Advertencias: T08 medido desde el cliente con 11 candidatos. **T06 y T07 siguen BLOCKED** hasta que `TheHouse-ML` publique el ONNX (ML-06) y los casos dorados (ML-04 T05); mientras tanto la paridad provisional de §2 (2,000 casos, bit a bit) cubre el modelo base.
