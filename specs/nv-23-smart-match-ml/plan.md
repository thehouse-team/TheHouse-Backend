# NV-23 — Smart Match — compatibilidad con el modelo de TheHouse-ML — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `plan.md` |
| Versión | 1.2.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA — 1.2.0** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo NV-23 1.2.0»: decisiones de la §13; Mobile aceptó el cambio de contrato §6.1; la autorización «EJECUTA» de M-BE-068 pasa a T01–T14 con las mismas rutas: `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas, `scripts/db/` (regenerado por la prueba de NV-26) y `specs/nv-23-smart-match-ml/`; la T06 requiere además autorización que nombre `Microsoft.ML.OnnxRuntime`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0 |
| Tasks | `task.md` 1.2.0 |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio (traducción al esquema de ML, perfil mínimo, restricciones duras, las 10 features y el modelo base con su redondeo) con pruebas unitarias y casos calculados a mano; después `scores`, recomendaciones, feedback, descartes y métricas; luego la carga del ONNX con su respaldo y la paridad con los casos dorados de TheHouse-ML; al final rendimiento, privacidad y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Perfil y traducción al esquema de ML; Modelo base alineado con ML-04; Puntajes por cuarto |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Recomendaciones ordenadas; Retroalimentación y descartes; Carga del modelo de TheHouse-ML |
| F3 | Seguridad, autorización y límites | R07, R08, R09, R10 | T07, T08, T09, T10 | Paridad con TheHouse-ML; Rendimiento y límites; Minimización y privacidad; Métricas de Smart Match (RF-66) |
| F4 | Pruebas y evidencia | R11, R12 | T11, T12, T13, T14 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Perfil y traducción al esquema de ML** (R01): El perfil se arma desde NV-11 o desde el cuerpo para invitados con las mismas validaciones, el perfil mínimo y los valores por omisión del esquema 2.0.0; la traducción de NV-12 de la §9 tiene una prueba por valor.
- **T02 · Modelo base alineado con ML-04** (R02): Las 10 features y `baseline-1.0.0` con nombres, curvas, orden y redondeo de ML-04; HC01–HC03 con todos los motivos; desglose de 6 factores; determinista; casos propios con resultados calculados a mano, incluidos los límites de redondeo (por ejemplo 13.35 → 13.4).
- **T03 · Puntajes por cuarto** (R03): `scores` con hasta 50 cuartos, con token o como invitado; ids no publicados como `unavailable` sin revelar existencia; 422 con `missing`.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Recomendaciones ordenadas** (R04): Lista paginada sobre hasta 500 candidatos de NV-14, no descartados primero, puntaje sin redondear desc y `roomId`; no elegibles fuera; con y sin cuenta.
- **T05 · Retroalimentación y descartes** (R05): Feedback con `modelVersion` y reemplazo de la opinión anterior; descartes con TTL de 30 días y opción de deshacer; idempotentes; borrados al eliminar la cuenta (NV-10); colecciones en el script de NV-26.
- **T06 · Carga del modelo de TheHouse-ML** (R06): `OnnxCompatibilityModel` carga el paquete de entrega, verifica SHA-256 y esquema y hace la prueba de arranque; cualquier falla usa el modelo base y lo registra; desactivado por configuración.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Paridad con TheHouse-ML** (R07): Los casos dorados de ML-04 (T05) dan el mismo vector de features, el mismo desglose y el mismo `matchScore` en `baseline-1.0.0`, y los de ML-06 el mismo puntaje en ONNX dentro de la tolerancia publicada.
- **T08 · Rendimiento y límites** (R08): p95 < 300 ms para 50 cuartos (RNF-14) y < 500 ms para recomendaciones con 500 candidatos en Azure; rate limits con 429; Redis caído 503.
- **T09 · Minimización y privacidad** (R09): Prueba de que el cálculo no lee campos personales ni ubicación real; el desglose no expone valores de features ni parámetros de un modelo entrenado; las métricas no guardan ids.
- **T10 · Métricas de Smart Match (RF-66)** (R10): `GET /admin/smart-match/metrics` con rango de fechas (máximo 90 días), totales, promedio y rangos de puntaje por `modelVersion`, retroalimentación y descartes activos; solo rol `admin`; contadores diarios sin ids; una falla al contar no afecta la respuesta al usuario.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T11 · Contrato para Mobile y despliegue** (R11): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (scores con y sin cuenta, recomendaciones, feedback, descartes y métricas).
- **T12 · Pruebas unitarias y de integración** (R12): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T13 · Pruebas de seguridad** (R12): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T14 · Evidencia y revisión final** (R12): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T01, T02, T03, T04, T05, T06.
- T10 depende de: T01, T02, T03, T04, T05, T06.
- T11 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T12 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- T13 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10, T11.
- T14 depende de: T12, T13.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Perfil y traducción al esquema de ML | Pruebas automatizadas en CI |
| R02 | Modelo base alineado con ML-04 | Pruebas automatizadas en CI |
| R03 | Puntajes por cuarto | Pruebas automatizadas en CI |
| R04 | Recomendaciones ordenadas | Pruebas automatizadas en CI |
| R05 | Retroalimentación y descartes | Pruebas automatizadas en CI |
| R06 | Carga del modelo de TheHouse-ML | Pruebas automatizadas en CI |
| R07 | Paridad con TheHouse-ML | Pruebas automatizadas en CI |
| R08 | Rendimiento y límites | Pruebas automatizadas en CI |
| R09 | Minimización y privacidad | Pruebas automatizadas en CI |
| R10 | Métricas de Smart Match (RF-66) | Pruebas automatizadas en CI |
| R11 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R12 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Minimización (RNF-11): el cálculo solo lee preferencias de búsqueda y atributos públicos del cuarto y del edificio; prueba que lo verifica.
- Rate limit 30/min por IP para invitados, 60/min por usuario y 30/min para las métricas; máximo 50 cuartos por `scores` y 500 candidatos por recomendación.
- El desglose expone `factor`, `label`, `points` y `maxPoints` (pesos públicos del modelo base); los valores de las features y los parámetros internos de un modelo entrenado no se exponen.
- Las métricas no guardan ids de usuario ni de cuarto; solo staff con rol `admin` las consulta (403 para otros roles).
- El artefacto ONNX se verifica con SHA-256 antes de cargarse; nunca se carga pickle ni código ejecutable (constitution §6.1).
- La distancia usa la celda aproximada de NV-14, nunca la ubicación real del edificio.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Modelo base y ONNX en el mismo contenedor (CPU) | $0 | Sin servicios de ML administrados ni GPU |
| Entrenamiento | $0 | En `TheHouse-ML` (CPU local o GitHub Actions) |
| MongoDB Atlas M0 | $0 | Tres colecciones pequeñas con índices únicos y TTL; un documento de métricas por día y versión |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.2.0 | PASS (M-BE-085) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (M-BE-068, trasladada por M-BE-085) |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F3 | T08 |
| R09 | F3 | T09 |
| R10 | F3 | T10 |
| R11 | F4 | T11 |
| R12 | F4 | T12, T13, T14 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — 1.2.0: T01–T14 autorizadas; T06 y T07 esperan a TheHouse-ML**.
