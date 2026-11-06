# NV-02 — CI y DevSecOps (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `spec.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA») |
| Estado de ejecución | **✅ COMPLETADA — PASS WITH WARNINGS (2026-11-05)** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks | `task.md` 1.1.0-u2 |
| Precondición | NV-01 y NV-04 a NV-08 U2 cerradas; Dockerfile y Terraform de NV-03; M-BE-027 |
| Relación | La entrega continua (CD) se especifica en NV-09 (M-BE-049); NV-02 no publica ni despliega |
| Reemplaza | `1.0.0-borrador` (estrategia DevSecOps general); el historial permanece en Git |

Esta reemisión prepara la integración continua (CI) para revisión humana. No autoriza crear workflows, instalar herramientas ni cambiar la configuración de GitHub: la ejecución requiere la autorización humana «EJECUTA».

## 1. Contexto

En la revisión académica del 2026-11-05 el profesor observó que el backend no tiene CI/CD: al abrir un pull request hacia `main` no corre ningún control. El repositorio tiene tres workflows (`u2-linux-validation`, `u2-container-validation` y `u2-ghcr-publish`), pero solo se disparan con push a `feature/u2-entrega` o manualmente; ninguno se ejecuta en pull requests ni en `main`, no hay escáneres de seguridad y no existe una etapa de despliegue.

Además, el ruleset "Main Proteccion" de `main` (pull request obligatorio con 1 aprobación, status checks requeridos, bloqueo de force push y de borrado) **no se aplica**: la organización `thehouse-team` está en el plan Free y el repositorio es privado, y la API de GitHub responde *"Upgrade to GitHub Pro or make this repository public to enable this feature"* (verificado el 2026-11-05). Su lista de status checks requeridos también está vacía.

NV-02 define la CI U2: un pipeline que se ejecuta en cada pull request y push a `main`, escanea código, dependencias, secretos, contenedor e infraestructura, y bloquea el merge cuando un control obligatorio falla. La entrega continua (publicar, firmar, desplegar y verificar en Azure) se especifica por separado en NV-09.

### Referencias metodológicas

Solo lectura de los README de `edelomeza/WebAPIDevSecOps` y `edelomeza/WebAPIDevSecOpsScalling` (M-BE-021): se adopta la secuencia de controles, no su código, umbrales ni SQL Server.

## 2. Objetivo

Que todo cambio hacia `main` pase por una CI reproducible, a costo $0, cuyos controles sean visibles en GitHub Actions, dejen evidencia descargable y, una vez aplicada la protección de rama, impidan el merge cuando fallan.

## 3. Precondiciones

1. NV-01 y NV-04 a NV-08 U2 cerradas; la solución compila y sus pruebas pasan en local.
2. `Dockerfile` y `infra/terraform/` existen (NV-03).
3. COST-001 vigente.
4. La CI no necesita secretos: no publica, no firma ni despliega.
5. Toda ejecución requiere la autorización humana «EJECUTA» con versión, tasks y rutas.

## 4. Alcance U2

1. Workflow único `ci.yml` disparado por `pull_request` hacia `main`, `push` a `main` y ejecución manual.
2. Build en Release y las tres suites de pruebas.
3. Cobertura de código como métrica informativa.
4. SAST con Semgrep (rulesets públicos y reglas propias).
5. SCA de dependencias NuGet y SBOM CycloneDX.
6. Detección de secretos en todo el historial con Gitleaks.
7. Seguridad de contenedor con Trivy (imagen y configuración) y Dockle.
8. Validación de infraestructura como código (Terraform).
9. Línea base de mutación con Stryker.NET (programada y manual).
10. Quality gate agregado y resumen de hardening.
11. Protección de rama con checks requeridos (configuración humana documentada).
12. Retiro de los workflows de validación duplicados.

## 5. Fuera de alcance

- Publicar imágenes, firmarlas con Cosign, desplegar a Azure, smoke tests en Azure, DAST con OWASP ZAP, fuzzing con RESTler, performance con NBomber y caos: **NV-09 (CD)**.
- Contract testing con PactNet: futuro, cuando exista un consumidor (la app móvil) con contrato propio.
- Pruebas de integración con Testcontainers para los adaptadores MongoDB y Redis: futuro (spec funcional o NV-02 1.2).
- SonarCloud y la subida de SARIF a GitHub code scanning: solo si el repositorio se hace público (§7); hoy no cumplen COST-001 en un repositorio privado.
- Cambiar la visibilidad del repositorio o el plan de GitHub: decisión humana (M-BE-050).
- Copiar workflows o reglas de los repositorios de referencia.

## 6. Diseño del pipeline

| Job | Herramienta | Qué valida | Falla si | Evidencia |
| --- | --- | --- | --- | --- |
| `build-test` | .NET 10 (`global.json`) | Restore, build Release con advertencias como error y las suites unitaria, de seguridad e integración | Error de compilación, advertencia o prueba fallida | TRX y cobertura Cobertura |
| `sast-semgrep` | Semgrep CE (imagen fijada) | Rulesets `p/csharp`, `p/secrets`, `p/dockerfile`, `p/terraform` y reglas propias en `.semgrep/` | Hallazgo de severidad ERROR no excepcionado | SARIF |
| `sca-sbom` | `dotnet list package` y CycloneDX | Dependencias vulnerables (incluidas transitivas) y SBOM | Vulnerabilidad High o Critical sin excepción | Reporte y `sbom.json` |
| `secrets-gitleaks` | Gitleaks (imagen fijada, sin la action con licencia) | Secretos en todo el historial Git | Cualquier hallazgo no excepcionado | Reporte con valores redactados |
| `container-security` | Docker, Trivy y Dockle | Imagen construida del `Dockerfile`: vulnerabilidades del SO y paquetes, configuración del Dockerfile y Terraform, buenas prácticas de imagen | Trivy: Critical o High con corrección disponible; misconfiguración High o Critical. Dockle: FATAL | Reportes Trivy y Dockle |
| `iac-terraform` | Terraform 1.17.x | `terraform fmt -check`, `init -backend=false` y `validate` | Formato o configuración inválidos | Salida del job |
| `mutation-stryker` | Stryker.NET | Mutación sobre Domain y Application con `TheHouse.UnitTests` | Solo después de fijar el umbral con evidencia (§7) | Reporte HTML/JSON |
| `quality-gate` | Script del workflow | Agrega el resultado de los jobs obligatorios y publica el resumen de hardening | Algún job obligatorio no terminó en éxito | Resumen del run |

Reglas comunes:

- Disparadores: `pull_request` con base `main`, `push` a `main` y `workflow_dispatch`. `mutation-stryker` corre además de forma programada semanal y no se ejecuta en cada pull request.
- `permissions: contents: read` a nivel de workflow; ningún job usa secretos ni tokens con escritura.
- Acciones de terceros fijadas por versión exacta y, cuando sea viable, por SHA de commit; imágenes Docker fijadas por versión.
- `concurrency` por rama con cancelación del run anterior; `timeout-minutes` en cada job.
- Artefactos retenidos 14 días, sin secretos ni datos personales.
- `u2-linux-validation.yml` y `u2-container-validation.yml` se retiran porque `ci.yml` los reemplaza; `u2-ghcr-publish.yml` pasa a NV-09.

## 7. Decisiones propuestas

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Visibilidad del repositorio | Hacer público el repositorio después de que `secrets-gitleaks` pase sobre todo el historial: aplica el ruleset de `main`, da minutos ilimitados de Actions y habilita gratis SARIF en code scanning y SonarCloud. Alternativa: GitHub Pro/Team (no cumple COST-001 salvo beneficio educativo) | APLICADA: repositorio público (M-BE-050); ruleset "Main Proteccion" activo (T14) |
| Checks requeridos | `build-test`, `sast-semgrep`, `sca-sbom`, `secrets-gitleaks`, `container-security`, `iac-terraform` y `quality-gate`, con "Require branches to be up to date" | PROPUESTO |
| Cobertura | Informativa, no gate único. Línea base local del 2026-11-05: 68.2 % de líneas y 64.8 % de ramas (Domain 97.3 %, Application 97.6 %, Infrastructure 27.6 %) | PROPUESTO |
| Mutación | Medir la línea base primero; fijar después el umbral (referencia del equipo: 60 %) con evidencia | PROPUESTO |
| Reglas Semgrep propias | Conjunto inicial de al menos 8 reglas del proyecto (registro de datos sensibles, `AllowAnonymous` fuera de la lista aprobada, cadenas de conexión literales, deserialización sin límite, etc.), ampliable | PROPUESTO |
| SonarCloud | Solo si el repositorio es público (plan gratuito) | PENDIENTE |

## 8. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-02-R01 | Disparadores y permisos mínimos | `ci.yml` corre en `pull_request` a `main`, `push` a `main` y manual; `contents: read`; acciones y versiones fijadas; `concurrency` y `timeout-minutes`; sin secretos. | F1 | T01, T13 |
| NV-02-R02 | Build y pruebas | Build Release con advertencias como error; las tres suites corren y publican TRX; una prueba fallida rompe el job. | F1 | T02 |
| NV-02-R03 | Cobertura informativa | Cada run publica la cobertura de líneas y ramas por proyecto en el resumen y como artefacto; no bloquea por sí sola. | F1 | T03 |
| NV-02-R04 | SAST | Semgrep con rulesets públicos y reglas propias versionadas; ERROR bloquea; supresiones con justificación, responsable y vencimiento; SARIF como artefacto. | F2 | T04, T05 |
| NV-02-R05 | SCA | Dependencias vulnerables directas y transitivas auditadas; High o Critical bloquea salvo excepción formal registrada. | F2 | T06 |
| NV-02-R06 | SBOM | SBOM CycloneDX generado en cada run, trazable al commit y retenido como artefacto. | F2 | T07 |
| NV-02-R07 | Detección de secretos | Gitleaks revisa todo el historial; un hallazgo bloquea; los reportes no muestran valores. | F2 | T08 |
| NV-02-R08 | Seguridad de contenedor | Trivy escanea la imagen (Critical/High corregibles bloquean) y la configuración; Dockle verifica la imagen (FATAL bloquea); la imagen se ejecuta como no root. | F3 | T09 |
| NV-02-R09 | Infraestructura como código | `terraform fmt -check` y `validate` pasan sin backend ni credenciales; Trivy revisa la configuración de Terraform. | F3 | T09, T10 |
| NV-02-R10 | Línea base de mutación | Stryker.NET se ejecuta sobre Domain y Application de forma programada y manual; el score y los mutantes sobrevivientes se documentan antes de fijar un umbral. | F4 | T11 |
| NV-02-R11 | Quality gate y protección de rama | Un job agrega los resultados y publica el resumen; la configuración de checks requeridos queda documentada y aplicada cuando la decisión de visibilidad lo permita. | F5 | T12, T14 |
| NV-02-R12 | Hallazgos, excepciones y evidencia | Todo hallazgo tiene severidad, origen, responsable y estado; las excepciones son explícitas; `evidence.md` registra run, commit y resultados. | F5 | T15 |
| NV-02-R13 | Costo $0 de la CI | Herramientas gratuitas; consumo de minutos de Actions estimado y acotado (jobs pesados programados); sin servicios de pago. | F5 | T11, T15 |

### Requisitos trasladados desde 1.0.0-borrador

| ID anterior | Tema | Destino |
| --- | --- | --- |
| R01–R03 | Suites unitaria, de integración y de seguridad | Cubiertas por las specs funcionales; aquí se ejecutan en CI (NV-02-R02) |
| R07 | Mutación | NV-02-R10 |
| R08 | Contract testing | Futuro (consumidor móvil) |
| R09 | Performance (NBomber) | NV-09 |
| R10 | DAST (OWASP ZAP) | NV-09 |
| R11 | REST fuzzing (RESTler) | NV-09 |
| R13 | Firma y procedencia (Cosign) | NV-09 |
| R04–R06, R12, R14–R16 | SAST, SCA, SBOM, contenedor, gates, CI y evidencia | NV-02-R04 a R09, R11 y R12 |

## 9. Seguridad

- La CI se ejecuta sin secretos; los pull requests no tienen acceso a credenciales.
- Ningún job escribe en el repositorio ni en registries.
- Los reportes se revisan antes de compartirse: pueden incluir rutas y fragmentos de código.
- Un control obligatorio que falla o no se ejecuta no se reporta como aprobado ni se compensa con otro.
- Las supresiones de Semgrep, Gitleaks o Trivy requieren justificación, responsable y vencimiento.
- La protección de rama no admite bypass silencioso; cualquier excepción se registra.

## 10. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| GitHub Actions (repositorio privado, plan Free) | $0 hasta 2,000 minutos Linux/mes | Estimación ~8–12 minutos por run; mutación solo semanal o manual. Sin método de pago no hay cobro por exceso (PENDIENTE DE VERIFICAR en la facturación de la organización) |
| GitHub Actions (repositorio público) | $0 sin límite de minutos | Depende de M-BE-050 |
| Semgrep CE, Gitleaks, Trivy, Dockle, CycloneDX, Stryker.NET | $0 (open source) | Ejecutados como herramientas o imágenes en el runner |
| SonarCloud / code scanning SARIF | $0 solo en repositorio público | Si sigue privado: no se usan |
| Artefactos | Incluidos en el almacenamiento gratuito | Retención de 14 días |

Si algún componente no puede demostrarse $0, la task correspondiente queda **BLOCKED**.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-02-RK01 | Ruleset no aplicado por plan Free en repositorio privado | El merge no se bloquea aunque la CI falle | Decisión M-BE-050; mientras tanto, revisión humana de los checks antes de aprobar |
| NV-02-RK02 | Minutos de Actions agotados | La CI deja de correr | Mutación programada, `concurrency` y filtros |
| NV-02-RK03 | Falsos positivos de scanners | Bloqueos sin causa | Triage y supresiones justificadas con vencimiento |
| NV-02-RK04 | Secreto histórico detectado al escanear todo el historial | Exposición si el repositorio se hace público | Rotar la credencial y limpiar o aceptar con registro antes de cambiar la visibilidad |
| NV-02-RK05 | Herramienta incompatible con .NET 10 | Job inviable | Validar versión y fijarla |
| NV-02-RK06 | Acción de terceros comprometida | Ejecución de código no confiable | Versiones fijadas (SHA cuando sea viable) y permisos mínimos |
| NV-02-RK07 | Workflows duplicados | Consumo de minutos y confusión | Retirar `u2-linux-validation` y `u2-container-validation` |

## 12. Dependencias

- NV-01 y NV-04 a NV-08 U2 (código y pruebas a validar).
- NV-03 (Dockerfile y Terraform).
- NV-09 (CD) consume el resultado de esta CI; su aprobación es independiente.
- M-BE-050 (visibilidad del repositorio) para aplicar la protección de rama.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Fases del plan | 5 |
| Tasks | 15 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 15 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETADA — PASS WITH WARNINGS** |

## 14. Aprobación pendiente

Para ejecutar se requiere la autorización humana «EJECUTA» que registre aprobador, fecha, versión `1.1.0-u2` de `spec.md`, `plan.md` y `task.md`, tasks autorizadas y rutas permitidas (`.github/workflows/`, `.semgrep/`, archivos de configuración de herramientas y `specs/nv-02-devsecops/`). La configuración del ruleset y la visibilidad del repositorio son acciones humanas en GitHub.
