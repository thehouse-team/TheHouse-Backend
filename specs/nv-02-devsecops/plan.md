# NV-02 — Plan CI y DevSecOps U2

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `plan.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA») |
| Estado de ejecución | **✅ COMPLETADA — PASS WITH WARNINGS (2026-11-05)** |
| Spec | `spec.md` 1.1.0-u2 |
| Tasks | `task.md` 1.1.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Reemplaza | `1.0.0-borrador`; el historial permanece en Git |

Plan preparado para revisión. No crea workflows ni cambia la configuración de GitHub hasta la autorización «EJECUTA».

## 1. Enfoque

La CI se construye en un solo workflow, `ci.yml`, por capas: primero build y pruebas, después los escáneres de código y dependencias, luego contenedor e infraestructura, la mutación como control programado y al final el quality gate y la protección de rama. Cada fase deja un job verificable en GitHub Actions.

Principios:

1. Se mide antes de fijar un umbral (cobertura y mutación).
2. Un control que no se ejecutó no es PASS.
3. La CI no usa secretos ni escribe en ningún sistema.
4. Cada herramienta es gratuita; si no, BLOCKED.
5. Los hallazgos se corrigen o se excepcionan con registro; nunca se ocultan.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Workflow base, build y pruebas | R01, R02, R03 | T01, T02, T03, T13 | `ci.yml` con `build-test` y cobertura; workflows duplicados retirados |
| F2 | Código, dependencias y secretos | R04, R05, R06, R07 | T04, T05, T06, T07, T08 | `sast-semgrep`, `sca-sbom` y `secrets-gitleaks` |
| F3 | Contenedor e infraestructura | R08, R09 | T09, T10 | `container-security` e `iac-terraform` |
| F4 | Mutación | R10, R13 | T11 | `mutation-stryker` programado y manual, con línea base |
| F5 | Gate, protección y evidencia | R11, R12, R13 | T12, T14, T15 | `quality-gate`, checks requeridos documentados y `evidence.md` |

## 3. Detalle de fases

### F1 — Workflow base, build y pruebas

**Requisitos:** NV-02-R01, NV-02-R02, NV-02-R03. **Tasks:** T01, T02, T03, T13.

**Entregables:**

- `.github/workflows/ci.yml` con disparadores, permisos mínimos, `concurrency` y `timeout-minutes`.
- Job `build-test`: SDK desde `global.json`, restore, build Release con advertencias como error y las tres suites con TRX.
- Cobertura con el recolector existente (`coverlet.collector`), resumen por proyecto en el run y artefacto.
- Retiro de `u2-linux-validation.yml` y `u2-container-validation.yml`.

**Criterios de salida:** el workflow corre en un pull request de prueba; una prueba fallida rompe `build-test`; la cobertura aparece en el resumen.

### F2 — Código, dependencias y secretos

**Requisitos:** NV-02-R04, NV-02-R05, NV-02-R06, NV-02-R07. **Tasks:** T04, T05, T06, T07, T08.

**Entregables:**

- Job `sast-semgrep` con rulesets públicos y reglas propias en `.semgrep/`; SARIF como artefacto.
- Job `sca-sbom` con auditoría de dependencias vulnerables y SBOM CycloneDX.
- Job `secrets-gitleaks` sobre todo el historial, con valores redactados.

**Criterios de salida:** cada job falla ante un caso de prueba controlado (regla, dependencia o secreto ficticio) y pasa sobre el código actual o con excepciones registradas.

### F3 — Contenedor e infraestructura

**Requisitos:** NV-02-R08, NV-02-R09. **Tasks:** T09, T10.

**Entregables:**

- Job `container-security`: build de la imagen, Trivy de imagen y de configuración (Dockerfile y Terraform) y Dockle.
- Job `iac-terraform`: `fmt -check`, `init -backend=false` y `validate`.

**Criterios de salida:** la imagen se ejecuta como no root; no quedan Critical/High corregibles sin excepción; Terraform válido sin credenciales.

### F4 — Mutación

**Requisitos:** NV-02-R10, NV-02-R13. **Tasks:** T11.

**Entregables:** job `mutation-stryker` semanal y manual sobre Domain y Application; reporte y línea base registrada.

**Criterios de salida:** el score medido queda en `evidence.md`; el umbral se propone con esa evidencia.

### F5 — Gate, protección y evidencia

**Requisitos:** NV-02-R11, NV-02-R12, NV-02-R13. **Tasks:** T12, T14, T15.

**Entregables:**

- Job `quality-gate` que agrega los jobs obligatorios y publica el resumen de hardening.
- Guía de configuración del ruleset de `main` con los checks requeridos y estado de M-BE-050.
- `evidence.md` con run, commit, resultados, hallazgos, excepciones y consumo de minutos.

**Criterios de salida:** el pull request muestra todos los checks; la revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

## 4. Secuencia y dependencias

```text
F1 Workflow/build/pruebas
  ↓
F2 Código/dependencias/secretos
  ↓
F3 Contenedor/IaC
  ↓
F4 Mutación
  ↓
F5 Gate/protección/evidencia
```

- T02 depende de T01.
- T03 depende de T02.
- T04 depende de T01.
- T05 depende de T04.
- T06 depende de T01.
- T07 depende de T06.
- T08 depende de T01.
- T09 depende de T01.
- T10 depende de T01.
- T11 depende de T02.
- T12 depende de T02, T04, T06, T07, T08, T09 y T10.
- T13 depende de T02 y T09.
- T14 depende de T12 y de la decisión M-BE-050.
- T15 depende de T12 y T14.

## 5. Estrategia de verificación

| Control | Verificación | Evidencia |
| --- | --- | --- |
| Build y pruebas | Run en pull request y prueba fallida controlada en local | Run y TRX |
| SAST | Regla propia que detecta un caso de prueba | SARIF |
| SCA | Comando local equivalente y reporte | Reporte |
| Secretos | Escaneo del historial completo | Reporte redactado |
| Contenedor e IaC | Reportes de Trivy y Dockle; salida de Terraform | Reportes |
| Mutación | Ejecución manual de Stryker.NET | Reporte |
| Gate | Run completo con todos los checks | Resumen del run |

## 6. Seguridad

- `permissions: contents: read`; sin secretos ni tokens con escritura.
- Acciones e imágenes fijadas por versión (SHA cuando sea viable).
- Reportes revisados antes de compartirse.
- Supresiones con justificación, responsable y vencimiento.

## 7. Costo

| Concepto | Costo esperado | Evidencia requerida |
| --- | --- | --- |
| Minutos de Actions | $0 dentro de 2,000 min/mes (privado) o sin límite (público) | Minutos por run y consumo mensual |
| Herramientas | $0 (open source) | Versión y licencia |
| SonarCloud / SARIF en code scanning | $0 solo si el repositorio es público | Decisión M-BE-050 |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación humana de 1.1.0-u2 | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas | BLOCKED |
| G1 | Fin F1 | `build-test` corre en pull request | No avanzar |
| G2 | Fin F2 | Escáneres operativos con hallazgos tratados | No avanzar |
| G3 | Fin F3 | Contenedor e IaC verificados | No avanzar |
| G4 | Fin F4 | Línea base de mutación registrada | No avanzar |
| G5 | Fin F5 | Checks visibles, evidencia y revisión | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T13 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04, T05 |
| R05 | F2 | T06 |
| R06 | F2 | T07 |
| R07 | F2 | T08 |
| R08 | F3 | T09 |
| R09 | F3 | T09, T10 |
| R10 | F4 | T11 |
| R11 | F5 | T12, T14 |
| R12 | F5 | T15 |
| R13 | F5 | T11, T15 |

## 10. No ejecución

Este plan no autoriza crear workflows, instalar herramientas, modificar la configuración de GitHub ni publicar o desplegar. Estado de ejecución: autorizado con «EJECUTA» el 2026-11-05; avance en `task.md` y `evidence.md`.
