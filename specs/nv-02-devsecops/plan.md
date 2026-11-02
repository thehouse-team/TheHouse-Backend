# NV-02 — Plan DevSecOps

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

Plan de ejecución futura. No se instala nada, no se crean workflows y no se ejecutan pruebas en esta entrega.

## 1. Enfoque

NV-02 se ejecuta después de que NV-01 produzca fundamentos verificables y después de que NV-03 produzca una imagen autorizada para las tareas de contenedor. La cadena se divide en seis fases para evitar que un gate dependa de un artefacto inexistente.

Principios:

1. Primero se mide, después se fija el umbral.
2. Un control no ejecutado no es PASS.
3. La evidencia debe ser reproducible: comando, versión, entorno, datos y reporte.
4. Las herramientas deben ser gratuitas o locales; si no, BLOCKED.
5. DAST y fuzzing solo en entornos aislados y autorizados.
6. La seguridad de contenedor no se simula sin imagen real.
7. Los repositorios de referencia aportan metodología, no código ni umbrales.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Línea base de pruebas | R01, R02, R03 | T01, T02, T03, T04, T05 | Suites unitarias, integración y seguridad ejecutables |
| F2 | Análisis estático y suministro | R04, R05, R06 | T06, T07, T08, T09 | SAST/SCA/SBOM con baseline |
| F3 | Pruebas avanzadas | R07, R08, R09 | T10, T11, T12 | Mutación, contrato y performance medidos |
| F4 | Seguridad dinámica | R10, R11 | T13, T14 | DAST y fuzzing en entorno aislado |
| F5 | Contenedor y firma | R12, R13 | T15, T16, T17 | Imagen escaneada, firmada y verificada |
| F6 | Gates, CI y evidencia | R14, R15, R16 | T18, T19, T20, T21, T22, T23, T24 | Pipeline y gates trazables |

## 3. Detalle de fases

### F1 — Línea base de pruebas

**Requisitos:** NV-02-R01, NV-02-R02, NV-02-R03.

**Tasks:** T01, T02, T03, T04, T05.

**Entregables futuros:**

- Inventario de suites y riesgos cubiertos.
- Pruebas unitarias de Domain/Application sin infraestructura.
- Harness de integración con `WebApplicationFactory`.
- Suite de seguridad con casos negativos y datos de prueba.
- Reporte base de ejecución y limitaciones.

**Criterios de salida:**

1. Las suites obligatorias ejecutan y fallan cuando deben fallar.
2. Los datos de prueba se aíslan y limpian.
3. No se usan credenciales ni servicios productivos.
4. Los placeholders se identifican y no se reportan como cobertura.

**Evidencia:** reporte de cada suite, versión de xUnit, comando y entorno.

### F2 — Análisis estático y suministro

**Requisitos:** NV-02-R04, NV-02-R05, NV-02-R06.

**Tasks:** T06, T07, T08, T09.

**Entregables futuros:**

- Configuración de Semgrep y baseline de hallazgos.
- SonarAnalyzer local y, solo si cumple $0, SonarCloud.
- Auditoría de paquetes NuGet y política de versiones.
- SBOM CycloneDX trazable a la entrega o imagen.
- Registro de supresiones y excepciones.

**Criterios de salida:**

1. Cada hallazgo tiene severidad, archivo y estado.
2. Las supresiones tienen justificación, responsable y vencimiento.
3. El SBOM se genera y se conserva.
4. Un fallo del scanner no se interpreta como ausencia de hallazgos.

**Evidencia:** reportes SARIF/JSON, SBOM, versiones de reglas y configuración.

### F3 — Pruebas avanzadas

**Requisitos:** NV-02-R07, NV-02-R08, NV-02-R09.

**Tasks:** T10, T11, T12.

**Entregables futuros:**

- Baseline de mutación con Stryker.NET y alcance acotado.
- Pruebas de contrato PactNet o alcance equivalente documentado.
- Escenarios NBomber con SLO, datos y entorno.
- Informe de limitaciones y resultados no extrapolables.

**Criterios de salida:**

1. El score de mutación se mide antes de proponer un gate.
2. El contrato verificable tiene consumidor/proveedor identificado.
3. Performance declara concurrencia, duración y límites.
4. Los resultados no se presentan como producción.

**Evidencia:** reportes de Stryker, Pact y NBomber; configuración y datos.

### F4 — Seguridad dinámica

**Requisitos:** NV-02-R10, NV-02-R11.

**Tasks:** T13, T14.

**Entregables futuros:**

- Plan de DAST con OWASP ZAP y alcance autorizado.
- Plan de fuzzing con RESTler sobre OpenAPI.
- Entorno aislado, datos de prueba y límites de tasa.
- Triage de hallazgos y criterio de parada.

**Criterios de salida:**

1. El entorno no es producción ni un sistema de terceros.
2. El alcance y las credenciales de prueba están aprobados.
3. Los hallazgos se reproducen y clasifican.
4. El fuzzing no se confunde con una prueba funcional completa.

**Evidencia:** reportes ZAP/RESTler, alcance, entorno y triage.

### F5 — Contenedor y firma

**Requisitos:** NV-02-R12, NV-02-R13.

**Tasks:** T15, T16, T17.

**Entregables futuros:**

- Escaneo de imagen con Trivy.
- Lint de contenedor con Dockle.
- Firma y verificación con Cosign.
- Registro de la imagen, digest y SBOM asociado.

**Criterios de salida:**

1. Existe una imagen autorizada por NV-03.
2. No hay secretos embebidos ni usuario root innecesario.
3. Los hallazgos críticos/altos corregibles bloquean.
4. La firma se verifica antes de cualquier despliegue.

**Evidencia:** reportes Trivy/Dockle, digest, firma y verificación.

### F6 — Gates, CI y evidencia

**Requisitos:** NV-02-R14, NV-02-R15, NV-02-R16.

**Tasks:** T18, T19, T20, T21, T22, T23, T24.

**Entregables futuros:**

- Catálogo de gates y criterios de bloqueo.
- Workflow de CI con permisos mínimos y acciones fijadas.
- Pipeline de seguridad con artefactos y reportes.
- Registro de hallazgos y excepciones.
- Paquete de evidencia y revisión final.

**Criterios de salida:**

1. Cada gate tiene evidencia y efecto bloqueante.
2. Un control omitido no se reporta como PASS.
3. Las excepciones tienen responsable y vencimiento.
4. La evidencia se conserva y se puede reproducir.
5. La revisión final emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** workflow revisado, reportes consolidados, registro de excepciones y acta.

## 4. Política de umbrales y gates

No hay umbrales numéricos aprobados. Se medirán primero y se propondrán después con evidencia. Mientras tanto, rigen reglas cualitativas:

| Gate | Regla de bloqueo | Excepción |
| --- | --- | --- |
| Build y suites obligatorias | Fallo o prueba requerida omitida | No admite excepción silenciosa |
| SAST | Hallazgo crítico o alto nuevo sin triage | Excepción aprobada con vencimiento |
| SCA | Vulnerabilidad crítica/alta explotable sin tratamiento | Excepción aprobada |
| SBOM | Ausente o no trazable | Bloquea la entrega |
| Mutación | Baseline no medido o score sin evidencia | No se aprueba gate |
| Contrato | Verificación de proveedor fallida | Bloquea si el consumidor es obligatorio |
| Performance | SLO no aprobado o no medido | No se declara cumplimiento |
| DAST/fuzzing | Crítico/alto sin triage | Excepción aprobada |
| Contenedor | Crítico/alto corregible o secreto embebido | Bloquea |
| Firma | Imagen sin firma o verificación fallida | No se despliega |
| CI | Permisos excesivos, acción no fijada o secreto expuesto | Bloquea |

Los umbrales cuantitativos (cobertura, mutación, latencia, error rate) se calibrarán con datos del entorno autorizado y se registrarán en una revisión posterior.

## 5. Secuencia y dependencias

```text
NV-01 (fundamentos)
  ↓
F1 pruebas base
  ↓
F2 SAST/SCA/SBOM
  ↓
F3 mutación/contrato/performance
  ↓
F4 DAST/fuzzing
  ↓
NV-03 (imagen autorizada)
  ↓
F5 contenedor/firma
  ↓
F6 gates/CI/evidencia
```

Dependencias internas:

- T02 depende de T01.
- T04 depende de T03.
- T05 depende de T03.
- T06 y T07 dependen de T01.
- T08 y T09 dependen de T01.
- T10 depende de T01 y T02.
- T11 depende de T03 y T04.
- T12 depende de T04.
- T13 y T14 dependen de T04.
- T15, T16 y T17 dependen de la imagen autorizada por NV-03.
- T18 depende de T06 a T14.
- T19 depende de T18.
- T20 depende de T01 a T12.
- T21 depende de T06 a T17.
- T22 depende de T20 y T21.
- T23 depende de T20 a T22.
- T24 depende de T18 a T23.

## 6. Evidencia y retención

Cada ejecución futura debe conservar:

1. Identificador de la entrega y versión de la configuración.
2. Comando reproducible y versión de la herramienta.
3. Entorno, sistema operativo y datos utilizados.
4. Reporte original sin edición.
5. Resumen de hallazgos con severidad y estado.
6. Excepciones con aprobador, motivo y vencimiento.
7. Enlaces o rutas a artefactos, no secretos.

La retención debe cumplir COST-001. Un artefacto que genera cobro se elimina o se sustituye por una alternativa gratuita; si no es posible, se reporta BLOCKED.

## 7. Seguridad

- Los workflows no reciben secretos productivos.
- Las acciones se fijan por versión o digest.
- Los permisos se limitan a lectura salvo necesidad explícita.
- Los reportes se revisan antes de publicarse.
- DAST/fuzzing se ejecutan solo en un entorno autorizado.
- Las imágenes se escanean antes de firmar.
- Los hallazgos no se cierran sin corrección o excepción.
- No se copian reglas ni workflows de referencia.

## 8. Costo

| Concepto | Costo esperado | Evidencia |
| --- | --- | --- |
| Suites y herramientas open source | $0 | Licencia y versión |
| SonarCloud | $0 si el plan aplica | Captura de elegibilidad |
| GitHub Actions | $0 dentro de cuota o local | Consumo y límite |
| Registry de imágenes | $0 si el plan aplica | Cuota y retención |
| Almacenamiento de reportes | $0 | Límite y retención |
| DAST/fuzzing | $0 local | Entorno propio |

Si un componente exige tarjeta, crédito temporal o cobro por uso, se detiene y se eleva a revisión. No se asume gratuidad.

## 9. Puertas de revisión

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0 | Antes de empezar | Aprobación humana de los tres documentos | BLOCKED |
| G1 | Fin de F1 | Suites base ejecutables y evidencia | No avanzar |
| G2 | Fin de F2 | SAST/SCA/SBOM con baseline | No avanzar |
| G3 | Fin de F3 | Mutación/contrato/performance medidos | No avanzar |
| G4 | Fin de F4 | DAST/fuzzing en alcance autorizado | No avanzar |
| G5 | Fin de F5 | Imagen escaneada y firmada | No avanzar |
| G6 | Fin de F6 | Gates/CI/evidencia revisados | NV-02 no se cierra |

## 10. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T02 |
| R02 | F1 | T03, T04 |
| R03 | F1 | T05 |
| R04 | F2 | T06, T07 |
| R05 | F2 | T08 |
| R06 | F2 | T09 |
| R07 | F3 | T10 |
| R08 | F3 | T11 |
| R09 | F3 | T12 |
| R10 | F4 | T13 |
| R11 | F4 | T14 |
| R12 | F5 | T15, T16 |
| R13 | F5 | T17 |
| R14 | F6 | T18, T22, T24 |
| R15 | F6 | T20, T21, T22 |
| R16 | F6 | T19, T23, T24 |

## 11. No ejecución

Este plan no autoriza instalar herramientas, crear workflows, modificar proyectos, ejecutar scanners, escanear imágenes, firmar artefactos ni desplegar. Estado: **⛔ NO EJECUTAR**. Siguiente paso: revisión humana de `spec.md`, `plan.md` y `task.md`.
