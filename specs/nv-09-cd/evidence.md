# NV-09 — Evidencia de entrega continua (CD) U2

| Campo | Valor |
| --- | --- |
| ID | NV-09 |
| Documento | `evidence.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-05 |
| Autorización | «EJECUTA NV-09 1.0.0-u2, tasks T01–T14» — Victor Manuel Jose Jose, 2026-11-05 (M-BE-051) |
| Rama | `feat/nv-09-cd` |
| Resultado | **🟡 EN EJECUCIÓN**: código listo y verificación efímera en verde; T01 y T03 completadas; pendiente el primer run real de `cd.yml` en `main` |

Esta evidencia no incluye valores de secretos, cadenas de conexión, llaves ni identificadores de suscripción. No se leyeron ni mostraron `terraform.tfvars` ni `terraform.tfstate`.

## 1. Archivos creados o modificados

| Archivo | Task | Cambio |
| --- | --- | --- |
| `infra/terraform/cd_identity.tf` | T01 | Identidad administrada, credencial federada, rol personalizado y dos asignaciones |
| `infra/terraform/variables.tf` | T01 | `cd_identity_name`, `github_repository` y `github_environment` (validado: solo `production`) |
| `infra/terraform/outputs.tf` | T01, T03 | `cd_azure_client_id`, `cd_azure_tenant_id` y `cd_azure_subscription_id` (no son secretos) |
| `infra/terraform/main.tf` | T02 | `template[0].container[0].image` en `ignore_changes` |
| `.github/workflows/cd.yml` | T04–T07, T09–T14 | Workflow CD completo |
| `.github/scripts/smoke_test.py` | T09, T12, T13 | Smoke tests `full` y `readonly`, falla controlada |
| `.github/scripts/ephemeral_env.sh` | T09, T10 | Entorno efímero en `Testing` |
| `.github/scripts/zap_gate.py` | T10, T14 | Gate de ZAP: High bloquea; tabla en el resumen |
| `.zap/rules.tsv` | T10, T14 | Reglas de ZAP; sin excepciones |
| `.github/workflows/u2-ghcr-publish.yml` | T08 | Retirado |
| `VERSIONING.md` | T08 | Publicación y despliegue por `cd.yml`, verificación con Cosign, visibilidad pública del paquete y bitácora de despliegues |

## 2. Versiones fijadas (consultadas el 2026-11-05)

| Componente | Versión | Fijación |
| --- | --- | --- |
| `actions/checkout` | v7.0.1 | SHA `3d3c42e5…` |
| `actions/upload-artifact` | v7.0.2 | SHA `cf430e03…` |
| `docker/setup-buildx-action` | v4.4.1 | SHA `f87e5991…` |
| `docker/login-action` | v4.6.0 | SHA `dbcb8138…` (v4.8.0 salió el mismo día; se evita una release tan reciente) |
| `docker/build-push-action` | v7.4.0 | SHA `c3c9e263…` |
| `sigstore/cosign-installer` | v4.1.2 | SHA `6f9f1778…`, instala Cosign **v3.1.4** |
| `azure/login` | v3.1.0 | SHA `a641126d…` |
| Trivy | 0.76.2 | Imagen `aquasec/trivy:0.76.2` (igual que NV-02) |
| OWASP ZAP | 2.17.0 | Imagen `ghcr.io/zaproxy/zaproxy:2.17.0` (sin la action, que pide permiso para crear issues) |
| MongoDB efímero | 7.0.45 | Ver §5 y M-BE-053 |
| Redis efímero | 7.4.12 | — |

## 3. T01 / T02 — Terraform (plan revisado, sin apply)

`terraform fmt -check`, `validate` y `plan` el 2026-11-05 con el state local:

```text
Plan: 5 to add, 0 to change, 0 to destroy.
```

| Recurso | Detalle |
| --- | --- |
| `azurerm_user_assigned_identity.cd` | `id-thehouse-u2-deploy`, `eastus2`, `rg-thehouse-u2` |
| `azurerm_federated_identity_credential.github_production` | issuer `https://token.actions.githubusercontent.com`, subject `repo:thehouse-team/TheHouse-Backend:environment:production`, audience `api://AzureADTokenExchange` |
| `azurerm_role_definition.cd_deployer` | `TheHouse CD Deployer (rg-thehouse-u2)`, asignable solo dentro del grupo |
| `azurerm_role_assignment.cd_container_app` | Alcance `ca-thehouse-u2` |
| `azurerm_role_assignment.cd_environment` | Alcance `cae-thehouse-u2` (`join`, requerido para actualizar la app) |

- La Container App **no cambia** en el plan: agregar la imagen a `ignore_changes` no genera diferencias.
- Rol integrado descartado: `Container Apps Contributor` incluye `*/delete` y `*/action` (que abarca `listSecrets`). El rol personalizado no puede borrar la app ni leer sus secretos (M-BE-052). Las 8 acciones se validaron contra `az provider operation show --namespace Microsoft.App`.
- **Apply** autorizado por Victor con «OK PARA APPLY» (2026-11-06):
  1. Primer intento: se creó `azurerm_role_definition.cd_deployer` y la identidad falló con `MissingSubscriptionRegistration` (el proveedor `Microsoft.ManagedIdentity` no estaba registrado en la suscripción).
  2. Con autorización explícita de Victor se ejecutó `az provider register --namespace Microsoft.ManagedIdentity --wait` → `Registered` (sin costo ni recursos nuevos).
  3. Plan nuevo: `4 to add, 0 to change, 0 to destroy`; apply: `Resources: 4 added, 0 changed, 0 destroyed`.
  4. `terraform plan -detailed-exitcode` posterior: código 0, *No changes*.
- **Inventario verificado con `az`:**
  - Grupo `rg-thehouse-u2`: `cae-thehouse-u2`, `ca-thehouse-u2`, `log-thehouse-u2` e `id-thehouse-u2-deploy` (nuevo).
  - Credencial federada `github-production`: issuer `https://token.actions.githubusercontent.com`, subject `repo:thehouse-team/TheHouse-Backend:environment:production`, audience `api://AzureADTokenExchange`.
  - Asignaciones de la identidad: solo `TheHouse CD Deployer (rg-thehouse-u2)` sobre `ca-thehouse-u2` y sobre `cae-thehouse-u2`; ninguna a nivel de grupo ni de suscripción.
  - Acciones del rol: las 8 listadas en `cd_identity.tf`; sin `delete` ni `listSecrets`.
- T01 completada. T02 queda abierta hasta repetir el `plan` sin cambios después del primer despliegue del CD.

## 3.1 T03 — Environment `production` (verificado con la API de GitHub, 2026-11-06)

Configurado por Victor en la interfaz de GitHub; consulta `gh api repos/thehouse-team/TheHouse-Backend/environments/production`.

| Campo | Valor verificado |
| --- | --- |
| `required_reviewers` | `IsaiAxel` y `vicmanujo` |
| `prevent_self_review` | `true` |
| `can_admins_bypass` | `false` |
| Política de ramas | personalizada: solo `main` |
| Variables | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (identificadores, no secretos; coinciden con los outputs `cd_azure_*` de Terraform) |

La primera verificación encontró el Environment sin revisores y con bypass de administradores; Victor lo corrigió y la segunda verificación pasó.

## 4. Validación local (2026-11-05)

- YAML de `cd.yml` válido; todas las acciones fijadas por SHA de 40 caracteres.
- `py_compile` de los scripts de Python y `bash -n` de `ephemeral_env.sh`.
- `smoke_test.py --mode readonly` contra la API desplegada hoy (digest `15f196af…`, commit `c651886`): health, 404 de detalle inexistente, HSTS, registro inválido 400, login inexistente 401 y logout sin token 401 en PASS. Los únicos FAIL fueron ETag/`Cache-Control`, **esperados** porque esa versión es anterior a la caché HTTP (NV-07/NV-08 1.1.0-u2); confirman que el smoke detecta una versión vieja. La primera ejecución expuso un fallo del script por arranque en frío (`/health/live` antes de `/health/ready`), corregido.

## 5. T09 / T10 — Verificación efímera en GitHub Actions

Se validó con un workflow **temporal** (`nv09-ephemeral-check.yml`, solo en `feat/nv-09-cd`, con imagen construida en el runner y sin publicar) que ejecuta los mismos pasos y scripts que el job `verify-ephemeral` de `cd.yml`. El workflow temporal se retiró antes del pull request.

| Run | Resultado | Causa y corrección |
| --- | --- | --- |
| `42170097726` | ❌ | `mongo:8.0.34` no arranca: "Linux kernel versions 6.19 and newer has a known incompatibility" → `mongo:7.0.45` (M-BE-053) |
| `42170260851` | ❌ | Config JWT inválida: la llave se lee de `JWT_SIGNING_KEY`, no de `Jwt__SigningKey` → variable corregida |
| `42170431608` | ❌ | La API redirige a HTTPS fuera de `/health` → el entorno simula el ingress con `Proxy__Enabled` y `X-Forwarded-Proto` |
| `42170706285` | ❌ | `RequireHeaderSymmetry`: sin `X-Forwarded-For` la API ignora `X-Forwarded-Proto` → ambos encabezados (IP de documentación `198.51.100.10`) |
| **`42171022743`** | ✅ | Ver abajo |

### Smoke `full` (run `42171022743`): 22 PASS, 0 FAIL, 1 WARN

- Seed de Testing: 3 universidades y 6 propiedades.
- Health live/ready, listados y detalle de universidades y propiedades, 404 de inexistentes.
- `Cache-Control: public, max-age=60`, ETag y **304** con `If-None-Match` en ambos catálogos.
- Registro 201, duplicado 409, inválido 400; login 200; logout 204; **reuso del token revocado 401**; logout sin token 401.
- **Rate limit de login 429** en el intento 5 con `Retry-After: 60`.
- WARN: falta `X-Content-Type-Options: nosniff` (hallazgo H-01, §6).

### ZAP API scan (run `42171022743`): ✅ PASS — 0 High

| Riesgo | Alertas | Detalle |
| --- | --- | --- |
| High | 0 | — |
| Medium | 1 | 90022 *Application Error Disclosure* en `/openapi/v1.json`: **falso positivo**; el documento OpenAPI declara respuestas 500 cuya descripción es "Internal Server Error" (`PropertyEndpoint`/`UniversityEndpoint`) |
| Low | 4 | 10023 (mismo falso positivo), 90004 *Cross-Origin-Resource-Policy* ausente en `/openapi/v1.json`, 100001 `text/html` en rutas inexistentes de metadatos cloud (sondas de ZAP), 10021 `nosniff` ausente (H-01) |
| Informational | 2 | Respuestas 4xx esperadas (14) y contenido cacheable (catálogos públicos, por diseño) |

Límite del escaneo: los límites por endpoint de propiedades y universidades (60/ventana) son fijos, así que parte del escaneo recibe 429; los límites globales y de login se relajan solo en el perfil `scan`.

## 6. Hallazgos

| ID | Severidad | Origen | Hallazgo | Responsable | Estado |
| --- | --- | --- | --- | --- | --- |
| H-01 | Baja | Smoke y ZAP 10021 | La API no envía `X-Content-Type-Options: nosniff` | Victor | ABIERTO — requiere cambio en `TheHouse/` (ruta protegida, fuera de las rutas autorizadas); se propone para NV-01 |
| H-02 | Baja | ZAP 90004 | `Cross-Origin-Resource-Policy` ausente en `/openapi/v1.json` (solo Development/Testing) | Victor | ACEPTADO — el documento no se expone en Production |
| H-03 | Informativa | ZAP 90022/10023 | Falso positivo por el texto de las respuestas 500 documentadas | — | CERRADO (falso positivo) |

## 7. Ajustes de implementación respecto al diseño de la §6 de la spec

1. **`deploy`, `smoke-production` y `rollback` en un solo job.** La credencial federada solo acepta tokens del Environment `production`; un job de rollback separado tendría que pedir una segunda aprobación en medio de una falla. El smoke y el rollback son pasos del job `deploy` y conservan su comportamiento (NV-09-R10 y R11).
2. **Estado de la revisión.** Con escala 0–2, una revisión recién creada puede estar sin réplicas: se espera `provisioningState=Provisioned`, el smoke activa la réplica, y después se exige que no esté `Unhealthy`, `Failed` ni `Degraded`.
3. **SBOM de la imagen con Trivy** (CycloneDX) en lugar de una herramienta adicional: misma versión fijada que NV-02.
4. **Entorno efímero con `Proxy__*`** para simular el ingress TLS de Azure (ver §5).

## 8. Estado de las tasks autorizadas

| Task | Estado | Falta |
| --- | --- | --- |
| T01 | ✅ Completada | — |
| T02 | Aplicada | `plan` sin cambios después del primer despliegue del CD |
| T03 | ✅ Completada | — |
| T04–T07 | Implementadas | Primer run real de `cd.yml` en `main` |
| T08 | Implementada | Integración del pull request |
| T09, T10 | Verificadas con imagen local | Run real con el digest publicado |
| T11–T14 | Implementadas | Run real con aprobación y prueba controlada de rollback (T13) |

Ninguna task se marca como completada hasta tener evidencia del run real.
