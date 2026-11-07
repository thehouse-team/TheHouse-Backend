# NV-09 — Plan de entrega continua (CD) U2

| Campo | Valor |
| --- | --- |
| ID | NV-09 |
| Documento | `plan.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA» T01–T14; decisiones de la §7 aceptadas) |
| Estado de ejecución | **🟡 CD EN PRODUCCIÓN (run `42180858665`) — falta T13; T15–T19 sin autorizar** |
| Spec | `spec.md` 1.1.0-u2 |
| Tasks | `task.md` 1.1.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

**Cambio 1.1.0-u2 (2026-11-07) — PENDIENTE DE APROBACIÓN Y «EJECUTA»:** agrega NV-09-R17 (no desplegar cambios que solo tocan documentación) y la task T20. T13 y T18 se ejecutan en este ciclo (T13 ya autorizada; T18 requiere su autorización propia).

Plan preparado para revisión. No crea workflows, identidades ni roles, no cambia Terraform ni la configuración de GitHub, y no publica ni despliega hasta la autorización «EJECUTA».

## 1. Enfoque

La CD se construye de afuera hacia adentro: primero la identidad y la aprobación (sin ellas no hay despliegue seguro), luego la publicación firmada, después la verificación en un entorno efímero, y al final el despliegue con smoke tests, rollback y DAST pasivo. Las pruebas pesadas (fuzzing y performance) van en un workflow aparte para no alargar cada despliegue.

Principios:

1. Se despliega exactamente lo que la CI validó: el mismo commit, publicado una vez y referido por digest.
2. Nada llega a producción sin firma verificada y aprobación humana.
3. Lo destructivo (DAST activo, fuzzing, carga, flujos que escriben) corre solo contra el entorno efímero.
4. Un despliegue que no se puede verificar se revierte.
5. Sin secretos de larga duración; cada herramienta es gratuita o la task queda BLOCKED.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Identidad, Terraform y aprobación | R07, R08, R09 | T01, T02, T03 | Identidad federada con rol mínimo, imagen fuera del control de Terraform y Environment `production` |
| F2 | Publicación y cadena de suministro | R01, R02, R03, R04 | T04, T05, T06, T07, T08 | `cd.yml` con `publish` (digest, Trivy, Cosign y SBOM); `u2-ghcr-publish.yml` retirado |
| F3 | Verificación efímera | R05, R06 | T09, T10 | `verify-ephemeral` con smoke tests completos y ZAP API scan |
| F4 | Despliegue y verificación en producción | R09, R10, R11, R12 | T11, T12, T13, T14 | `deploy`, `smoke-production`, `rollback` y `dast-baseline` |
| F5 | Pruebas extendidas | R13 | T15, T16 | `cd-extended.yml` con RESTler y performance |
| F6 | Primer despliegue, seed y evidencia | R09, R14, R15, R16 | T17, T18, T19 | Versión actual desplegada, catálogo cargado, `VERSIONING.md` y `evidence.md` |

## 3. Detalle de fases

### F1 — Identidad, Terraform y aprobación

**Requisitos:** NV-09-R07, NV-09-R08, NV-09-R09. **Tasks:** T01, T02, T03.

**Entregables:**

- `infra/terraform/cd_identity.tf`: `azurerm_user_assigned_identity` (`id-thehouse-u2-deploy`), `azurerm_federated_identity_credential` (issuer `https://token.actions.githubusercontent.com`, subject `repo:thehouse-team/TheHouse-Backend:environment:production`, audience `api://AzureADTokenExchange`) y `azurerm_role_assignment` con alcance a `ca-thehouse-u2`.
- Antes de elegir el rol se consulta `az role definition list` para confirmar si existe un rol integrado de Container Apps que permita actualizar la app; si no, rol personalizado (`azurerm_role_definition`) con solo las acciones necesarias.
- Outputs no sensibles: `client_id` de la identidad, `tenant_id` y `subscription_id`, para cargarlos como variables del Environment.
- `ignore_changes` de `azurerm_container_app.this` con `template[0].container[0].image`.
- Guía para el Environment `production`: revisores Victor e Isai, "Prevent self-review", rama `main` únicamente y variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`.

**Criterios de salida:** `terraform plan` revisado y aplicado solo con «OK PARA APPLY»; un segundo `plan` sin cambios; la API de GitHub muestra el Environment con revisores y la política de ramas.

### F2 — Publicación y cadena de suministro

**Requisitos:** NV-09-R01, NV-09-R02, NV-09-R03, NV-09-R04. **Tasks:** T04, T05, T06, T07, T08.

**Entregables:**

- `.github/workflows/cd.yml`: `on.workflow_run` (`workflows: [CI]`, `types: [completed]`, `branches: [main]`) y `workflow_dispatch` con entradas `image_digest` (redespliegue o rollback manual) y `simulate_smoke_failure` (solo para probar el rollback).
- Condición del primer job: `workflow_run.event == 'push'`, `conclusion == 'success'` y `head_branch == 'main'`.
- Job `publish`: Buildx desde `head_sha`, push a `ghcr.io/thehouse-team/thehouse-backend:sha-<commit>` con etiquetas OCI, validación del digest (patrón de `u2-ghcr-publish.yml`), Trivy sobre el digest, SBOM CycloneDX de la imagen, `cosign sign` y `cosign attest --type cyclonedx`.
- Versiones verificadas el 2026-11-05 (se fijan por SHA al implementar): `docker/setup-buildx-action` v4.4.1, `docker/login-action` v4.8.0, `docker/build-push-action` v7.4.0, `sigstore/cosign-installer` v4.1.3 (Cosign v3.1.4), `azure/login` v3.1.0, Trivy 0.76.2 (igual que NV-02), ZAP `ghcr.io/zaproxy/zaproxy:2.17.0`.
- Retiro de `u2-ghcr-publish.yml` y actualización de `VERSIONING.md` para describir el flujo nuevo.

**Criterios de salida:** un push a `main` produce un digest firmado; `cosign verify` y `cosign verify-attestation` pasan desde una máquina limpia.

### F3 — Verificación efímera

**Requisitos:** NV-09-R05, NV-09-R06. **Tasks:** T09, T10.

**Entregables:**

- Job `verify-ephemeral` con `services` `mongo` y `redis` (imágenes fijadas por versión), el digest publicado en `ASPNETCORE_ENVIRONMENT=Testing`, configuración por variables de entorno y una llave JWT aleatoria generada en el run.
- Seed de universidades y propiedades (permitido en `Testing` sin la bandera de producción).
- `.github/scripts/smoke_test.py` (solo librería estándar de Python, como los scripts de NV-02) con dos modos: `full` (efímero) y `readonly` (producción).
- ZAP API scan con la imagen de ZAP (no la action, que necesita permiso para crear issues) contra `/openapi/v1.json`, con `.zap/rules.tsv` versionado.

**Criterios de salida:** el modo `full` pasa; una regresión controlada en local hace fallar el smoke; el reporte de ZAP queda como artefacto y sin High abiertos.

### F4 — Despliegue y verificación en producción

**Requisitos:** NV-09-R09, NV-09-R10, NV-09-R11, NV-09-R12. **Tasks:** T11, T12, T13, T14.

**Entregables:**

- Job `deploy` con `environment: production`: `cosign verify` del digest, `azure/login` con OIDC, lectura de la imagen actual, `az containerapp update --image <ref>@sha256:… --revision-suffix <commit7>-<run>` y espera hasta que la revisión quede `Running` y `Healthy`.
- Job `smoke-production` (`smoke_test.py --mode readonly`) con reintentos acotados contra `/health/ready` por el arranque en frío.
- Job `rollback` con `if: failure()` sobre `deploy` y `smoke-production`: restaura la imagen anterior, espera la revisión y repite el smoke.
- Job `dast-baseline` con ZAP baseline contra la URL pública.
- Job `cd-summary`: tabla en el resumen y `deployment-record.json` como artefacto.

**Criterios de salida:** un despliegue aprobado termina en verde; el rollback se prueba una vez con `simulate_smoke_failure` y deja la versión anterior sana.

### F5 — Pruebas extendidas

**Requisitos:** NV-09-R13. **Tasks:** T15, T16.

**Entregables:**

- `.github/workflows/cd-extended.yml` (manual y semanal): levanta el mismo entorno efímero que F3.
- RESTler compilado desde `v9.3.1`, en modo *test* y luego *fuzz-lean*, contra el OpenAPI efímero; reporte de bugs como artefacto.
- Performance: verificar primero la licencia de NBomber para uso académico. Si se confirma, proyecto `TheHouse.PerformanceTests/` en la raíz (mismo patrón que las otras suites), lo que requiere el levantamiento explícito de las acciones protegidas de `AGENTS.md` (`dotnet new`, `dotnet sln add`, paquetes NuGet); si no, la task queda BLOCKED y se propone k6 v2.3.0 con un script en `.github/perf/`, sin proyecto nuevo, y decisión formal. Escenarios: listado de universidades y de propiedades, detalle y login; presupuesto propuesto p95 < 500 ms en el runner.

**Criterios de salida:** reportes registrados; los hallazgos se triagean y los umbrales solo se fijan con evidencia.

### F6 — Primer despliegue, seed y evidencia

**Requisitos:** NV-09-R09, NV-09-R14, NV-09-R15, NV-09-R16. **Tasks:** T17, T18, T19.

**Entregables:**

- Primer despliegue real de `main` (con el PR #3 integrado) por el pipeline, con aprobación registrada.
- Seed en producción como operación humana: ejecución única con `--seed-universities --seed-properties --allow-production-seed` y las banderas `UniversitySeed__Enabled` y `PropertySeed__Enabled` activadas solo durante la operación. El mecanismo (Container Apps Job manual o `az containerapp exec`) se decide en la task según su costo.
- `VERSIONING.md` actualizado por pull request con el registro del despliegue.
- Evidencia para NV-03: T14 (digest y SBOM), T19 (smoke en Azure), T22 (registro) y T25 (logs de consola de la revisión nueva).
- `evidence.md` con runs, hallazgos, excepciones, costo y revisión final.

**Criterios de salida:** la versión de `main` corre en Azure por digest firmado; catálogo cargado; evidencia revisada con resultado PASS, PASS WITH WARNINGS o BLOCKED.

## 4. Dependencias entre tasks

```text
F1 Identidad/Terraform/aprobación
  ↓
F2 Publicación y cadena de suministro
  ↓
F3 Verificación efímera ──► F5 Pruebas extendidas
  ↓
F4 Despliegue y producción
  ↓
F6 Primer despliegue, seed y evidencia
```

- T02 depende de «OK PARA APPLY» (puede ir junto con T01).
- T03 depende de T01 (valores de las variables del Environment).
- T05 depende de T04.
- T06 y T07 dependen de T05.
- T08 depende de T05 y T07.
- T09 depende de T05.
- T10 depende de T09.
- T11 depende de T01, T02, T03, T07 y T10.
- T12 depende de T11.
- T13 depende de T11 y T12.
- T14 depende de T12.
- T15 y T16 dependen de T09.
- T17 depende de T11 a T14 y de la integración del PR #3.
- T18 depende de T17.
- T19 depende de T17 y T18.

## 5. Estrategia de verificación

| Control | Verificación | Evidencia |
| --- | --- | --- |
| Identidad federada | Login OIDC exitoso desde el Environment; un intento desde otra rama falla | Logs del run |
| Rol mínimo | El despliegue funciona y la identidad no puede modificar otro recurso del grupo | Salida de `az` |
| Firma y SBOM | `cosign verify` y `verify-attestation` desde fuera del workflow | Salida de Cosign |
| Smoke tests | Modo `full` en efímero y `readonly` en producción | Reportes |
| Rollback | Falla controlada con `simulate_smoke_failure` | Run con revisión restaurada |
| DAST | ZAP API scan (efímero) y baseline (producción) | Reportes ZAP |
| Drift | `terraform plan` sin cambios después de un despliegue del CD | Salida del plan |

## 6. Seguridad

- Permisos por job; `permissions: {}` a nivel de workflow.
- Credencial federada solo para el Environment `production`.
- Verificación de la firma inmediatamente antes de desplegar.
- Producción sin pruebas activas ni escrituras persistentes.
- Reportes revisados antes de compartirse; sin tokens, cadenas de conexión ni PII.

## 7. Costo

| Concepto | Costo esperado | Evidencia requerida |
| --- | --- | --- |
| Minutos de Actions, Environments y atestaciones | $0 (repositorio público) | Duración por run |
| GHCR y Sigstore | $0 | Paquete público; entrada en Rekor |
| Identidad y rol en Azure | $0 | Inventario del grupo de recursos |
| Revisiones de Container Apps y Log Analytics | $0 dentro de la concesión y del tope diario | Cost Management con fecha |
| NBomber | $0 solo con licencia confirmada | Respuesta del proveedor o licencia; si no, BLOCKED |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación humana de 1.0.0-u2 | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas | BLOCKED |
| G0-Apply | T01 y T02 | «OK PARA APPLY» con plan revisado | No aplicar |
| G1 | Fin F1 | Identidad, rol, `ignore_changes` y Environment verificados | No avanzar |
| G2 | Fin F2 | Digest firmado y verificable | No avanzar |
| G3 | Fin F3 | Smoke completo y ZAP sin High | No desplegar |
| G4 | Fin F4 | Despliegue aprobado en verde y rollback probado | No cerrar |
| G5 | Fin F5 | Reportes extendidos registrados | Cierre con advertencia |
| G6 | Fin F6 | Versión actual en Azure, seed y evidencia revisada | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F2 | T04, T08 |
| R02 | F2 | T05 |
| R03 | F2 | T06 |
| R04 | F2 | T07 |
| R05 | F3 | T09 |
| R06 | F3 | T10 |
| R07 | F1 | T01 |
| R08 | F1 | T03 |
| R09 | F1, F4, F6 | T02, T11, T17 |
| R10 | F4 | T12 |
| R11 | F4 | T13 |
| R12 | F4 | T14 |
| R13 | F5 | T15, T16 |
| R14 | F6 | T18 |
| R15 | F6 | T17, T19 |
| R16 | F6 | T19 |
| R17 | F2 | T20 |

## 10. No ejecución

Este plan no autoriza crear workflows, identidades ni roles, ejecutar `terraform apply`, configurar GitHub, publicar, desplegar ni ejecutar seeds. Estado de ejecución: T01–T14 autorizadas con «EJECUTA» el 2026-11-05; `terraform apply` sigue requiriendo «OK PARA APPLY»; avance en `task.md` y `evidence.md`.
