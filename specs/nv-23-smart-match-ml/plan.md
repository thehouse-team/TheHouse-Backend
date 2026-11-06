# NV-23 — Smart Match — compatibilidad con Machine Learning — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-23 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Se entrega primero el modelo base explicable con casos de prueba de resultados esperados; el modelo entrenado es una fase posterior que solo se activa si supera al base.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Perfil Smart Match; Cálculo de compatibilidad; Recomendaciones ordenadas |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Retroalimentación y descartes; Rendimiento; Modelo entrenado y evaluación |
| F3 | Seguridad, autorización y límites | R07 | T07 | Minimización y límites |
| F4 | Pruebas y evidencia | R08 | T08, T09, T10 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Perfil Smart Match** (R01): Se arma desde las preferencias de NV-11 o del cuerpo para invitados, con las mismas validaciones.
- **T02 · Cálculo de compatibilidad** (R02): Puntaje 0–100 con factores de presupuesto, distancia, servicios, modalidad, tipo y extras; determinista por versión; casos de prueba con resultados esperados.
- **T03 · Recomendaciones ordenadas** (R03): Lista paginada ordenada por puntaje descendente con desempate estable; descartes al final.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Retroalimentación y descartes** (R04): Se registran con `modelVersion`; idempotentes; descartes por 30 días.
- **T05 · Rendimiento** (R05): p95 < 300 ms para 50 cuartos en el runner (RNF-14).
- **T06 · Modelo entrenado y evaluación** (R06): Pipeline fuera de línea con ML.NET; métrica comparada contra el modelo base; promoción solo con evidencia y nueva `modelVersion`.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Minimización y límites** (R07): Prueba que el cálculo no lee campos personales; límites de tamaño y rate limit verificados.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Pruebas unitarias y de integración** (R08): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T09 · Pruebas de seguridad** (R08): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T10 · Evidencia y revisión final** (R08): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Perfil Smart Match | Pruebas automatizadas en CI |
| R02 | Cálculo de compatibilidad | Pruebas automatizadas en CI |
| R03 | Recomendaciones ordenadas | Pruebas automatizadas en CI |
| R04 | Retroalimentación y descartes | Pruebas automatizadas en CI |
| R05 | Rendimiento | Pruebas automatizadas en CI |
| R06 | Modelo entrenado y evaluación | Pruebas automatizadas en CI |
| R07 | Minimización y límites | Pruebas automatizadas en CI |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Minimización de datos: las variables son solo preferencias de búsqueda y atributos públicos del cuarto (RNF-11).
- Rate limit 30/min por IP para invitados y 60/min por usuario.
- Máximo 50 cuartos por cálculo para acotar el costo de CPU.
- El modelo y sus pesos no se exponen; solo el desglose por factor.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| ML.NET y cálculo en el mismo contenedor | $0 | Sin servicios de ML administrados |
| Entrenamiento fuera de línea | $0 | En el runner de GitHub Actions o local |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | BLOCKED |
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
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
