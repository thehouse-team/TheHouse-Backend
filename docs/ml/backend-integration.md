# Guía de integración del modelo `logreg-1.0.0` en el backend (NV-23)

| Campo | Valor |
| --- | --- |
| Documento | `docs/backend-integration.md` |
| Spec | ML-06 1.0.0 (§6.1, §6.6, §6.7; M-ML-044), task T06 (ML-06-R06) |
| Release | [`model-v1.0.0`](https://github.com/thehouse-team/TheHouse-ML/releases/tag/model-v1.0.0) (commit `59f07bcfe1fa6cf4a0f183b568c56913980175ee`) |
| Modelo | `logreg-1.0.0` · esquema de features `2.0.0` |
| Destinatario | Equipo de `TheHouse-Backend` (.NET), NV-23, con `Microsoft.ML.OnnxRuntime` |
| Fecha | 2026-11-25 |

> **DATOS SINTÉTICOS.** `logreg-1.0.0` se entrenó y evaluó con datos sintéticos (constitución §8). El puntaje es una heurística aprendida de etiquetas simuladas, no de preferencias reales de estudiantes. Las métricas de `metrics.json` y de `model_card.md` son sintéticas.

> **Ejemplos de C#:** todos los fragmentos de esta guía son **de referencia**. No se compilan ni se prueban en este repositorio. Los nombres de clases y métodos (`OnnxCompatibilityModel`, `ScoreRounding`, etc.) son ilustrativos: adáptalos a las convenciones de `TheHouse-Backend`. El único nombre que viene de la spec es el puerto `ICompatibilityModel`.

## Contenido

1. [Resumen en una página](#1-resumen-en-una-página)
2. [Qué hace el backend y qué hace el modelo](#2-qué-hace-el-backend-y-qué-hace-el-modelo)
3. [Descargar el release y verificar el SHA-256](#3-descargar-el-release-y-verificar-el-sha-256)
4. [Cargar el modelo una sola vez](#4-cargar-el-modelo-una-sola-vez)
5. [Pasos 1–3: faltantes, perfil incompleto y restricciones duras](#5-pasos-13-faltantes-perfil-incompleto-y-restricciones-duras)
6. [Paso 4: calcular las 10 features](#6-paso-4-calcular-las-10-features)
7. [Paso 5: inferencia con ONNX](#7-paso-5-inferencia-con-onnx)
8. [Paso 6: redondeo oficial](#8-paso-6-redondeo-oficial)
9. [Paso 7: JSON de respuesta](#9-paso-7-json-de-respuesta)
10. [Paso 8: ranking](#10-paso-8-ranking)
11. [Casos dorados como pruebas automáticas en .NET](#11-casos-dorados-como-pruebas-automáticas-en-net)
12. [Desviación conocida ML-05 T05-H1](#12-desviación-conocida-ml-05-t05-h1)
13. [Rendimiento esperado](#13-rendimiento-esperado)
14. [Versiones nuevas del modelo](#14-versiones-nuevas-del-modelo)
15. [Nota para `TheHouse-GenAI`](#15-nota-para-thehouse-genai)
16. [Lista de verificación final](#16-lista-de-verificación-final)
17. [Lo que esta guía no define](#17-lo-que-esta-guía-no-define)

## 1. Resumen en una página

El modelo es un archivo `model.onnx` de 4,380 bytes. Recibe una matriz de números y devuelve puntos por factor. **No** sabe nada de estudiantes, cuartos, JSON ni reglas de negocio: todo eso lo hace el backend.

```text
perfil del estudiante + cuarto
        │
        ▼
[Backend] 1. resolver campos nulos (reglas del esquema 2.0.0)
[Backend] 2. ¿falta budget.max, livingWith o placeType? → incomplete = true (fin)
[Backend] 3. ¿viola HC01, HC02 o HC03?                 → excluded = true   (fin)
[Backend] 4. calcular las 10 features en orden y validarlas
        │   double[N, 10]
        ▼
[ONNX]    5. recorte → escalado → contribuciones → suma por factor → calibración
        │   factor_points double[N, 6], raw_total double[N]  (sin redondear)
        ▼
[Backend] 6. redondear (round6, AwayFromZero) y normalizar -0 → 0
[Backend] 7. armar el JSON (matchScore + breakdown)
[Backend] 8. ordenar por raw_total descendente y room_id ascendente
```

Cinco reglas que no se pueden olvidar:

1. **Verificar el SHA-256** de `model.onnx` contra el valor fijado en el repositorio del backend antes de cargarlo (§3).
2. **Crear la sesión de ONNX una sola vez** y reutilizarla (§4).
3. **El ONNX solo puntúa pares que ya pasaron** el perfil incompleto y HC01–HC03 (§5).
4. **Redondear con `MidpointRounding.AwayFromZero`**, nunca con el `Math.Round` por omisión, y aplicar round6 antes (§8).
5. **Correr los casos dorados en el CI del backend** con diferencia 0 en `matchScore` y en los puntos (§11).

## 2. Qué hace el backend y qué hace el modelo

Reparto oficial (spec ML-06 §6.1, decisión 3 de M-ML-044):

| Paso | Quién | Qué |
| --- | --- | --- |
| 1. Faltantes | Backend | Resolver los campos opcionales nulos con las reglas `missing` de `feature_schema.json` 2.0.0 (§5.1) |
| 2. Perfil incompleto | Backend | Si falta `budget.max`, `livingWith` o `placeType`: `incomplete = true` con `missingFields` en ese orden. Se revisa **antes** que las restricciones |
| 3. Restricciones duras | Backend | HC01–HC03; un par excluido responde `excluded = true` con `violatedConstraints` y **no** se puntúa |
| 4. Features | Backend | Las 10 features en el orden de `feature_order`, con sus fórmulas, y validación del vector |
| 5. Inferencia | **ONNX** | Devuelve `factor_points` y `raw_total` sin redondear |
| 6. Redondeo | Backend | round6 → 1 decimal por factor; `matchScore` entero; `AwayFromZero`; `-0` → `0` |
| 7. JSON | Backend | `modelVersion`, `featureSchemaVersion`, `matchScore`, `breakdown` |
| 8. Ranking | Backend | `raw_total` descendente; empates por `room_id` ascendente |

`TheHouse-GenAI` **explica** el puntaje y su desglose; nunca lo calcula ni lo modifica. Este repositorio no llama a ningún LLM.

## 3. Descargar el release y verificar el SHA-256

### 3.1 Archivos del release

El release [`model-v1.0.0`](https://github.com/thehouse-team/TheHouse-ML/releases/tag/model-v1.0.0) tiene 7 archivos (spec §6.6):

| Archivo | Para qué lo usa el backend |
| --- | --- |
| `model.onnx` | El modelo. Lo carga `Microsoft.ML.OnnxRuntime` |
| `feature_schema.json` | Contrato de entrada 2.0.0: catálogos, rangos, reglas para faltantes, fórmulas, HC01–HC03 y `feature_order` |
| `golden_cases.json` | 109 casos dorados de `logreg-1.0.0` para las pruebas del backend (§11) |
| `baseline_golden_cases.json` | 81 casos dorados de `baseline-1.0.0`, el modelo de respaldo por reglas que NV-23 implementa en C# (§11.4) |
| `model_card.md` | Ficha del modelo; la sección 11 describe el artefacto ONNX |
| `metrics.json` | Métricas **sintéticas** de ML-05 (informativo) |
| `SHA256SUMS` | SHA-256 de los 6 archivos anteriores, en formato `sha256sum` |

Valores de `SHA256SUMS` del release (los mismos del paquete generado desde el commit `59f07bc`):

```text
36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5  model.onnx
67af2e487e2b34c6bc5943e0eca4c209e9812a165f30698246c2f2090ffaa8bb  feature_schema.json
38a589205b29801761b0ab89ca17c82f53f0152c3035043d468794d4849da48a  golden_cases.json
a92852c7dd289d1040e4f8e5d0b134d23eab8f3a7798c04439b787f9f85ae3f2  baseline_golden_cases.json
16b199f2eafe1cdd5ce3912565f3ebbd0e8767dde9983a9843f38549dc447b87  model_card.md
50c1a0c69fbe9aa662b79c5428ed9acc9d07b0708b9e794ee0668d91574bd54a  metrics.json
```

### 3.2 Descargar por tag y verificar

Descarga siempre **por tag** (`model-v1.0.0`), nunca «el último release». Ejemplo en una terminal con `curl` y `sha256sum` (Git Bash, Linux o el runner del CI):

```bash
TAG=model-v1.0.0
BASE=https://github.com/thehouse-team/TheHouse-ML/releases/download/$TAG
mkdir -p ml-model && cd ml-model
for f in model.onnx feature_schema.json golden_cases.json baseline_golden_cases.json model_card.md metrics.json SHA256SUMS; do
  curl -fsSL -o "$f" "$BASE/$f"
done
sha256sum -c SHA256SUMS   # debe imprimir 6 líneas "OK"
```

En PowerShell, `Get-FileHash -Algorithm SHA256 model.onnx` da el hash en mayúsculas; compáralo sin distinguir mayúsculas.

### 3.3 Fijar el SHA-256 de `model.onnx` en el backend

`SHA256SUMS` viene del mismo lugar que el modelo: si alguien reemplaza los dos, la comprobación anterior pasa. Por eso el backend **fija en su propio repositorio** el SHA-256 esperado de `model.onnx` (constitución §14.3, spec §6.6) y **rechaza** el modelo si no coincide:

| Dato fijado en el backend | Valor |
| --- | --- |
| Tag | `model-v1.0.0` |
| SHA-256 de `model.onnx` | `36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5` |
| `modelVersion` esperado | `logreg-1.0.0` |
| `featureSchemaVersion` esperado | `2.0.0` |

```csharp
// REFERENCIA — no se compila en TheHouse-ML.
using System.Security.Cryptography;

public static class ModelIntegrity
{
    // Valor fijado en el repositorio del backend (release model-v1.0.0).
    public const string ExpectedModelSha256 =
        "36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5";

    public static void EnsureExpectedHash(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        using var sha = SHA256.Create();
        string actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        if (actual != ExpectedModelSha256)
        {
            // No cargar el modelo. Fallar al arrancar (o en el build) con un mensaje claro.
            throw new InvalidOperationException(
                $"model.onnx con SHA-256 inesperado: {actual}. Se esperaba {ExpectedModelSha256}.");
        }
    }
}
```

Recomendación: verificar el hash **en el build o en el CI** (al descargar) **y** al arrancar la aplicación (antes de crear la sesión).

## 4. Cargar el modelo una sola vez

Crear una `InferenceSession` lee y optimiza el grafo; hacerlo en cada petición es lento. La sesión se crea **una vez** al arrancar (por ejemplo, registrada como *singleton*) y se reutiliza; `InferenceSession.Run` admite llamadas concurrentes sobre la misma sesión.

Al cargar, comprueba también los metadatos (`metadata_props`, que en .NET se leen con `ModelMetadata.CustomMetadataMap`) para no confundir el modelo oficial con otro:

| Clave | Valor en `model-v1.0.0` | Comprobación al arrancar |
| --- | --- | --- |
| `modelVersion` | `logreg-1.0.0` | Igual al esperado |
| `featureSchemaVersion` | `2.0.0` | Igual al esperado |
| `profile` | `full` | Debe ser `full` (un modelo `ci` no es oficial) |
| `synthetic` | `true` | Informativo |
| `featureOrder` | `["distance_km","effective_distance_km","price_to_budget","services_coverage","place_type_match","property_type_match","pets_compatible","distance_ratio","price_below_min","extra_services_ratio"]` | Igual al orden que usa el backend |
| `factorOrder` | `["budget","distance","essential_services","place_type","property_type","extras"]` | Igual al orden del `breakdown` |
| `maxPointsRaw` | `[9.569811954389692,72.60747294686455,6.733606109723173,4.8542379171078585,1.7248277730510626,4.510043298863674]` | Se usa para `maxPoints` (§8.3) |
| `roundingRule` | JSON con la regla de §8 | Informativo |
| `logregSha256` | `341ea3ec705c2f877b36d1b931eafe2dea4630523560fe8764ce59e0a70b27fa` | Informativo (trazabilidad) |
| `gitCommit` | `59f07bcfe1fa6cf4a0f183b568c56913980175ee` | Informativo; no debe terminar en `-dirty` |

Formato técnico: opset 17, IR 8, productor `thehouse-ml` 0.1.0. Cualquier `Microsoft.ML.OnnxRuntime` que soporte opset 17 lo carga (onnxruntime ≥ 1.13, M-ML-045); en Python se validó con onnxruntime 1.31.1. La versión del paquete NuGet la elige el backend.

```csharp
// REFERENCIA — no se compila en TheHouse-ML.
using Microsoft.ML.OnnxRuntime;
using System.Text.Json;

public sealed class OnnxCompatibilityModel : IDisposable   // implementaría ICompatibilityModel
{
    private readonly InferenceSession _session;
    public double[] MaxPointsRaw { get; }

    public OnnxCompatibilityModel(string modelPath)
    {
        ModelIntegrity.EnsureExpectedHash(modelPath);          // §3.3: primero el hash
        _session = new InferenceSession(modelPath);            // una sola vez

        var meta = _session.ModelMetadata.CustomMetadataMap;
        Require(meta, "modelVersion", "logreg-1.0.0");
        Require(meta, "featureSchemaVersion", "2.0.0");
        Require(meta, "profile", "full");
        MaxPointsRaw = JsonSerializer.Deserialize<double[]>(meta["maxPointsRaw"])!;
    }

    private static void Require(IDictionary<string, string> meta, string key, string expected)
    {
        if (!meta.TryGetValue(key, out var value) || value != expected)
            throw new InvalidOperationException($"Metadato {key} inesperado: {value ?? "(falta)"}");
    }

    public void Dispose() => _session.Dispose();
}
```

## 5. Pasos 1–3: faltantes, perfil incompleto y restricciones duras

Todo sale de `feature_schema.json` 2.0.0 (secciones `student`, `room` y `hard_constraints`).

### 5.1 Reglas para campos nulos del estudiante

| Campo | Si es nulo |
| --- | --- |
| `budget.min` | `0` (sin piso de presupuesto) |
| `budget.max` | **Perfil incompleto** (no se puntúa) |
| `maxDistanceKm` | `30` |
| `essentialServices` | Conjunto vacío |
| `livingWith` | **Perfil incompleto** |
| `placeType` | **Perfil incompleto** |
| `pets` | `false` |
| `transport` | `"public"` |
| `propertyTypes` | Nulo o vacío = acepta todos los tipos |
| `university_code` | No admite nulo |

Los campos del cuarto no admiten nulos. `price_mxn` es entero: el esquema indica que se redondea a entero desde `Property.Price` (`decimal`), pero **no fija el modo de redondeo** (ver §17).

### 5.2 Perfil incompleto

Si falta `budget.max`, `livingWith` o `placeType`, la respuesta es `incomplete = true` con **todos** los que faltan en el orden `budget.max`, `livingWith`, `placeType`. Se detecta **antes** que las restricciones y no se calculan features (M-ML-033 decisión 7).

```json
{"modelVersion": "logreg-1.0.0", "featureSchemaVersion": "2.0.0", "incomplete": true, "missingFields": ["budget.max"]}
```

### 5.3 Restricciones duras HC01–HC03

| ID | Excluye cuando |
| --- | --- |
| `HC01_budget` | `room.price_mxn > student.budget.max` |
| `HC02_living_with` | `student.livingWith != "any" and room.livingWith != student.livingWith` |
| `HC03_pets` | `student.pets = true and room.pets_allowed = false` |

Un par que viola alguna responde `excluded = true` con `violatedConstraints` en ese orden y **no pasa por el ONNX** (constitución §11.1). El modelo no sabe excluir: si se le manda un par excluido, devuelve un puntaje sin sentido.

```json
{"modelVersion": "logreg-1.0.0", "featureSchemaVersion": "2.0.0", "excluded": true, "violatedConstraints": ["HC01_budget"]}
```

## 6. Paso 4: calcular las 10 features

### 6.1 Orden y fórmulas

La entrada del ONNX es una fila de 10 `double` **en este orden exacto** (`feature_order` del esquema y metadato `featureOrder`). Un orden distinto no da error: da puntajes equivocados.

| # | Feature | Fórmula (resumen de `feature_schema.json`) | `range` | `training_range` |
| --- | --- | --- | --- | --- |
| 0 | `distance_km` | Haversine entre el campus de `student.university_code` y `(room.lat, room.lon)`, radio 6371 km | [0, sin máximo] | [0, 30] |
| 1 | `effective_distance_km` | `distance_km × factor(transport)`: `walk` 1.0, `bike` 0.5, `public` 0.4, `car` 0.25 | [0, sin máximo] | [0, 30] |
| 2 | `price_to_budget` | `room.price_mxn / max(budget.max, 1)` | [0, sin máximo] | [0, 1] |
| 3 | `services_coverage` | `|essentialServices ∩ room.services| / |essentialServices|`; 1 si `essentialServices` está vacío | [0, 1] | — |
| 4 | `place_type_match` | 1 si `student.placeType = room.placeType`; si no, 0 | {0, 1} | — |
| 5 | `property_type_match` | 1 si `propertyTypes` es nulo o vacío, o si `room.propertyType ∈ propertyTypes`; si no, 0 | {0, 1} | — |
| 6 | `pets_compatible` | 1 si `pets = false` o `room.pets_allowed = true`; si no, 0 (vale 1 en todo par que pasa HC03) | {0, 1} | — |
| 7 | `distance_ratio` | `distance_km / maxDistanceKm` resuelto (usa la distancia real, no la efectiva) | [0, sin máximo] | [0, 60] |
| 8 | `price_below_min` | `(budget.min − price_mxn) / budget.min` si `budget.min > 0` y `price_mxn < budget.min`; si no, 0 | [0, 1] | — |
| 9 | `extra_services_ratio` | `|room.services − essentialServices| / (11 − |essentialServices|)`; 0 si pidió los 11 servicios | [0, 1] | — |

Notas:

- **Catálogo de servicios:** 11 valores (`wifi`, `water`, `electricity`, `gas`, `hot_water`, `laundry`, `kitchen`, `furnished`, `parking`, `security`, `cleaning`).
- **No recortes las features.** Manda los valores tal como salen de las fórmulas (por ejemplo, `distance_km` = 35). El ONNX ya recorta cada columna a su `training_range` antes de puntuar. El `value` del JSON también va **sin recortar** (§9).
- **Haversine de referencia** (Python, `thehouse_ml.universities.haversine_m`): `a = sin²(Δφ/2) + cos φ₁ · cos φ₂ · sin²(Δλ/2)`, `a` acotado a [0, 1], distancia = `2 · R · asin(√a)` con `R` = 6,371,000 m, y luego `/ 1000` para km.
- **Coordenadas del campus:** `final_lat` y `final_lon` de `data/universities.csv` (ML-02). Las mismas coordenadas vienen en `metadata.campuses` de `golden_cases.json`. `TESJI`, `UMB-VDC` y `UCAN` están marcadas `seed_sin_verificar`: sus distancias son orientativas.

```csharp
// REFERENCIA — no se compila en TheHouse-ML.
static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
{
    const double EarthRadiusM = 6_371_000.0;
    double ToRad(double deg) => deg * Math.PI / 180.0;
    double phi1 = ToRad(lat1), phi2 = ToRad(lat2);
    double dPhi = phi2 - phi1, dLambda = ToRad(lon2 - lon1);
    double a = Math.Pow(Math.Sin(dPhi / 2), 2)
             + Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin(dLambda / 2), 2);
    a = Math.Min(1.0, Math.Max(0.0, a));
    return 2.0 * EarthRadiusM * Math.Asin(Math.Sqrt(a)) / 1000.0;
}
```

### 6.2 Validar el vector antes del ONNX

Antes de llamar al modelo (M-ML-031 decisión 4), cada feature debe ser un número **finito** (sin `NaN` ni infinito), estar dentro de su `range` (no su `training_range`) y ser 0 o 1 en las features enteras (`place_type_match`, `property_type_match`, `pets_compatible`). Un vector inválido es un error del backend, no algo que el modelo deba «arreglar».

## 7. Paso 5: inferencia con ONNX

### 7.1 Contrato

| Nombre | Dirección | Tipo | Forma | Contenido |
| --- | --- | --- | --- | --- |
| `features` | Entrada | `tensor(double)` | `[N, 10]` | Una fila por cuarto candidato, en el orden de §6.1 |
| `factor_points` | Salida | `tensor(double)` | `[N, 6]` | Puntos **sin redondear** por factor, en el orden `budget`, `distance`, `essential_services`, `place_type`, `property_type`, `extras` |
| `raw_total` | Salida | `tensor(double)` | `[N]` | Suma **sin redondear** de los 6 factores (de izquierda a derecha) |

- Todo es `double` (64 bits). **No uses `float`**: la conversión a 32 bits cambia redondeos (riesgo RK01 de la spec).
- `N` es libre: puntúa los 200 cuartos de un estudiante en una sola llamada `[200, 10]`.
- El ONNX no devuelve logit, probabilidad ni término fijo: no forman parte del contrato.
- Puede devolver valores negativos diminutos (del orden de −1e-16) en un factor que está en su mínimo; se resuelven en el redondeo (§8.2).

```csharp
// REFERENCIA — no se compila en TheHouse-ML.
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public (double[,] FactorPoints, double[] RawTotal) Score(double[][] featureRows)
{
    int n = featureRows.Length;
    var data = new double[n * 10];
    for (int i = 0; i < n; i++)
        Array.Copy(featureRows[i], 0, data, i * 10, 10);   // cada fila ya en el orden de feature_order

    var input = new DenseTensor<double>(data, new[] { n, 10 });
    var inputs = new[] { NamedOnnxValue.CreateFromTensor("features", input) };

    using var results = _session.Run(inputs);                // _session creada una sola vez (§4)
    var points = results.First(r => r.Name == "factor_points").AsTensor<double>();
    var total = results.First(r => r.Name == "raw_total").AsTensor<double>();

    var factorPoints = new double[n, 6];
    var rawTotal = new double[n];
    for (int i = 0; i < n; i++)
    {
        for (int k = 0; k < 6; k++) factorPoints[i, k] = points[i, k];
        rawTotal[i] = total[i];
    }
    return (factorPoints, rawTotal);
}
```

## 8. Paso 6: redondeo oficial

Regla de M-ML-029/031, fijada para `logreg-1.0.0` en M-ML-041 y con la normalización de `-0` de M-ML-046. Es la misma que trae el metadato `roundingRule` y `metadata.rounding` de `golden_cases.json`.

### 8.1 Las tres fórmulas

| Valor | Regla |
| --- | --- |
| `round6(x)` | Redondear `x` a 6 decimales, mitad hacia arriba (`AwayFromZero`) |
| `points` de cada factor | `round6(factor_points[k])` y después 1 decimal, mitad hacia arriba |
| `matchScore` | `floor(round6(raw_total) + 0.5)` = `Math.Round(round6(raw_total), MidpointRounding.AwayFromZero)` (son iguales porque `raw_total` ≥ 0) |
| `maxPoints` de cada factor | `maxPointsRaw[k]` con la misma regla que `points` |

Por qué round6 primero: con `double`, un valor que «es» 4.65 puede llegar como 4.64999994; round6 lo lleva a 4.65 y después sube a 4.7. Sin round6 daría 4.6.

**Nunca uses el `Math.Round` por omisión** (`MidpointRounding.ToEven`, «mitad al par»). Ejemplos de los casos dorados que fallan con `ToEven`:

| Caso | Sin redondear | Correcto (`AwayFromZero`) | Con `ToEven` |
| --- | --- | --- | --- |
| `factor_round_budget_4_65` | presupuesto 4.64999994 → round6 4.65 | **4.7** | 4.6 |
| `logreg_total_x5000004_up` | total 80.5000004 → round6 80.5 | **81** | 80 |

### 8.2 Normalizar `-0` a `0`

`Math.Round(-2.45e-16, 6, MidpointRounding.AwayFromZero)` da `-0.0`, y `System.Text.Json` lo escribe como `-0`. Después de redondear, convierte `-0` en `0` en los `points` y en `matchScore` (M-ML-046). En C#, `x == 0` es verdadero también para `-0.0`, así que basta con devolver la constante `0.0`. El caso dorado `factor_minimum_place_type` lo prueba.

```csharp
// REFERENCIA — no se compila en TheHouse-ML.
public static class ScoreRounding
{
    private const MidpointRounding Mode = MidpointRounding.AwayFromZero;   // nunca ToEven

    public static double Round6(double x) => Math.Round(x, 6, Mode);

    public static double FactorPoints(double rawPoints)
    {
        double r = Math.Round(Round6(rawPoints), 1, Mode);
        return r == 0.0 ? 0.0 : r;                     // -0 → 0
    }

    public static int MatchScore(double rawTotal)
    {
        double r = Math.Round(Round6(rawTotal), 0, Mode); // = floor(round6(raw_total) + 0.5) con raw_total ≥ 0
        return r == 0.0 ? 0 : (int)r;
    }

    public static double MaxPoints(double maxPointsRaw) => FactorPoints(maxPointsRaw);
}
```

### 8.3 `maxPoints` de `logreg-1.0.0`

Con `maxPointsRaw` del metadato y la regla de §8.1:

| Factor | `maxPointsRaw` (metadato) | `maxPoints` |
| --- | --- | --- |
| `budget` | 9.569811954389692 | 9.6 |
| `distance` | 72.60747294686455 | 72.6 |
| `essential_services` | 6.733606109723173 | 6.7 |
| `place_type` | 4.8542379171078585 | 4.9 |
| `property_type` | 1.7248277730510626 | 1.7 |
| `extras` | 4.510043298863674 | 4.5 |

Σ `maxPointsRaw` = 100 y Σ `maxPoints` = 100.0. Los `maxPoints` son **variables por versión del modelo**: léelos del metadato, no los escribas a mano. (En `golden_cases.json` algunos `maxPointsRaw` difieren en el último dígito, p. ej. 6.733606109723172, porque el recuento independiente usa aritmética exacta; el redondeo da los mismos `maxPoints`.)

Como `matchScore` y cada `points` se redondean por separado desde los valores sin redondear, la suma de los `points` mostrados no tiene por qué coincidir con `matchScore`. Cotas de M-ML-041 (30,755 pares de test, sintéticos): \|Σ `points` − `raw_total`\| ≤ 0.3 y \|`matchScore` − `raw_total`\| ≤ 0.5. Es esperado: no «corrijas» ningún factor para que cuadre.

## 9. Paso 7: JSON de respuesta

Este es el formato que consume `TheHouse-GenAI` (M-ML-041 decisión 4, spec §6.1 paso 7). Ejemplo real: caso dorado `perfect_100` de `golden_cases.json`.

```json
{
  "modelVersion": "logreg-1.0.0",
  "featureSchemaVersion": "2.0.0",
  "matchScore": 93,
  "breakdown": [
    {"factor": "budget", "points": 2.5, "maxPoints": 9.6,
     "features": [{"feature": "price_to_budget", "value": 0.75}, {"feature": "price_below_min", "value": 0}]},
    {"factor": "distance", "points": 72.6, "maxPoints": 72.6,
     "features": [{"feature": "distance_km", "value": 0}, {"feature": "effective_distance_km", "value": 0}, {"feature": "distance_ratio", "value": 0}]},
    {"factor": "essential_services", "points": 6.7, "maxPoints": 6.7, "feature": "services_coverage", "value": 1},
    {"factor": "place_type", "points": 4.9, "maxPoints": 4.9, "feature": "place_type_match", "value": 1},
    {"factor": "property_type", "points": 1.7, "maxPoints": 1.7, "feature": "property_type_match", "value": 1},
    {"factor": "extras", "points": 4.5, "maxPoints": 4.5, "feature": "extra_services_ratio", "value": 1}
  ]
}
```

Reglas del formato:

- `matchScore`: entero de 0 a 100.
- `breakdown`: siempre los 6 factores, en el orden de `factorOrder`.
- `budget` y `distance` llevan una lista `features` (`{feature, value}`) en el orden de la tabla; los otros 4 factores llevan `feature` y `value` directos.
- `value` es la feature **sin recortar** (el puntaje usa el valor recortado; el JSON muestra el real). Ejemplo: un cuarto a 35 km muestra `distance_km` 35 aunque puntúe como 30.
- `points` y `maxPoints` con 1 decimal (§8).
- Pares excluidos o perfil incompleto: §5.2 y §5.3 (sin `matchScore` ni `breakdown`).

| Factor | Features (en orden) |
| --- | --- |
| `budget` | `price_to_budget`, `price_below_min` |
| `distance` | `distance_km`, `effective_distance_km`, `distance_ratio` |
| `essential_services` | `services_coverage` |
| `place_type` | `place_type_match` |
| `property_type` | `property_type_match` |
| `extras` | `extra_services_ratio` |

`pets_compatible` no pertenece a ningún factor (su aporte es constante y va al término fijo): **no aparece** en el `breakdown`.

## 10. Paso 8: ranking

Ordena los cuartos de un estudiante por **`raw_total` sin redondear, de mayor a menor**; los empates se rompen por `room_id` ascendente (M-ML-031 decisión 7). No ordenes por `matchScore`: dos cuartos con 87 pueden tener `raw_total` distintos.

## 11. Casos dorados como pruebas automáticas en .NET

Los casos dorados son la prueba de que el backend calcula **lo mismo** que Python. Deben correr en el CI del backend (constitución §13.4).

### 11.1 Qué trae `golden_cases.json` (`logreg-1.0.0`)

109 casos generados con un recuento independiente en aritmética exacta (M-ML-047):

| Tipo de caso | Cantidad | Qué prueban |
| --- | --- | --- |
| Entradas crudas puntuadas (`input.student` + `input.room`) | 89 | Features + ONNX + redondeo + JSON |
| Solo vector (`input.features`) | 9 | Solo la inferencia (recorte incluido); algunos están fuera del `range` y el backend real los rechazaría antes del ONNX |
| Excluidos (HC01–HC03) | 6 | `excluded` y `violatedConstraints` |
| Perfil incompleto | 5 | `incomplete` y `missingFields` |

Categorías: `baseline_inputs` 81, `clipping` 8, `extremes` 3, `factor_minimum` 2, `factor_rounding` 5, `total_rounding` 3, `t05_h1_zone` 4, `fixed_term` 3.

Campos de cada caso: `id`, `category`, `description`, `input`, `tolerance` y `expected` con `features` (10, sin recortar; `null` si el perfil está incompleto), `clippedFeatures` (informativo), `onnx` (`factor_points` [6] y `raw_total`; solo en pares puntuados) y `result` (el JSON de §9, o `excluded`/`incomplete`).

### 11.2 Tolerancias

| Qué se compara | Tolerancia |
| --- | --- |
| `expected.features` y `breakdown[].value` | 1e-6 (absoluta) |
| `expected.onnx` (`factor_points`, `raw_total` sin redondear) | 1e-4 |
| `points`, `maxPoints`, `matchScore` | **0** (exacto) |
| `factor`, `feature`, `excluded`, `violatedConstraints`, `incomplete`, `missingFields` | Exacto |

En Python, la diferencia máxima observada entre onnxruntime y el esperado fue 2.8e-14, con 0 diferencias en puntos redondeados y en `matchScore` (M-ML-049).

### 11.3 Cómo correrlos

Para cada caso:

1. Si tiene `input.features`: arma el vector en el orden de `featureOrder` y ve al paso 4.
2. Si tiene `input.student`/`input.room`: corre el flujo real del backend (faltantes → incompleto → restricciones → features), usando las coordenadas de campus de `metadata.campuses`.
3. Si `expected.result.incomplete` o `expected.result.excluded`: compara esos campos y termina. En los excluidos, compara también las features con `expected.features`.
4. Corre el ONNX y compara con `expected.onnx` (1e-4).
5. Redondea (§8), arma el JSON (§9) y compara: `matchScore` y `points` exactos; `value` con 1e-6.

Las entradas crudas siguen los nombres de `feature_schema.json` (por ejemplo `budget.max` con punto en el nombre); `null` significa campo nulo.

```csharp
// REFERENCIA — no se compila en TheHouse-ML. xUnit (Apache-2.0) o NUnit (MIT), ambos gratuitos.
using System.Text.Json;
using Xunit;

public class LogregGoldenCasesTests
{
    private static readonly OnnxCompatibilityModel Model = new("ml-model/model.onnx"); // una sola sesión

    public static IEnumerable<object[]> Cases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText("ml-model/golden_cases.json"));
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            yield return new object[] { c.GetProperty("id").GetString()!, c.GetRawText() };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_expected_result(string id, string caseJson)
    {
        using var doc = JsonDocument.Parse(caseJson);
        var expected = doc.RootElement.GetProperty("expected");
        var result = expected.GetProperty("result");

        // Pasos 1–4 con el código real del backend (nombres ilustrativos).
        var outcome = BackendScoringPipeline.Evaluate(doc.RootElement.GetProperty("input"));

        if (result.TryGetProperty("incomplete", out _)) { /* comparar missingFields exacto */ return; }
        if (result.TryGetProperty("excluded", out _))   { /* comparar violatedConstraints exacto */ return; }

        var (points, rawTotal) = Model.Score(new[] { outcome.Features });

        var onnx = expected.GetProperty("onnx");
        Assert.InRange(rawTotal[0] - onnx.GetProperty("raw_total").GetDouble(), -1e-4, 1e-4);

        Assert.Equal(result.GetProperty("matchScore").GetInt32(), ScoreRounding.MatchScore(rawTotal[0]));
        var breakdown = result.GetProperty("breakdown").EnumerateArray().ToArray();
        for (int k = 0; k < 6; k++)
            Assert.Equal(breakdown[k].GetProperty("points").GetDouble(), ScoreRounding.FactorPoints(points[0, k]));
    }
}
```

**Criterio de aceptación en el backend:** los 109 casos en verde, con diferencia **0** en `matchScore` y en `points`. Un solo caso en rojo bloquea el despliegue del modelo.

### 11.4 `baseline_golden_cases.json` (modelo de respaldo)

`baseline-1.0.0` es el modelo por reglas de ML-04. **No** se exporta a ONNX: NV-23 lo implementa en C# como respaldo (spec §5) y lo valida con estos 81 casos (70 puntuados, 6 excluidos, 5 incompletos):

- Mismas entradas crudas, mismas features, mismos HC01–HC03 y mismo perfil incompleto que el modelo ONNX.
- `modelVersion` `baseline-1.0.0`; `maxPoints` fijos 30 / 25 / 20 / 10 / 10 / 5; un `feature`/`value` por factor (sin listas `features`).
- Mismo redondeo de §8 (round6 y `AwayFromZero`). Tolerancias: `features`, `value` y `rawTotal` 1e-6; `points`, `maxPoints` y `matchScore` exactos.
- Los pesos y curvas están en `baseline_params.json`, que **no** va en el release `model-v1.0.0` (el release no se modifica). El backend lo obtiene del repositorio **fijado al commit del release** y verifica su SHA-256, igual que con `model.onnx`:

| Dato fijado en el backend | Valor |
| --- | --- |
| URL (raw, fijada al commit) | `https://raw.githubusercontent.com/thehouse-team/TheHouse-ML/59f07bcfe1fa6cf4a0f183b568c56913980175ee/src/thehouse_ml/models/baseline_params.json` |
| Commit | `59f07bcfe1fa6cf4a0f183b568c56913980175ee` |
| SHA-256 de `baseline_params.json` en ese commit | `51b663e840eb0b5d12592802093050e28091bf91279db6aece7c237dd5737111` |

```bash
curl -fsSL -o baseline_params.json \
  https://raw.githubusercontent.com/thehouse-team/TheHouse-ML/59f07bcfe1fa6cf4a0f183b568c56913980175ee/src/thehouse_ml/models/baseline_params.json
echo "51b663e840eb0b5d12592802093050e28091bf91279db6aece7c237dd5737111  baseline_params.json" | sha256sum -c -
```

Nunca uses una URL con `main` (cambia con el tiempo). Si en el futuro `baseline_params.json` debe ir dentro del paquete, será en una versión nueva del release (`model-v1.0.1`).
- El campo `metadata.dotnet` de `baseline_golden_cases.json` explica dos trampas de `double` (`total_x5_binary64_noise` y `budget_13_35`).

**Criterio:** los 81 casos en verde, diferencia 0 en `matchScore` y `points`.

## 12. Desviación conocida ML-05 T05-H1

> No confundir con el hallazgo T05-H1 de ML-06: esta es la desviación **ML-05 T05-H1** (M-ML-041).

- **Qué pasa:** el coeficiente de `price_below_min` salió negativo. Cuando la renta está **por debajo** de `budget.min`, el factor `budget` **sube** con la renta (debería bajar) si `budget.max / budget.min` > **76.2**.
- **Magnitud:** subida máxima **+0.124 puntos** sobre 100 (caso extremo `budget.min` 1, `budget.max` 50,000). Ningún par sintético observado cae en esa zona (el mayor cociente observado es 2.83).
- **Qué hace el backend:** **nada especial.** No la corrijas ni la compenses: el backend debe reproducir el modelo tal como es, y los casos dorados `t05_h1_zone` (`t05_h1_rent_0`, `t05_h1_rent_100`, `t05_h1_rent_199`, `t05_h1_rent_200`) esperan exactamente ese comportamiento.
- **Corrección:** `logreg-1.1.0`, con datos nuevos de ML-07 (release nuevo; §14).

## 13. Rendimiento esperado

| Dato | Valor |
| --- | --- |
| Criterio del contrato (constitución §13.5) | 200 cuartos en **< 20 ms** de mediana, CPU |
| Medido en Python (onnxruntime 1.31.1, CPU, sesión ya cargada, lote `[200, 10]`) | Mediana 0.0477 ms (T04) y 0.0914 / 0.0673 ms (T05); la model card lo reporta como cota **< 1 ms** |
| Máquina de la medición | i7-13620H (16 CPUs lógicas), Windows 11 |
| Medido en .NET | **No medido** en este repositorio |

Qué esperar en .NET: el mismo orden de magnitud (submilisegundo por lote de 200), **siempre que** la sesión se reutilice (§4) y se mande el lote completo en una llamada. Crear la sesión en cada petición o llamar al modelo cuarto por cuarto es lo que más degrada la latencia. El cálculo de features (haversine y conjuntos) suele costar más que la inferencia.

Recomendación: medir en el backend con el mismo método (10 corridas de calentamiento, 100 medidas, mediana) y alertar si la mediana se acerca a 20 ms.

## 14. Versiones nuevas del modelo

- Un release publicado **no se modifica**; cualquier corrección es una versión nueva con tag nuevo (`model-vX.Y.Z`, constitución §13.3).
- Para adoptar una versión nueva:
  1. Descargar el nuevo tag y verificar `SHA256SUMS` (§3.2).
  2. Actualizar en el backend el SHA-256 fijado de `model.onnx`, el tag y el `modelVersion` esperado (§3.3).
  3. Leer `featureSchemaVersion` en el metadato. Si cambia la versión **mayor** (p. ej. 2.x → 3.0.0), cambian las features o su orden: requiere spec en ambos repositorios y cambios de código en el backend antes de usar el modelo.
  4. Reemplazar `golden_cases.json` por el del release nuevo y correr las pruebas (§11): 0 diferencias.
  5. Revisar la `model_card.md` (cambios de `maxPoints`, desviaciones conocidas) y avisar a `TheHouse-GenAI` si cambian los `maxPoints`.
- Nunca mezclar archivos de dos releases distintos (por ejemplo, `model.onnx` de una versión con `golden_cases.json` de otra).

## 15. Nota para `TheHouse-GenAI`

Resumen de la sección 7 de `model_card.md` (M-ML-041, T05-H2):

1. Explicar cada factor como «obtuvo `points` de `maxPoints` posibles». **`maxPoints` no es el peso ni la importancia del factor**: decir «la distancia vale el 72.6 % de la decisión» es incorrecto. `maxPoints` mide cuánto puede mover cada factor en todo su `training_range`; la importancia se midió con la ablación (sección 6 de la model card).
2. Para comparar cuartos del mismo estudiante, usar la **diferencia de `points`**.
3. Un `matchScore` alto es sobre todo «cuarto cercano»; mencionar los factores con proporción baja.
4. `value` es la entrada sin recortar; el puntaje usa el valor recortado.
5. No recalcular ni modificar `matchScore` ni `points`; no mencionar logit ni probabilidad.
6. Todo es **sintético**: no afirmar que el puntaje refleja preferencias reales.

## 16. Lista de verificación final

Descarga e integridad:

- [ ] El build descarga los 7 archivos **por tag** `model-v1.0.0`.
- [ ] `sha256sum -c SHA256SUMS` da 6 × `OK` en el CI.
- [ ] El SHA-256 de `model.onnx` está fijado en el repositorio del backend (`36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5`) y la aplicación **rechaza** el modelo si no coincide.

Carga:

- [ ] La `InferenceSession` se crea una sola vez y se reutiliza.
- [ ] Al arrancar se comprueban `modelVersion` = `logreg-1.0.0`, `featureSchemaVersion` = `2.0.0` y `profile` = `full`.
- [ ] `maxPoints` se calcula desde `maxPointsRaw` del metadato.

Flujo:

- [ ] Faltantes resueltos con las reglas del esquema 2.0.0.
- [ ] Perfil incompleto detectado **antes** de las restricciones, con `missingFields` en orden.
- [ ] HC01–HC03 aplicadas; los pares excluidos **no** llegan al ONNX.
- [ ] 10 features en el orden exacto de `feature_order`, en `double`, sin recortar y validadas (finitas, dentro de `range`, enteras donde aplica).
- [ ] Entrada `features` `[N, 10]`; salidas `factor_points` `[N, 6]` y `raw_total` `[N]`.

Redondeo y JSON:

- [ ] round6 antes de redondear; `MidpointRounding.AwayFromZero` en todos los `Math.Round` (ninguno con el modo por omisión).
- [ ] `-0` normalizado a `0` en `points` y `matchScore`.
- [ ] JSON con `modelVersion`, `featureSchemaVersion`, `matchScore` y `breakdown` (6 factores en orden; listas `features` en `budget` y `distance`; `value` sin recortar).
- [ ] Ranking por `raw_total` descendente y `room_id` ascendente.

Pruebas:

- [ ] `golden_cases.json`: 109 casos en verde, 0 diferencias en `matchScore` y `points`.
- [ ] `baseline_params.json` descargado del commit `59f07bc` con SHA-256 `51b663e840eb0b5d12592802093050e28091bf91279db6aece7c237dd5737111` verificado.
- [ ] `baseline_golden_cases.json`: 81 casos en verde con el respaldo en C#.
- [ ] Los casos `t05_h1_zone` pasan **sin** corregir la desviación ML-05 T05-H1.
- [ ] Mediana de inferencia para 200 cuartos medida en el backend y < 20 ms.

## 17. Lo que esta guía no define

Puntos que no están fijados en los archivos de `TheHouse-ML` y que el backend debe resolver en su propia spec (NV-23) con el responsable:

1. **Modo de redondeo de `Property.Price` a `price_mxn`:** el esquema dice que se redondea a entero, pero no con qué regla. Se define en la **spec de NV-23 del backend** (M-ML-050). Los casos dorados usan rentas enteras, así que no lo prueban.
2. **Rendimiento en .NET:** no se midió aquí (§13).
3. **Versión del paquete `Microsoft.ML.OnnxRuntime`:** la elige el backend; debe soportar opset 17 / IR 8.
4. **Campos del cuarto que hoy no existen en `Property`** (`lat`, `lon`, `services`, `placeType`, `propertyType`, `pets_allowed`, `livingWith`; `backend_status = missing_in_property` en el esquema): dependen de NV-12 y son necesarios para producción.
