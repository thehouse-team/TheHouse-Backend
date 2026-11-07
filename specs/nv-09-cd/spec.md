# NV-09 — Entrega continua (CD) U2

| Campo | Valor |
| --- | --- |
| ID | NV-09 |
| Documento | `spec.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA» T01–T14; decisiones de la §7 aceptadas) |
| Estado de ejecución | **🟡 CD EN PRODUCCIÓN (run `42180858665`) — falta T13; T15–T19 sin autorizar** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks | `task.md` 1.1.0-u2 |
| Precondición | NV-02 1.1.0-u2 completada (CI en verde y ruleset de `main` activo); NV-03 desplegada en Azure; M-BE-036, M-BE-038, M-BE-043, M-BE-044 y M-BE-049 |
| Relación | Consume el resultado de la CI de NV-02. Aporta la evidencia que falta en NV-03 (T14, T19, T22 y T25). El roadmap funcional de M-BE-023 se recorre a partir de NV-10 (M-BE-049) |

**Cambio 1.1.0-u2 (2026-11-07) — APROBADO Y AUTORIZADO por Victor Manuel Jose Jose el 2026-11-07 («okey si esta bien una vez que quedo esto me dices», respuesta al paquete propuesto: aprobación documental, «EJECUTA» de NV-08 T14, NV-07 T13, NV-01 T19, NV-09 T13, T18 y T20, NV-03 T08, T18 y T19, y autorización del seed en producción; `terraform apply` sigue requiriendo «OK PARA APPLY»):** agrega NV-09-R17 (no desplegar cambios que solo tocan documentación) y la task T20. T13 y T18 se ejecutan en este ciclo (T13 ya autorizada; T18 requiere su autorización propia).

Esta spec define la entrega continua para revisión humana. No autoriza crear workflows, identidades ni roles, cambiar Terraform, configurar GitHub, publicar, desplegar ni ejecutar seeds: la ejecución requiere la autorización humana «EJECUTA», y cualquier `terraform apply` requiere además «OK PARA APPLY».

## 1. Contexto

En la revisión académica del 2026-11-05 el profesor observó que el backend no tenía CI/CD. NV-02 resolvió la CI: hoy cada pull request hacia `main` ejecuta 7 checks obligatorios y el ruleset "Main Proteccion" bloquea el merge si alguno falla o si falta la aprobación.

La entrega sigue siendo manual:

- La imagen se publica con `u2-ghcr-publish.yml`, que exige un `workflow_dispatch` con la confirmación `PUBLISH`, no firma la imagen y no genera SBOM.
- El despliegue a Azure Container Apps se hace a mano con `az containerapp update` o Terraform desde la computadora del responsable, porque el state de Terraform es local.
- Azure ejecuta el digest `sha256:15f196af…` del commit `c651886`; las correcciones posteriores (caché HTTP, observabilidad, seed controlado, CI) no están desplegadas.
- No hay smoke tests automatizados después del despliegue, ni DAST, fuzzing o performance (trasladados aquí desde NV-02 1.0.0-borrador), ni rollback.
- NV-03 tiene tasks abiertas que dependen de un despliegue trazable: T14 (handoff de digest y SBOM), T19 (smoke tests en Azure), T22 (registro en `VERSIONING.md`) y T25 (logs de consola en Log Analytics).

Hallazgos de la revisión previa a esta spec (2026-11-05):

1. El paquete `ghcr.io/thehouse-team/thehouse-backend` es público (GHCR entrega un token anónimo de lectura), así que Azure descarga la imagen sin credenciales de registry.
2. `azurerm_container_app.this` no tiene `template[0].container[0].image` en `ignore_changes`: si el CD cambia la imagen, el siguiente `terraform apply` la revertiría al digest de `terraform.tfvars`.
3. La aplicación expone OpenAPI solo en `Development` y `Testing` (`ApiConventions.ExposesOpenApi`); en `Production` no hay documento para un escaneo de API.
4. El repositorio es público (M-BE-050): los Environments con revisores obligatorios y las atestaciones de GitHub son gratuitos.
5. NBomber, previsto por la constitución para performance, es gratuito solo para uso personal; el uso por una organización requiere licencia (FAQ del README de `PragmaticFlow/NBomber`, consultado el 2026-11-05).

## 2. Objetivo

Que cada cambio integrado a `main` con la CI en verde produzca una imagen inmutable, firmada y con SBOM, verificada en un entorno efímero, y que llegue a Azure solo después de una aprobación humana, con smoke tests, DAST y rollback automático, a costo $0 y sin secretos de larga duración.

## 3. Precondiciones

1. NV-02 1.1.0-u2 completada: `ci.yml` en `main` y ruleset activo con los 7 checks requeridos.
2. El PR #3 (`fix/u2-review`) integrado a `main`.
3. Infraestructura de NV-03 en Azure: `rg-thehouse-u2`, `cae-thehouse-u2` y `ca-thehouse-u2`, con secretos y variables de ejecución administrados fuera de Terraform (M-BE-038).
4. COST-001 vigente.
5. Toda ejecución requiere la autorización humana «EJECUTA» con versión, tasks y rutas; `terraform apply` requiere «OK PARA APPLY»; el seed en Production requiere una autorización propia (M-BE-043).

## 4. Alcance U2

1. Workflow `cd.yml`, disparado cuando `CI` termina en éxito por un push a `main`, más ejecución manual para redesplegar o revertir a un digest firmado.
2. Publicación en GHCR por digest, con etiqueta `sha-<commit>` y metadatos OCI.
3. Re-escaneo con Trivy del artefacto publicado.
4. Firma keyless con Cosign y SBOM CycloneDX de la imagen como atestación.
5. Entorno efímero en el runner (imagen publicada + MongoDB + Redis) con smoke tests completos y DAST activo con OWASP ZAP.
6. Identidad federada (OIDC) de GitHub Actions hacia Azure, con permisos mínimos y definida en Terraform.
7. Aprobación manual mediante el Environment `production` de GitHub.
8. Despliegue a Azure Container Apps por digest verificado, con rollback automático si fallan los smoke tests.
9. Smoke tests en producción sin escrituras persistentes y DAST pasivo con ZAP.
10. Fuzzing REST con RESTler y performance (manuales o programados, informativos).
11. Seed de catálogo en producción como operación humana controlada.
12. Registro de cada despliegue (`VERSIONING.md` y resumen del run) y evidencia para NV-03.
13. Retiro de `u2-ghcr-publish.yml`.

## 5. Fuera de alcance

- Entornos `staging` persistentes en Azure: duplican costo; la verificación previa se hace en el entorno efímero del runner.
- Despliegues blue/green o canary con revisiones múltiples: `revision_mode` sigue en `Single`; se reevalúa después de U2.
- Ejecutar Terraform desde GitHub Actions: exigiría un backend remoto de state; el CD solo cambia la imagen y Terraform sigue siendo manual con «OK PARA APPLY».
- DAST activo, fuzzing o pruebas de carga contra producción: solo contra el entorno efímero.
- Caos y pruebas de resiliencia con fallos inyectados en Azure: futuro.
- Contract testing con PactNet: futuro (consumidor móvil).
- Ventana de evaluación con 2–2 réplicas: operación de NV-03 (M-BE-039), no del CD.
- Rotación de las credenciales de MongoDB, Upstash y JWT: operación de seguridad independiente, recomendada antes de la evaluación.

## 6. Diseño del pipeline

```text
CI (push a main, éxito)
  └─ cd.yml
       publish ──► verify-ephemeral ──► [aprobación: Environment production] ──► deploy ──► smoke-production ──► dast-baseline ──► cd-summary
                                                                                              └─ si falla ─► rollback
```

| Job | Herramienta | Qué hace | Falla si | Evidencia |
| --- | --- | --- | --- | --- |
| `publish` | Buildx, GHCR, Trivy, Cosign, CycloneDX | Construye desde el commit exacto de la CI, publica `sha-<commit>`, re-escanea el digest, firma con Cosign keyless y atestigua el SBOM | Push o digest inválido; Trivy Critical/High corregible; firma o atestación fallida | Digest, reporte Trivy, SBOM y entrada en Rekor |
| `verify-ephemeral` | Docker, MongoDB, Redis, ZAP | Ejecuta el digest publicado en `Testing` con servicios efímeros, carga el seed, corre los smoke tests completos y el escaneo activo de ZAP sobre OpenAPI | Algún smoke test falla; ZAP reporta High no excepcionado | Reporte de smoke tests y de ZAP |
| `deploy` | Environment `production`, Cosign, Azure CLI (OIDC) | Espera la aprobación, verifica la firma, guarda la imagen anterior y actualiza `ca-thehouse-u2` al digest con un sufijo de revisión trazable | Firma no verificable; la revisión no queda `Running`/`Healthy` en el tiempo límite | Revisión, imagen anterior y nueva |
| `smoke-production` | Script de smoke tests | Health, 7 endpoints sin escrituras persistentes, encabezados de seguridad y caché | Cualquier verificación falla | Reporte |
| `rollback` | Azure CLI (OIDC) | Solo si `deploy` o `smoke-production` fallan: vuelve a la imagen anterior y repite el smoke | El rollback tampoco queda sano (alerta en el resumen) | Revisión restaurada |
| `dast-baseline` | ZAP baseline | Escaneo pasivo de la URL de producción | High no excepcionado | Reporte ZAP |
| `cd-summary` | Script del workflow | Registro del despliegue: commit, digest, firma, aprobador, revisión y resultados | Algún job obligatorio no terminó en éxito | Resumen y `deployment-record.json` |

Workflow aparte `cd-extended.yml` (manual y semanal, informativo): RESTler y performance contra el entorno efímero.

Reglas comunes:

- `permissions: {}` a nivel de workflow; cada job declara solo lo que necesita (`packages: write`, `id-token: write`, `attestations: write` en `publish`; `id-token: write` en `deploy` y `rollback`).
- Acciones fijadas por SHA de commit e imágenes por versión exacta, como en NV-02.
- `concurrency: cd-production` sin cancelación: dos despliegues nunca se cruzan y uno en curso no se interrumpe.
- El job checkout usa `github.event.workflow_run.head_sha` y solo procede cuando el run de `CI` fue un `push` a `main` con conclusión `success`.
- Ningún secreto de larga duración: GHCR con `GITHUB_TOKEN`, Azure con OIDC y firma keyless.
- La ejecución manual acepta un digest solo si su firma verifica con la identidad de `cd.yml` en `main`.

## 7. Decisiones propuestas

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Dueño de la imagen | El CD es dueño de `image`: se agrega `template[0].container[0].image` a `ignore_changes`; `var.image` queda como bootstrap y conserva su validación de digest | APROBADO (M-BE-051) |
| Identidad hacia Azure | Identidad administrada asignada por el usuario `id-thehouse-u2-deploy`, con credencial federada para `repo:thehouse-team/TheHouse-Backend:environment:production`, creada en Terraform | APROBADO (M-BE-051) |
| Rol de la identidad | Mínimo privilegio con alcance al recurso `ca-thehouse-u2`: rol integrado de Container Apps si cubre la actualización, o rol personalizado con lectura y escritura de `Microsoft.App/containerApps`, lectura de revisiones y `Microsoft.App/managedEnvironments/join/action`. Nunca `Contributor` a nivel de grupo o suscripción | APROBADO (M-BE-051) |
| Aprobación | Environment `production` con Victor e Isai como revisores, "Prevent self-review" y despliegue solo desde `main` | APROBADO (M-BE-051) |
| Firma | Cosign keyless (Sigstore público, gratuito); la identidad del certificado es `cd.yml` en `main` | APROBADO (M-BE-051) |
| Smoke en producción | Sin escrituras persistentes: GET de universidades y propiedades, registro con cuerpo inválido (400), login con credenciales inexistentes (401) y logout sin token (401). El flujo completo (registro, login, logout y reuso del token) corre solo en el entorno efímero | APROBADO (M-BE-051) |
| DAST | Activo (API scan con OpenAPI) solo en el entorno efímero; pasivo (baseline) en producción | APROBADO (M-BE-051) |
| Performance | NBomber (constitución) solo si se confirma que el uso académico no requiere licencia; si no, BLOCKED y se propone k6 (AGPL-3.0, gratuito) con cambio formal de la constitución | PENDIENTE DE VERIFICAR |
| Seed en producción | Operación humana única, fuera del pipeline, con `--allow-production-seed` y autorización explícita (M-BE-043) | APROBADO (M-BE-051) |
| `VERSIONING.md` | El CD no escribe en `main` (lo impide el ruleset); el registro de cada despliegue se agrega con un pull request | APROBADO (M-BE-051) |

## 8. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-09-R01 | Disparadores, permisos y retiro del workflow anterior | `cd.yml` corre solo tras `CI` exitoso por push a `main` y de forma manual; permisos por job; acciones fijadas por SHA; `concurrency` sin cancelación; sin secretos de larga duración; `u2-ghcr-publish.yml` retirado. | F2 | T04, T08 |
| NV-09-R02 | Publicación inmutable | Imagen en GHCR con etiqueta `sha-<commit>` y metadatos OCI (fuente, revisión, versión); el digest `sha256:` se valida y se propaga a los jobs siguientes; nunca `latest`. | F2 | T05 |
| NV-09-R03 | Re-escaneo del artefacto publicado | Trivy escanea el digest publicado con el mismo criterio de NV-02 (Critical/High corregibles bloquean). | F2 | T06 |
| NV-09-R04 | Firma y SBOM | El digest se firma con Cosign keyless y lleva un SBOM CycloneDX atestiguado; `cosign verify` y `cosign verify-attestation` pasan con la identidad de `cd.yml` en `main`. | F2 | T07 |
| NV-09-R05 | Verificación funcional efímera | El digest publicado corre en el runner con MongoDB y Redis efímeros en `Testing`; los smoke tests cubren health, los 7 endpoints, registro → login → logout → reuso del token (401), rate limit (429) y revalidación con ETag (304). | F3 | T09 |
| NV-09-R06 | DAST activo efímero | ZAP API scan contra el OpenAPI del entorno efímero; High bloquea salvo excepción justificada en un archivo de reglas versionado; reporte como artefacto. | F3 | T10 |
| NV-09-R07 | Identidad federada mínima | Identidad administrada con credencial federada limitada al Environment `production` y rol de mínimo privilegio sobre `ca-thehouse-u2`, todo en Terraform; sin client secret; `apply` solo con «OK PARA APPLY». | F1 | T01 |
| NV-09-R08 | Aprobación manual | Environment `production` con revisores obligatorios, sin autoaprobación y limitado a `main`; ningún despliegue ocurre sin una aprobación registrada. | F1 | T03 |
| NV-09-R09 | Despliegue por digest verificado | Terraform no revierte la imagen desplegada por el CD; antes de desplegar se verifica la firma; el despliegue usa el digest y un sufijo de revisión con el commit; la revisión queda `Running` y `Healthy` dentro del tiempo límite. | F1, F4, F6 | T02, T11, T17 |
| NV-09-R10 | Smoke en producción sin escrituras | Después del despliegue: health, los 7 endpoints sin escrituras persistentes, HSTS, `nosniff` y caché; el arranque en frío (0 réplicas) se tolera con reintentos acotados. | F4 | T12 |
| NV-09-R11 | Rollback automático | Si `deploy` o `smoke-production` fallan, se restaura la imagen anterior, se repiten los smoke tests y el run termina en fallo; el camino se prueba una vez con una falla controlada. | F4 | T13 |
| NV-09-R12 | DAST pasivo en producción | ZAP baseline contra la URL pública después del smoke; High bloquea el cierre del run salvo excepción justificada. | F4 | T14 |
| NV-09-R13 | Fuzzing y performance | `cd-extended.yml` (manual y semanal) ejecuta RESTler y la prueba de performance contra el entorno efímero; resultados y presupuesto de latencia p95 registrados; informativo hasta fijar umbrales con evidencia. | F5 | T15, T16 |
| NV-09-R14 | Seed controlado en producción | El catálogo de universidades y propiedades se carga una sola vez en producción con `--allow-production-seed`, autorización explícita y evidencia sin datos sensibles. | F6 | T18 |
| NV-09-R15 | Registro y trazabilidad | Cada despliegue deja commit, digest, firma, aprobador, revisión y resultados en el resumen y en `deployment-record.json`; `VERSIONING.md` se actualiza por pull request; la evidencia se enlaza a NV-03 T14, T19, T22 y T25. | F6 | T17, T19 |
| NV-09-R16 | Costo $0 y evidencia | Todos los componentes con costo $0 demostrado; `evidence.md` con runs, hallazgos, excepciones y revisión de seguridad y coherencia. | F6 | T19 |
| NV-09-R17 | Omitir despliegues de solo documentación | Si los archivos cambiados respecto al commit anterior de `main` son únicamente `specs/`, `*.md`, `.opencode/` o `docs/`, `cd.yml` termina sin publicar ni desplegar y lo registra en el resumen; cualquier otro archivo mantiene el flujo completo; la ejecución manual siempre despliega. | F2 | T20 |

### Requisitos recibidos de NV-02 1.0.0-borrador

| ID anterior | Tema | Destino en NV-09 |
| --- | --- | --- |
| R09 | Performance (NBomber) | NV-09-R13 |
| R10 | DAST (OWASP ZAP) | NV-09-R06 y NV-09-R12 |
| R11 | REST fuzzing (RESTler) | NV-09-R13 |
| R13 | Firma y procedencia (Cosign) | NV-09-R04 |

## 9. Seguridad

- **Sin secretos de larga duración**: GHCR con `GITHUB_TOKEN` del job, Azure con OIDC, firma keyless. Los identificadores de Azure (client, tenant y suscripción) son variables del Environment, no secretos.
- **Credencial federada acotada**: el *subject* solo acepta tokens del Environment `production`; un pull request o una rama no pueden obtener acceso a Azure.
- **Cadena de suministro**: solo se despliega un digest firmado por `cd.yml` en `main`; la verificación ocurre en el job de despliegue, no solo al publicar.
- **`workflow_run`**: el workflow que corre es siempre el de `main`; se valida que el evento origen sea `push` a `main` y se hace checkout del `head_sha` exacto.
- **Entradas manuales**: `image_digest` se valida con `^sha256:[0-9a-f]{64}$` y todas las entradas pasan a los scripts por variables de entorno, nunca interpoladas con `${{ }}` dentro de `run`, para evitar inyección de comandos.
- **Producción**: sin pruebas activas ni escrituras persistentes; el entorno efímero usa una llave JWT aleatoria por run que nunca sale del runner.
- **Evidencia**: los reportes no incluyen tokens, cadenas de conexión ni datos personales; los usuarios de prueba solo existen en el entorno efímero.
- **Transparencia**: la firma keyless registra en Rekor (público) la identidad del workflow; el repositorio ya es público, así que no expone información nueva.
- Un control obligatorio que falla o no se ejecuta no se reporta como aprobado.

## 10. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| GitHub Actions y Environments con revisores | $0 | Repositorio público (M-BE-050) |
| GHCR | $0 | Paquete público |
| Cosign / Sigstore público, atestaciones | $0 | Repositorio público |
| Identidad administrada, credencial federada y rol | $0 | Recursos de identidad sin cargo |
| Azure Container Apps (nuevas revisiones) | $0 dentro de la concesión gratuita mensual | Escala 0–2 (M-BE-039); se verifica en Cost Management |
| Log Analytics | $0 dentro del tope diario de 0.1 GB | NV-03 1.2.0-u2 |
| OWASP ZAP, RESTler, Trivy, MongoDB y Redis efímeros | $0 (open source, en el runner) | — |
| NBomber | $0 solo si el uso académico no requiere licencia | PENDIENTE DE VERIFICAR; si no, BLOCKED |

Si algún componente no puede demostrarse $0, la task correspondiente queda **BLOCKED**.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-09-RK01 | Drift entre Terraform y la imagen desplegada | Un `apply` revierte a una versión vieja | `ignore_changes` sobre la imagen (T02) y `terraform plan` sin cambios como evidencia |
| NV-09-RK02 | Rol insuficiente o excesivo | Despliegue fallido o privilegio de más | Rol con alcance al recurso; prueba de despliegue y de denegación fuera del alcance |
| NV-09-RK03 | Sigstore no disponible | No se puede firmar o verificar | El despliegue se detiene; nunca se omite la verificación |
| NV-09-RK04 | Falsos positivos de ZAP | Bloqueos sin causa | Archivo de reglas versionado con justificación, responsable y vencimiento |
| NV-09-RK05 | Arranque en frío con 0 réplicas | Smoke falla por tiempo | Reintentos acotados contra `/health/ready` antes de evaluar |
| NV-09-RK06 | Rate limit de login durante el smoke | 429 inesperado | Un solo intento de login en producción; el 429 se prueba en el entorno efímero |
| NV-09-RK07 | Rollback también falla | Servicio caído | Alerta en el resumen, procedimiento manual documentado y digest anterior registrado |
| NV-09-RK08 | Licencia de NBomber | Incumplimiento de licencia | Verificar antes de usar; alternativa k6 con decisión formal |
| NV-09-RK09 | RESTler sin imagen oficial | Job lento o inviable | Compilar desde una etiqueta fijada (`v9.3.1`), solo en `cd-extended.yml`, informativo |
| NV-09-RK10 | Aprobadores no disponibles cerca de la evaluación | Despliegue detenido | Dos revisores (Victor e Isai) y desplegar con anticipación al 3 de diciembre |

## 12. Dependencias

- NV-02 1.1.0-u2 (CI y ruleset) y la integración del PR #3.
- NV-03 (Dockerfile, Terraform, Container App y Log Analytics).
- M-BE-036 (digest), M-BE-038 (secretos fuera de Terraform), M-BE-043 (seed en Production), M-BE-044 (publicación desde `main`) y M-BE-049 (NV-09 como CD).

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 17 |
| Fases del plan | 6 |
| Tasks | 20 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 13 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 CD EN PRODUCCIÓN — falta T13** |

## 14. Aprobación pendiente

Para ejecutar se requiere la autorización humana «EJECUTA» que registre aprobador, fecha, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`, tasks autorizadas y rutas permitidas (`.github/workflows/`, `.github/scripts/`, `.github/perf/`, `.zap/`, `infra/terraform/`, `VERSIONING.md` y `specs/nv-09-cd/`). Además:

- Crear `TheHouse.PerformanceTests/` con NBomber (T16) requiere el levantamiento explícito de las acciones protegidas de `AGENTS.md` (`dotnet new`, `dotnet sln add` y paquetes NuGet); no se agrega `tests/`.

- `terraform apply` de T01 y T02 requiere «OK PARA APPLY».
- El Environment `production` y sus revisores se configuran por un humano en GitHub (T03).
- El seed en producción (T18) requiere una autorización propia.
- Las decisiones de la §7 se registran en `memoria.md` al aprobarse.
