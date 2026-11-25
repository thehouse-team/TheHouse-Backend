# Model card — `logreg-1.0.0` (ML-05) — DATOS SINTÉTICOS

> **DATOS SINTÉTICOS — ETIQUETAS SINTÉTICAS.** Métricas con etiquetas SINTÉTICAS (utilidad oculta documentada más ruido, M-ML-032): el modelo aprende la función del simulador; no son desempeño real ni preferencias reales (constitución §8.2 y §20.3) y se reemplazan con señales reales en ML-07.

Generada automáticamente (`thehouse_ml.models.model_card.render_model_card`, ML-05 1.0.0, T06 (ML-05-R06) y T07) desde los JSON de `reports/`, `baseline_params.json` y `uv.lock`; ninguna cifra está escrita a mano. La regenera `python -m thehouse_ml evaluate` (perfil `full`).

## 1. Resumen

| Campo | Valor |
| --- | --- |
| Modelo | `logreg-1.0.0` — regresión logística aditiva con puntaje 0-100 y desglose |
| Estado | **PROMOVIDO** como modelo de ML-05 el 2026-11-24 (M-ML-041) |
| Respaldo | `baseline-1.0.0` (modelo base por reglas de ML-04) |
| Esquema de features | `feature_schema.json` 2.0.0 |
| Decisión (`trained_metrics.json`) | `logreg_promoted` |
| NDCG@10 en test (DATOS SINTÉTICOS) | 0.862 [0.852, 0.873] contra 0.527 [0.503, 0.550] del baseline; diferencia pareada +0.335 [+0.312, +0.360] |
| Spec | ML-05 1.0.0 (M-ML-036) |

La regresión logística cumple las 3 condiciones: queda **PROMOVIDA** como modelo de ML-05 (`logreg-1.0.0`, puntaje 0-100 con desglose por factor); `baseline-1.0.0` queda como respaldo. Desviación conocida aceptada (M-ML-041): `monotonicity_budget` (ver `reports/trained_breakdown.md`).

## 2. Uso previsto y fuera de alcance

**Uso previsto.**

- Ordenar los cuartos candidatos de un estudiante de las **10 universidades** de la región Tula/Tepeji (`UTTT`, `UAEH-ESTE`, `UAEH-ESTL`, `UAEH-ESAT`, `ITSOEH`, `TESJI`, `UMB-JILO`, `UMB-VDC`, `UCAN`, `UNE-TULA`) y darle a cada uno un `matchScore` de 0 a 100 con su desglose por los 6 factores (constitución §1 y §12).
- Lo consume el backend dentro del monolito (NV-23, puerto `ICompatibilityModel`) después de la exportación a ONNX de ML-06; `TheHouse-GenAI` explica el puntaje y su desglose, nunca lo calcula.

**Fuera de alcance.**

- **No decide** la aceptación de un estudiante ni el precio del cuarto: solo ordena y explica.
- **No usa género ni atributos sensibles** (edad, origen) como señal de puntaje: las entradas son las 10 features de `feature_order` del esquema; el género solo interviene en la restricción dura `livingWith` que declara el propio estudiante (constitución §11.2).
- **Los pares excluidos por HC01-HC03 no reciben puntaje** (renta sobre `budget.max`, `livingWith` y mascotas; constitución §11.1): el modelo solo puntúa candidatos y devuelve el par excluido con el formato de ML-04.
- No se afirma desempeño real: todas las métricas son sobre **DATOS SINTÉTICOS** (sección 3).

## 3. Datos

> **DATOS SINTÉTICOS.** Estudiantes, cuartos, pares y etiquetas son sintéticos.

- **Estudiantes, cuartos y pares sintéticos** de ML-03, calibrados con fuentes de INEGI (ENVI 2020, ENIGH 2022 deflactadas con el INPC a 2024-07, Censo y DENUE): ver la [data card](data_card.md).
- **Etiquetas sintéticas** de relevancia 0-3 de ML-04 (`labels.parquet`, `labels_params.json` 1.0.0, M-ML-032): una utilidad oculta documentada más ruido. ML-05 no genera etiquetas y nunca lee la utilidad ni los pesos ocultos (prueba obligatoria de T01).
- Objetivo de la regresión logística: `relevance ≥ 2`.

| Partición | Pares | Estudiantes | Excluidos (< 2 candidatos) |
| --- | ---: | ---: | ---: |
| train | 135,992 | 1,399 | 1 |
| validation | 29,857 | 300 | 0 |
| test | 30,755 | 300 | 0 |
| total | 196,604 | 1,999 | 1 |

Excluidos: `S1078` (train, 1 candidato).

**Advertencia de circularidad (ML-05-RK01).** Las etiquetas salen de un simulador cuya utilidad usa las mismas features que ve el modelo: el modelo aprende esa función más el ruido. Las métricas de esta ficha **validan el pipeline** (partición, entrenamiento, comparación y desglose), **no el desempeño real**, que solo se medirá con señales reales en ML-07 (constitución §8.2 y §8.3).

## 4. Entrenamiento

- **Validación cruzada:** 5 pliegues `StratifiedGroupKFold` agrupados por estudiante y estratificados por campus sobre train + validation (1,699 estudiantes, 165,849 pares), semilla 20261124; ningún estudiante en dos pliegues. La partición test solo se usó una vez, en T03.

| Pliegue | Estudiantes | Pares |
| --- | ---: | ---: |
| P0 | 340 | 32,234 |
| P1 | 340 | 33,999 |
| P2 | 340 | 36,237 |
| P3 | 339 | 33,014 |
| P4 | 340 | 30,365 |

- **Rejilla** (`train_params.json` 1.0.0, M-ML-038 (APROBADO)), elegida por el mayor NDCG@10 medio entre pliegues (desempate: menor desviación y después la más simple):

| Configuración | NDCG@10 media | Desv. | Elegida |
| --- | ---: | ---: | --- |
| `logreg:standard:C=0.01` | 0.8623 | 0.0035 | **sí** |
| `logreg:standard:C=0.1` | 0.8623 | 0.0034 |  |
| `logreg:standard:C=1` | 0.8621 | 0.0032 |  |
| `logreg:standard:C=10` | 0.8621 | 0.0033 |  |
| `logreg:quantile_bins:C=0.01` | 0.7111 | 0.0039 |  |
| `logreg:quantile_bins:C=0.1` | 0.7093 | 0.0042 |  |
| `logreg:quantile_bins:C=1` | 0.7093 | 0.0045 |  |
| `logreg:quantile_bins:C=10` | 0.7094 | 0.0045 |  |
| `lightgbm:leaves=15:lr=0.05:trees=100` | 0.8580 | 0.0051 |  |
| `lightgbm:leaves=15:lr=0.05:trees=300` | 0.8665 | 0.0049 | **sí** |
| `lightgbm:leaves=15:lr=0.1:trees=100` | 0.8652 | 0.0062 |  |
| `lightgbm:leaves=15:lr=0.1:trees=300` | 0.8652 | 0.0053 |  |
| `lightgbm:leaves=31:lr=0.05:trees=100` | 0.8609 | 0.0045 |  |
| `lightgbm:leaves=31:lr=0.05:trees=300` | 0.8652 | 0.0051 |  |
| `lightgbm:leaves=31:lr=0.1:trees=100` | 0.8657 | 0.0045 |  |
| `lightgbm:leaves=31:lr=0.1:trees=300` | 0.8634 | 0.0049 |  |

- **Hiperparámetros elegidos:** regresión logística `logreg:standard:C=0.01`; LightGBM `lightgbm:leaves=15:lr=0.05:trees=300` con restricciones monotónicas en `price_to_budget`, `distance_km`, `effective_distance_km`, `distance_ratio` (0 violaciones en la comprobación del modelo final).
- **Modelos finales:** reentrenados en train + validation (1,699 estudiantes, 165,849 pares); guardados sin pickle (JSON y texto nativo de LightGBM, constitución §13.1).
- **Reproducibilidad:** misma semilla, `uv.lock` y datos con SHA-256 → mismos pliegues, coeficientes, modelos y métricas; dos corridas de `train` dieron SHA-256 idénticos (M-ML-038).

| Archivo | Bytes | SHA-256 |
| --- | ---: | --- |
| `models/logreg.json` | 1,853 | `341ea3ec705c2f877b36d1b931eafe2dea4630523560fe8764ce59e0a70b27fa` |
| `models/lightgbm_lambdarank.txt` | 543,515 | `5eb8d22dd771e04444383c092413c02501726d6ea1362111673b87ecd9e6086d` |

## 5. Métricas

> **DATOS SINTÉTICOS.** Métricas por estudiante de M-ML-034; ranking por puntaje descendente con desempate por `room_id`.

**Validación cruzada** (5 pliegues; media ± desviación estándar muestral):

| Modelo | Métrica | P0 | P1 | P2 | P3 | P4 | Media ± desv. |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline (`baseline-1.0.0`) | NDCG@10 | 0.5396 | 0.5470 | 0.5453 | 0.5241 | 0.5345 | 0.5381 ± 0.0093 |
| Baseline (`baseline-1.0.0`) | NDCG@5 | 0.5399 | 0.5512 | 0.5583 | 0.5185 | 0.5282 | 0.5392 ± 0.0163 |
| Baseline (`baseline-1.0.0`) | Precision@5 | 0.5765 | 0.5935 | 0.5882 | 0.5617 | 0.5576 | 0.5755 ± 0.0158 |
| Baseline (`baseline-1.0.0`) | MAP | 0.5114 | 0.5161 | 0.5095 | 0.5012 | 0.5103 | 0.5097 ± 0.0054 |
| Regresión logística | NDCG@10 | 0.8664 | 0.8570 | 0.8616 | 0.8626 | 0.8640 | 0.8623 ± 0.0035 |
| Regresión logística | NDCG@5 | 0.8683 | 0.8659 | 0.8747 | 0.8674 | 0.8648 | 0.8682 ± 0.0039 |
| Regresión logística | Precision@5 | 0.8906 | 0.8888 | 0.8900 | 0.8897 | 0.8800 | 0.8878 ± 0.0044 |
| Regresión logística | MAP | 0.8342 | 0.8244 | 0.8275 | 0.8230 | 0.8278 | 0.8274 ± 0.0043 |
| LightGBM LambdaRank | NDCG@10 | 0.8733 | 0.8597 | 0.8651 | 0.8661 | 0.8684 | 0.8665 ± 0.0049 |
| LightGBM LambdaRank | NDCG@5 | 0.8773 | 0.8675 | 0.8775 | 0.8735 | 0.8708 | 0.8733 ± 0.0043 |
| LightGBM LambdaRank | Precision@5 | 0.8959 | 0.8871 | 0.8994 | 0.8973 | 0.8865 | 0.8932 ± 0.0060 |
| LightGBM LambdaRank | MAP | 0.8408 | 0.8275 | 0.8309 | 0.8318 | 0.8333 | 0.8329 ± 0.0049 |

**Test** (medición única: 300 estudiantes, 30,755 pares; IC 95 % por bootstrap pareado por estudiante, 1,000 remuestreos, semilla 20261122):

| Modelo | NDCG@10 | NDCG@5 | Precision@5 | MAP |
| --- | ---: | ---: | ---: | ---: |
| Baseline (`baseline-1.0.0`) | 0.527 [0.503, 0.550] | 0.535 [0.507, 0.560] | 0.586 [0.557, 0.615] | 0.503 [0.485, 0.522] |
| Regresión logística | 0.862 [0.852, 0.873] | 0.874 [0.861, 0.886] | 0.916 [0.900, 0.931] | 0.831 [0.821, 0.839] |
| LightGBM LambdaRank | 0.863 [0.853, 0.872] | 0.877 [0.864, 0.890] | 0.911 [0.894, 0.927] | 0.835 [0.825, 0.844] |

**Diferencia pareada contra el baseline** (test):

| Modelo - baseline | NDCG@10 | NDCG@5 | Precision@5 | MAP |
| --- | ---: | ---: | ---: | ---: |
| Regresión logística | +0.335 [+0.312, +0.360] | +0.339 [+0.313, +0.367] | +0.330 [+0.299, +0.361] | +0.327 [+0.307, +0.347] |
| LightGBM LambdaRank | +0.336 [+0.313, +0.360] | +0.342 [+0.315, +0.370] | +0.325 [+0.295, +0.358] | +0.331 [+0.312, +0.350] |

**Criterio de promoción** (spec §6.5):

| Condición | Regresión logística | LightGBM LambdaRank |
| --- | --- | --- |
| 1. NDCG@10 mayor que el baseline en cada pliegue | **sí** (5/5) | **sí** (5/5) |
| 2. IC 95 % de Δ NDCG@10 en test con límite inferior > 0 | **sí** | **sí** |
| 3. Desglose aditivo que suma el total | **sí** (`verified_t05`) | no (`not_additive`) |
| Resultado | `promoted` | `reference_not_promoted_in_v1` |

**Casi empate entre la regresión logística y LightGBM.** LightGBM - regresión logística en NDCG@10: +0.0042 en la media de los pliegues y +0.0009 en test; sus intervalos de test se solapan (0.862 [0.852, 0.873] y 0.863 [0.853, 0.872]). No se calculó un intervalo pareado LightGBM - regresión logística (la spec compara cada modelo contra el baseline). Se eligió la regresión logística porque es la única que cumple la condición 3: ofrece un desglose aditivo por factor que suma el total y es exportable con su explicación (constitución §12); LightGBM: «LightGBM no ofrece desglose aditivo exportable (decisión 5)».

## 6. Ablación y segmentos

> **DATOS SINTÉTICOS.**

**Ablación** (importancia real de cada factor): se reentrena cada modelo sin las features del grupo, con los mismos hiperparámetros y en los 5 pliegues (nunca en test); Δ = NDCG@10 sin el grupo - completo. Ordenada de mayor a menor importancia para la regresión logística.

| Grupo | Features | Δ NDCG@10 LR (media ± desv.) | Pliegues peores LR | Δ NDCG@10 LightGBM |
| --- | --- | ---: | ---: | ---: |
| Distancia (`distance`) | `distance_km`, `effective_distance_km`, `distance_ratio` | -0.2545 ± 0.0065 | 5/5 | -0.2514 ± 0.0074 |
| Tipo de lugar (`place_type`) | `place_type_match` | -0.1118 ± 0.0036 | 5/5 | -0.1146 ± 0.0048 |
| Presupuesto (`budget`) | `price_to_budget`, `price_below_min` | -0.0840 ± 0.0090 | 5/5 | -0.0822 ± 0.0076 |
| Servicios esenciales (`essential_services`) | `services_coverage` | -0.0384 ± 0.0036 | 5/5 | -0.0384 ± 0.0040 |
| Extras (`extras`) | `extra_services_ratio` | -0.0195 ± 0.0021 | 5/5 | -0.0191 ± 0.0021 |
| Tipo de inmueble (`property_type`) | `property_type_match` | -0.0137 ± 0.0015 | 5/5 | -0.0136 ± 0.0025 |

`pets_compatible` no pertenece a ningún grupo: es constante (1) en los candidatos y se queda en todos los modelos.

**Segmentos** (constitución §11.3; de la medición única de test, NDCG@10 con IC 95 %; Δ = pareada contra el baseline):

| Segmento | Estudiantes | Baseline | Regresión logística | Δ LR | LightGBM | Δ LightGBM |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Total | 300 | 0.527 [0.503, 0.550] | 0.862 [0.852, 0.873] | +0.335 [+0.312, +0.360] | 0.863 [0.853, 0.872] | +0.336 [+0.313, +0.360] |
| UTTT | 30 | 0.515 [0.441, 0.581] | 0.837 [0.803, 0.871] | +0.322 [+0.261, +0.389] | 0.832 [0.792, 0.870] | +0.317 [+0.259, +0.380] |
| UAEH-ESTE | 30 | 0.497 [0.415, 0.584] | 0.870 [0.832, 0.904] | +0.373 [+0.287, +0.460] | 0.877 [0.841, 0.907] | +0.380 [+0.297, +0.465] |
| UAEH-ESTL | 30 | 0.514 [0.443, 0.580] | 0.851 [0.818, 0.882] | +0.337 [+0.263, +0.415] | 0.854 [0.828, 0.880] | +0.340 [+0.270, +0.413] |
| UAEH-ESAT | 30 | 0.465 [0.385, 0.549] | 0.845 [0.819, 0.872] | +0.379 [+0.290, +0.467] | 0.837 [0.810, 0.865] | +0.371 [+0.282, +0.458] |
| ITSOEH | 30 | 0.478 [0.405, 0.559] | 0.865 [0.831, 0.895] | +0.386 [+0.300, +0.464] | 0.866 [0.840, 0.891] | +0.387 [+0.312, +0.461] |
| TESJI (sin verificar) | 30 | 0.581 [0.511, 0.650] | 0.861 [0.830, 0.891] | +0.280 [+0.214, +0.346] | 0.864 [0.835, 0.894] | +0.283 [+0.219, +0.351] |
| UMB-JILO | 30 | 0.536 [0.457, 0.618] | 0.890 [0.857, 0.919] | +0.354 [+0.270, +0.441] | 0.895 [0.861, 0.924] | +0.359 [+0.280, +0.445] |
| UMB-VDC (sin verificar) | 30 | 0.576 [0.509, 0.638] | 0.886 [0.861, 0.910] | +0.310 [+0.243, +0.381] | 0.890 [0.867, 0.911] | +0.314 [+0.249, +0.383] |
| UCAN (sin verificar) | 30 | 0.540 [0.478, 0.601] | 0.856 [0.821, 0.891] | +0.316 [+0.260, +0.370] | 0.863 [0.826, 0.898] | +0.323 [+0.265, +0.382] |
| UNE-TULA | 30 | 0.570 [0.502, 0.641] | 0.863 [0.832, 0.893] | +0.293 [+0.222, +0.359] | 0.855 [0.823, 0.888] | +0.286 [+0.215, +0.352] |
| Sin `seed_sin_verificar` | 210 | 0.511 [0.480, 0.539] | 0.860 [0.849, 0.871] | +0.349 [+0.319, +0.381] | 0.859 [0.848, 0.871] | +0.349 [+0.319, +0.379] |
| `budget.max` ≤ $2,000 | 73 | 0.568 [0.525, 0.608] | 0.857 [0.837, 0.875] | +0.289 [+0.243, +0.336] | 0.865 [0.847, 0.882] | +0.298 [+0.254, +0.343] |
| `budget.max` $2,001–$3,500 | 158 | 0.534 [0.503, 0.566] | 0.867 [0.854, 0.880] | +0.332 [+0.300, +0.364] | 0.864 [0.850, 0.877] | +0.330 [+0.298, +0.361] |
| `budget.max` > $3,500 | 69 | 0.468 [0.410, 0.518] | 0.859 [0.834, 0.880] | +0.391 [+0.338, +0.446] | 0.859 [0.835, 0.881] | +0.391 [+0.343, +0.446] |

**Hipótesis del segmento `budget.max` > $3,500: CONFIRMADA** (regla de M-ML-040, fijada antes de calcular los segmentos).

- (1) Δ NDCG@10 en budget:budget_gt_3500 = +0.391 es la mayor de los rangos de presupuesto
- (2) IC 95 % [+0.338, +0.446] excluye el 0 (por encima)
- (3) quitar el grupo presupuesto cambia el NDCG@10 en -0.0840 de media (5/5 pliegues por debajo): lo empeora

Salvedades (aceptadas al aprobar M-ML-040):

- **T04-H2:** la condición (1) compara medias puntuales; el IC de > $3,500 (+0.391 [+0.338, +0.446]) se solapa con el de `budget.max` $2,001–$3,500. La regla no exige que las diferencias entre rangos sean significativas.
- **T04-H3:** la condición (3) usa la ablación **global** del grupo presupuesto (-0.0840, 5/5 pliegues), no una ablación por rango de presupuesto como sugiere la spec §6.7.

## 7. Desglose del puntaje

**Calibración** (afín por factor sobre el recorte: puntos_k = 100 · (C_k - min_k) / S; M-ML-041): cada feature se recorta a su `training_range` antes de puntuar; S = 45.281261 unidades de logit, intercepto b0 = -4.133224. `matchScore` es una función afín creciente del logit, así que el orden del ranking es el del modelo.

**Coeficientes** (entrada estandarizada; «puntos por unidad» = 100 · |logit por unidad| / S):

| Feature | Factor | coef (estandarizado) | Logit por unidad | Puntos por unidad | Signo coherente |
| --- | --- | ---: | ---: | ---: | --- |
| `distance_km` | `distance` | -3.349741 | -0.531370 | 1.1735 | sí |
| `effective_distance_km` | `distance` | -1.869938 | -0.505759 | 1.1169 | sí |
| `price_to_budget` | `budget` | -0.996049 | -4.277202 | 9.4459 | sí |
| `services_coverage` | `essential_services` | +0.565531 | +3.049062 | 6.7336 | sí |
| `place_type_match` | `place_type` | +1.006235 | +2.198060 | 4.8542 | sí |
| `property_type_match` | `property_type` | +0.359535 | +0.781024 | 1.7248 | sí |
| `pets_compatible` | término fijo | +0.000000 | +0.000000 | 0.0000 | — |
| `distance_ratio` | `distance` | -0.100585 | -0.029395 | 0.0649 | sí |
| `price_below_min` | `budget` | -0.012314 | -0.056130 | 0.1240 | **no** |
| `extra_services_ratio` | `extras` | +0.439171 | +2.042204 | 4.5100 | sí |

**`maxPoints` frente a los pesos del baseline y a la ablación:**

| Factor | maxPoints | Peso del baseline | Δ NDCG@10 ablación LR | Orden por maxPoints | Orden por ablación |
| --- | ---: | ---: | ---: | ---: | ---: |
| Presupuesto (`budget`) | 9.6 | 30 | -0.0840 | 2 | 3 |
| Distancia (`distance`) | 72.6 | 25 | -0.2545 | 1 | 1 |
| Servicios esenciales (`essential_services`) | 6.7 | 20 | -0.0384 | 3 | 4 |
| Tipo de lugar (`place_type`) | 4.9 | 10 | -0.1118 | 4 | 2 |
| Tipo de inmueble (`property_type`) | 1.7 | 10 | -0.0137 | 6 | 6 |
| Extras (`extras`) | 4.5 | 5 | -0.0195 | 5 | 5 |
| **Total** | 100.0 | 100 |  |  |  |

Verificación en 30,755 pares de test: máx. |Σ sin redondear - `matchScore`| 0.49997 (≤ 0.5); máx. |Σ reportada - Σ| 0.2129 (≤ 0.3); 0 factores fuera de [0, `maxPoints`]; 0 totales fuera de [0, 100]; orden idéntico al del logit en 300/300 estudiantes.

**Distribución de `matchScore` en test** (DATOS SINTÉTICOS; 30,755 pares candidatos de la medición única de T03):

| Mín. | p5 | p10 | p25 | p50 | p75 | p90 | p95 | Máx. | Media |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 8 | 50 | 61 | 74 | 81 | 85 | 88 | 90 | 97 | 77.6 |

| `matchScore` | 0-9 | 10-19 | 20-29 | 30-39 | 40-49 | 50-59 | 60-69 | 70-79 | 80-89 | 90-100 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Pares | 1 | 45 | 108 | 319 | 963 | 1,444 | 2,491 | 7,407 | 16,140 | 1,837 |
| % | 0.0 % | 0.1 % | 0.4 % | 1.0 % | 3.1 % | 4.7 % | 8.1 % | 24.1 % | 52.5 % | 6.0 % |

Puntos por factor (sin redondear) en los mismos pares:

| Factor | maxPoints | Media | p25 | Mediana | p75 | Media / maxPoints |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Presupuesto (`budget`) | 9.6 | 4.27 | 2.49 | 4.40 | 6.04 | 45 % |
| Distancia (`distance`) | 72.6 | 63.42 | 60.55 | 68.04 | 70.90 | 87 % |
| Servicios esenciales (`essential_services`) | 6.7 | 4.94 | 4.04 | 4.81 | 5.77 | 73 % |
| Tipo de lugar (`place_type`) | 4.9 | 1.52 | 0.00 | 0.00 | 4.85 | 31 % |
| Tipo de inmueble (`property_type`) | 1.7 | 1.25 | 0.00 | 1.72 | 1.72 | 73 % |
| Extras (`extras`) | 4.5 | 2.17 | 1.50 | 2.26 | 2.87 | 48 % |

**T05-H2 — `maxPoints` no es importancia.** `maxPoints` mide cuánto puede mover el logit cada factor dentro de **todo** su `training_range`, no cuánto importa en los candidatos típicos; la medida de importancia es la ablación (sección 6). El factor `distance` recibe el mayor `maxPoints` porque su rango llega hasta los límites del esquema mientras la mayoría de los candidatos están cerca: casi todos obtienen una parte grande de esos puntos y **los cuartos cercanos tienden a puntajes altos** (en test, mediana de `matchScore` 81; `distance` da en promedio el 87 % de su máximo). Las tablas de arriba y los ejemplos de abajo salen de `trained_metrics.json` (`breakdown.distribution` y `breakdown.examples`); [`trained_breakdown.md`](trained_breakdown.md) §4 y §6 los muestra con el histograma de barras.

**Ejemplos explicados** (DATOS SINTÉTICOS; regla fija de M-ML-041, decisión 8: por rango de `budget.max`, el primer estudiante de test por `student_id` de un campus verificado con suficientes candidatos, y su top por `matchScore`; ids sintéticos, sin datos personales; «Baseline» es el `matchScore` de `baseline-1.0.0` para el mismo par):

1. `S0031` — UTTT, `budget.max` $1,500 (≤ $2,000), 112 candidatos.

| # | Cuarto | matchScore | `budget` (/9.6) | `distance` (/72.6) | `essential_services` (/6.7) | `place_type` (/4.9) | `property_type` (/1.7) | `extras` (/4.5) | Relevancia sintética | Baseline |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | `R00261` | **94** | 7.3 | 70.6 | 6.7 | 4.9 | 1.7 | 2.6 | 3 | 81 |
| 2 | `R00101` | **93** | 6.4 | 72.2 | 5.1 | 4.9 | 1.7 | 3.2 | 3 | 82 |
| 3 | `R00069` | **93** | 7.0 | 72.2 | 5.1 | 4.9 | 1.7 | 2.6 | 3 | 78 |
| 4 | `R00282` | **91** | 6.1 | 70.7 | 5.1 | 4.9 | 1.7 | 2.6 | 3 | 83 |
| 5 | `R00193` | **90** | 5.5 | 71.1 | 5.1 | 4.9 | 0.0 | 3.2 | 2 | 78 |

Primer lugar `R00261`: `budget` 7.3/9.6, `distance` 70.6/72.6, `essential_services` 6.7/6.7, `place_type` 4.9/4.9, `property_type` 1.7/1.7, `extras` 2.6/4.5 → `matchScore` **94** (suma sin redondear 93.747352). Renta = 23% de `budget.max`, 0.89 km (efectiva 0.89 km), cobertura de servicios 100%, extras 0.57.

2. `S0003` — UTTT, `budget.max` $2,200 ($2,001–$3,500), 178 candidatos.

| # | Cuarto | matchScore | `budget` (/9.6) | `distance` (/72.6) | `essential_services` (/6.7) | `place_type` (/4.9) | `property_type` (/1.7) | `extras` (/4.5) | Relevancia sintética | Baseline |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | `R00244` | **89** | 6.3 | 70.7 | 3.8 | 4.9 | 0.0 | 3.4 | 3 | 44 |
| 2 | `R00088` | **88** | 6.5 | 66.6 | 5.8 | 4.9 | 0.0 | 4.5 | 3 | 50 |
| 3 | `R00101` | **88** | 7.4 | 72.2 | 3.8 | 0.0 | 0.0 | 4.5 | 3 | 54 |
| 4 | `R00193` | **88** | 6.7 | 71.0 | 4.8 | 0.0 | 1.7 | 3.4 | 3 | 44 |
| 5 | `R00075` | **88** | 5.0 | 70.6 | 4.8 | 4.9 | 0.0 | 2.3 | 3 | 53 |

Primer lugar `R00244`: `budget` 6.3/9.6, `distance` 70.7/72.6, `essential_services` 3.8/6.7, `place_type` 4.9/4.9, `property_type` 0/1.7, `extras` 3.4/4.5 → `matchScore` **89** (suma sin redondear 89.101906). Renta = 34% de `budget.max`, 0.78 km (efectiva 0.78 km), cobertura de servicios 57%, extras 0.75.

3. `S0044` — UTTT, `budget.max` $5,400 (> $3,500), 200 candidatos.

| # | Cuarto | matchScore | `budget` (/9.6) | `distance` (/72.6) | `essential_services` (/6.7) | `place_type` (/4.9) | `property_type` (/1.7) | `extras` (/4.5) | Relevancia sintética | Baseline |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | `R00088` | **94** | 8.2 | 69.0 | 6.7 | 4.9 | 1.7 | 3.4 | 3 | 74 |
| 2 | `R00175` | **92** | 6.0 | 72.5 | 4.8 | 4.9 | 1.7 | 2.3 | 3 | 78 |
| 3 | `R00235` | **92** | 6.9 | 70.6 | 6.7 | 4.9 | 1.7 | 1.1 | 3 | 78 |
| 4 | `R00156` | **91** | 7.0 | 72.0 | 5.8 | 4.9 | 1.7 | 0.0 | 3 | 74 |
| 5 | `R00269` | **91** | 6.4 | 70.9 | 4.8 | 4.9 | 1.7 | 2.3 | 3 | 76 |

Primer lugar `R00088`: `budget` 8.2/9.6, `distance` 69/72.6, `essential_services` 6.7/6.7, `place_type` 4.9/4.9, `property_type` 1.7/1.7, `extras` 3.4/4.5 → `matchScore` **94** (suma sin redondear 93.931952). Renta = 13% de `budget.max`, 2.48 km (efectiva 0.62 km), cobertura de servicios 100%, extras 0.75.

**Desglose de ejemplo** (salida JSON del primer lugar del ejemplo 1, `R00261`; formato del contrato: `maxPoints` de la calibración y lista `features` en los factores de varias features):

```json
{
  "modelVersion": "logreg-1.0.0",
  "featureSchemaVersion": "2.0.0",
  "matchScore": 94,
  "breakdown": [
    {
      "factor": "budget",
      "points": 7.3,
      "maxPoints": 9.6,
      "features": [
        {
          "feature": "price_to_budget",
          "value": 0.23333333333333334
        },
        {
          "feature": "price_below_min",
          "value": 0.5625
        }
      ]
    },
    {
      "factor": "distance",
      "points": 70.6,
      "maxPoints": 72.6,
      "features": [
        {
          "feature": "distance_km",
          "value": 0.8924504599656791
        },
        {
          "feature": "effective_distance_km",
          "value": 0.8924504599656791
        },
        {
          "feature": "distance_ratio",
          "value": 0.029748348665522634
        }
      ]
    },
    {
      "factor": "essential_services",
      "points": 6.7,
      "maxPoints": 6.7,
      "feature": "services_coverage",
      "value": 1
    },
    {
      "factor": "place_type",
      "points": 4.9,
      "maxPoints": 4.9,
      "feature": "place_type_match",
      "value": 1
    },
    {
      "factor": "property_type",
      "points": 1.7,
      "maxPoints": 1.7,
      "feature": "property_type_match",
      "value": 1
    },
    {
      "factor": "extras",
      "points": 2.6,
      "maxPoints": 4.5,
      "feature": "extra_services_ratio",
      "value": 0.5714285714285714
    }
  ]
}
```

**Nota para `TheHouse-GenAI` (cómo interpretar y explicar el `breakdown`):**

1. Explicar cada factor como **«obtuvo `points` de `maxPoints` posibles»** (proporción lograda), nunca presentar `maxPoints` como el peso o la importancia del factor ni comparar `maxPoints` entre factores («distancia vale el 72.6 % de la decisión» es **incorrecto**).
2. Para decir qué distingue a un cuarto de otro, usar la **diferencia de `points`** entre cuartos del mismo estudiante: entre cuartos cercanos la distancia difiere en pocas décimas y los factores con `maxPoints` pequeño (tipo de lugar, servicios) pueden decidir el orden.
3. Un `matchScore` alto es sobre todo «cuarto cercano»; no debe leerse como garantía de que todo coincide. Mencionar los factores con proporción baja.
4. Los factores de varias features (`budget`, `distance`) traen la lista `features` con su `value` de entrada **sin recortar**; el puntaje usa el valor recortado al `training_range`.
5. No recalcular ni modificar `matchScore` ni `points` (constitución §2.5); no mencionar `probability` ni `logit`, que no forman parte del contrato.
6. Todo lo de esta ficha es **sintético**: no afirmar que el puntaje refleja preferencias reales.

## 8. Limitaciones y riesgos

- **Desviación T05-H1 (aceptada, M-ML-041):** el coeficiente de `price_below_min` es negativo; por debajo de `budget.min` el factor presupuesto **sube con la renta** si `budget.max / budget.min` > 76.2 (46 de 10,204 pasos de la rejilla; subida máxima +0.124 puntos sobre 100 con `budget.min` 1 y `budget.max` 50,000). En los datos sintéticos el mayor cociente es 2.83: ningún par observado en la zona. NV-23 debe conocerla; se corrige en `logreg-1.1.0`.
- **Datos de encuestas antiguos:** la calibración usa ENVI 2020 y ENIGH 2022 deflactadas con el INPC a 2024-07; no observan el mercado actual.
- **Renta estatal:** la renta no se estima a nivel municipal (no cumple la regla de suficiencia); se usa la estimación estatal de ENVI ([data card](data_card.md) §5 y §10).
- **3 universidades sin verificar** (`seed_sin_verificar`: `TESJI`, `UMB-VDC`, `UCAN`): sus coordenadas no se confirmaron; el segmento sin ellas (210 estudiantes) da Δ NDCG@10 LR +0.349 [+0.319, +0.381].
- **Estudiantes con 1 candidato:** 1 excluido(s) de entrenamiento y evaluación (`S1078`); en producción un estudiante con un solo candidato recibe su puntaje, pero no hay ranking que evaluar.
- **Circularidad y etiquetas sintéticas** (ML-05-RK01): sección 3.
- **Paridad pendiente:** la exportación a ONNX, la paridad Python ↔ onnxruntime y la de .NET se verifican en ML-06 y NV-23 (ML-05-RK03); el recorte al `training_range` debe ser la misma regla en el backend.
- **Campos de Property pendientes (NV-12, RK03 de ML-03):** el cuarto del backend aún no tiene coordenadas, `placeType`, `propertyType`, mascotas, `livingWith` ni servicios como catálogo; sin ellos el modelo no se puede usar en producción.
- **Timeout del CI (T05-H3):** el job `quality` necesitó un tiempo límite mayor porque las pruebas casi lo agotaban (`.github/workflows/ci.yml`, 30 min); en T07 se optimizaron las pruebas más lentas (fixtures de sesión y datos pequeños) sin cambiar ese límite.

## 9. Decisión de promoción y trabajo futuro

**Decisión (M-ML-041, 2026-11-24):** `logreg_promoted`. La regresión logística cumple las 3 condiciones: queda **PROMOVIDA** como modelo de ML-05 (`logreg-1.0.0`, puntaje 0-100 con desglose por factor); `baseline-1.0.0` queda como respaldo. Desviación conocida aceptada (M-ML-041): `monotonicity_budget` (ver `reports/trained_breakdown.md`).

- Desviación aceptada `monotonicity_budget`: M-ML-041 (T05-H1): el coeficiente de price_below_min es negativo; por debajo de budget.min el factor presupuesto sube con la renta solo si budget.max / budget.min > 76.2 (máximo +0.124 puntos sobre 100; ningún par observado en esa zona). Aceptada por el responsable después de ver la verificación; corrección en logreg-1.1.0 (ML-07).

**Trabajo futuro:**

1. `logreg-1.1.0` sin `price_below_min` o con su coeficiente ≥ 0 (corrige T05-H1), evaluado con datos nuevos de ML-07: la partición test ya se usó y no puede medir un modelo reentrenado (spec §6.2).
2. Revisar la calibración de `maxPoints` (T05-H2) para que refleje mejor el rango observado en los candidatos y no todo el `training_range`; requiere una versión nueva del modelo y coordinarlo con `TheHouse-GenAI` y NV-23.
3. Vigilar el tiempo de la suite en el CI: las pruebas lentas se optimizaron en T07 (T05-H3); si vuelve a acercarse al límite, separar las pruebas de integración pesadas en un job propio.
4. LightGBM como candidato si se resuelve un desglose aditivo explicable y exportable (p. ej., contribuciones por factor); hoy queda como referencia del potencial.
5. Medir el desempeño real con señales reales de TheHouse (ML-07).

## 10. Versiones y trazabilidad

| Elemento | Versión |
| --- | --- |
| Modelo promovido | `logreg-1.0.0` |
| Respaldo | `baseline-1.0.0` |
| Esquema de features | 2.0.0 |
| `train_params.json` | 1.0.0 |
| `labels_params.json` | 1.0.0 |
| `baseline_eval_params.json` | 1.0.0 |
| Paquete `thehouse-ml` | 0.1.0 |
| `scikit-learn` (`uv.lock`) | 1.9.1 |
| `lightgbm` (`uv.lock`) | 4.7.0 |
| `numpy` (`uv.lock`) | 2.5.4 |
| `pandas` (`uv.lock`) | 3.1.0 |
| `scipy` (`uv.lock`) | 1.18.1 |

| Archivo | SHA-256 | Verificado contra |
| --- | --- | --- |
| `models/logreg.json` | `341ea3ec705c2f877b36d1b931eafe2dea4630523560fe8764ce59e0a70b27fa` | `train_manifest.json` |
| `models/lightgbm_lambdarank.txt` | `5eb8d22dd771e04444383c092413c02501726d6ea1362111673b87ecd9e6086d` | `train_manifest.json` |
| `data/processed/pairs.parquet` | `1f5c4d167c2e45c571681857e6b6b34d2889f9cf5d3578729b6557f0b0431cae` | `train_manifest.json` |
| `data/processed/labels.parquet` | `b720f05602fb19cd1d68b8ee9a38d3a593b4cca6eb4985ae3a6a99984d9cdd54` | `train_manifest.json` |
| `data/processed/students.parquet` | `d4ad048ac31392bf98d33437d4bf91ba07839ffeb83d1ebec9842fb49ef373c1` | `train_manifest.json` |

Pull requests de ML-05 (`thehouse-team/TheHouse-ML`):

| Trabajo | PR | Rama |
| --- | ---: | --- |
| spec 1.0.0 (M-ML-036) | #30 | `docs/ml-05-spec` |
| T01 dependencias y carga de datos (M-ML-037) | #31 | `feat/ml-05-deps-data` |
| T02 validación cruzada y entrenamiento (M-ML-038) | #32 | `feat/ml-05-train` |
| T03 medición única en test (M-ML-039) | #33 | `feat/ml-05-eval` |
| T04 ablación y segmentos (M-ML-040) | #34 | `feat/ml-05-ablation` |
| T05 puntaje 0-100 y desglose (M-ML-041) | #35 | `feat/ml-05-breakdown` |
| T06 model card (M-ML-042) | #36 | `docs/ml-05-model-card` |
| T07 pruebas, evidencia y cierre (M-ML-043) | pendiente | `feat/ml-05-cierre` |

Decisiones:

- **M-ML-029** — ML-04 aprobada: modelo base, esquema 2.0.0 y etiquetas sintéticas
- **M-ML-031** — puntaje 0-100 con desglose y redondeo del baseline
- **M-ML-032** — simulador de etiquetas sintéticas (`labels_params.json` 1.0.0)
- **M-ML-034** — evaluación del baseline (`baseline_eval_params.json` 1.0.0)
- **M-ML-035** — cierre de ML-04 y herencia para ML-05
- **M-ML-036** — ML-05 1.0.0 aprobada: criterio de promoción
- **M-ML-037** — T01: dependencias y cargador
- **M-ML-038** — T02: pliegues, rejilla, semilla y perfil `ci`
- **M-ML-039** — T03: medición única en test y comparación pareada
- **M-ML-040** — T04: ablación, segmentos e hipótesis > $3,500
- **M-ML-041** — T05: calibración, desglose, desviación T05-H1 y promoción
- **M-ML-042** — T06: generación de esta model card (APROBADO)
- **M-ML-043** — T07: cierre de ML-05 y herencia para ML-06

Reportes de origen: `reports/trained_metrics.json`, `reports/train_cv_metrics.json`, `reports/ablation_metrics.json` y `reports/baseline_metrics.json`; detalle en [`trained_report.md`](trained_report.md), [`ablation_report.md`](ablation_report.md), [`trained_breakdown.md`](trained_breakdown.md), [`training_report.md`](training_report.md) y [`baseline_report.md`](baseline_report.md).

## 11. Artefacto ONNX

> **DATOS SINTÉTICOS.** Sección generada por `python -m thehouse_ml export` (ML-06 T05, spec ML-06 §6.6); no forma parte de `reports/model_card.md`. Las cifras de paridad comparan implementaciones del mismo modelo, no miden desempeño.

| Campo | Valor |
| --- | --- |
| Paquete | `model-v1.0.0` (perfil `full`) |
| Publicable | **SÍ** |
| Modelo | `logreg-1.0.0` · esquema de features `2.0.0` |
| Commit de git (`gitCommit`) | `59f07bcfe1fa6cf4a0f183b568c56913980175ee` |
| `logreg.json` SHA-256 | `341ea3ec705c2f877b36d1b931eafe2dea4630523560fe8764ce59e0a70b27fa` |
| `model.onnx` SHA-256 | `36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5` |
| Opset / IR | 17 / 8 |
| Productor | `thehouse-ml` 0.1.0 |
| Proveedor de ejecución | onnxruntime `CPUExecutionProvider` (en .NET: `Microsoft.ML.OnnxRuntime`) |

### Entradas y salidas

| Nombre | Tipo | Forma | Contenido |
| --- | --- | --- | --- |
| `features` (entrada) | `tensor(double)` | [N, 10] | `distance_km`, `effective_distance_km`, `price_to_budget`, `services_coverage`, `place_type_match`, `property_type_match`, `pets_compatible`, `distance_ratio`, `price_below_min`, `extra_services_ratio` (orden de `featureOrder`; sin recortar: el grafo recorta al `training_range`) |
| `factor_points` (salida) | `tensor(double)` | [N, 6] | Puntos sin redondear de `budget`, `distance`, `essential_services`, `place_type`, `property_type`, `extras` |
| `raw_total` (salida) | `tensor(double)` | [N] | Suma sin redondear de `factor_points`, de izquierda a derecha |

El ONNX solo puntúa pares que pasaron el perfil incompleto y HC01-HC03; el backend (NV-23) redondea (round6 y 1 decimal por factor; `matchScore = floor(round6(raw_total) + 0.5)`; `MidpointRounding.AwayFromZero`), normaliza `-0` a `0` (M-ML-046), arma el JSON y ordena (spec ML-06 §6.1).

### Metadatos (`metadata_props`)

| Clave | Valor |
| --- | --- |
| `factorOrder` | `["budget","distance","essential_services","place_type","property_type","extras"]` |
| `featureOrder` | `["distance_km","effective_distance_km","price_to_budget","services_coverage","place_type_match","property_type_match","pets_compatible","distance_ratio","price_below_min","extra_services_ratio"]` |
| `featureSchemaVersion` | `2.0.0` |
| `gitCommit` | `59f07bcfe1fa6cf4a0f183b568c56913980175ee` |
| `logregSha256` | `341ea3ec705c2f877b36d1b931eafe2dea4630523560fe8764ce59e0a70b27fa` |
| `maxPointsRaw` | `[9.569811954389692,72.60747294686455,6.733606109723173,4.8542379171078585,1.7248277730510626,4.510043298863674]` |
| `modelVersion` | `logreg-1.0.0` |
| `profile` | `full` |
| `roundingRule` | `{"factor":"floor(round6(puntos sin redondear) * 10 + 0.5) / 10","factor_decimals":1,"total":"floor(round6(Σ puntos sin redondear) + 0.5)","pre_round_decimals":6,"mode":"half_up","dotnet":"En .NET, con double o con decimal, aplicar round6 antes de redondear: total = Math.Round(Math.Round(sum, 6, MidpointRounding.AwayFromZero), MidpointRounding.AwayFromZero); cada factor = Math.Round(Math.Round(points, 6, MidpointRounding.AwayFromZero), 1, MidpointRounding.AwayFromZero). El Math.Round por omisión (mitad al par, ToEven) NO es compatible."}` |
| `synthetic` | `true` |

### Paridad Python ↔ onnxruntime

Tolerancia: \|dif\| ≤ 0.0001 sin redondear; 0 diferencias en puntos redondeados y `matchScore` (spec ML-06 §6.3).

| Conjunto | Vectores | Máx. \|dif\| `factor_points` | Máx. \|dif\| `raw_total` | Dif. redondeados | Dif. `matchScore` | Resultado |
| --- | --- | --- | --- | --- | --- | --- |
| pares de test | 30,755 | 2.8e-14 | 4.3e-14 | 0 | 0 | **PASS** |
| rejilla fuera del training_range | 380 | 1.4e-14 | 1.4e-14 | 0 | 0 | **PASS** |
| casos dorados | 98 | 1.4e-14 | 1.4e-14 | 0 | 0 | **PASS** |

Casos dorados (`golden_cases.json`, recuento independiente) contra `model.onnx`: 109 casos, 98 puntuados; máx. \|dif\| 1.4e-14 (`factor_points`) y 2.8e-14 (`raw_total`); 0 diferencias en puntos redondeados y 0 en `matchScore`: **PASS**.

### Rendimiento

onnxruntime CPU, sesión creada una vez (la carga no cuenta), lote de 200 cuartos [200, 10] (los primeros pares de test), 10 corridas de calentamiento y 100 medidas con `time.perf_counter_ns`; hilos intra-op 0 e inter-op 0 (0 = valor por omisión).

| Campo | Valor |
| --- | --- |
| Criterio (constitución §13.5) | mediana < 20 ms: **PASS** |
| Mediana | < 1 ms |
| CPU | 13th Gen Intel(R) Core(TM) i7-13620H (16 CPUs lógicas) |
| SO | Windows-11-10.0.26200-SP0 |
| Python / onnxruntime | 3.12.16 / 1.31.1 |

La mediana se da como cota para que el paquete sea reproducible byte a byte; las cifras exactas (mediana, p95, máximo) varían entre corridas y quedan en la salida de `export` y en `specs/ml-06-onnx-export/evidence.md` (M-ML-048).

### Archivos del paquete

| Archivo | Origen |
| --- | --- |
| `model.onnx` | Grafo de la spec ML-06 §6.2 construido desde `models/logreg.json` |
| `feature_schema.json` | `src/thehouse_ml/features/feature_schema.json` 2.0.0 |
| `golden_cases.json` | `tests/fixtures/logreg_golden_cases.json` (casos dorados de `logreg-1.0.0`) |
| `baseline_golden_cases.json` | `tests/fixtures/baseline_golden_cases.json` (81 casos, respaldo en C#) |
| `model_card.md` | `reports/model_card.md` más esta sección |
| `metrics.json` | `reports/trained_metrics.json` (DATOS SINTÉTICOS) |
| `SHA256SUMS` | SHA-256 de los 6 archivos anteriores (`sha256sum -c SHA256SUMS`) |

Las copias son del contenido versionado en git (finales de línea LF). Los enlaces relativos de las secciones 1-10 apuntan a `reports/` del repositorio `TheHouse-ML`.

### Desviación conocida ML-05 T05-H1

Desviación de ML-05 (M-ML-041), no de ML-06: el coeficiente de `price_below_min` es negativo: por debajo de `budget.min`, el factor presupuesto **sube con la renta** si `budget.max / budget.min` > 76.2 (subida máxima +0.124 puntos sobre 100; ningún par sintético observado en esa zona). Se conserva tal cual en este artefacto (M-ML-041) y se corrige en `logreg-1.1.0`; los casos `t05_h1_zone` de `golden_cases.json` la muestran.

### Publicación

El release de GitHub `model-v1.0.0` lo publica el responsable a mano con los 7 archivos, sobre el commit de `main` que generó el paquete; es inmutable (una corrección es una versión nueva, constitución §13.3). El backend descarga por tag, verifica `SHA256SUMS` y fija en su repositorio el SHA-256 de `model.onnx`.
