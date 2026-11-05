# NV-03 — Evidencia Scaling Cloud U2

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `evidence.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-05 |
| Aprobador | Victor Manuel Jose Jose |
| Spec | `spec.md` 1.1.0-u2 |
| Plan | `plan.md` 1.1.0-u2 |
| Tasks | `task.md` 1.1.0-u2 |
| Alcance | Verificación de artefactos y despliegue existentes; sin implementar, aplicar, destruir ni sembrar datos |
| Resultado de evidencia | **PASS WITH WARNINGS** |
| Tasks cerradas | T13, T15, T23 |
| Tasks abiertas | 20 (19 U2 + T14 futura) |

Esta evidencia no reabre la autorización de Fase B ni autoriza `terraform apply`, `terraform destroy`, despliegues, seeds o cambios de datos. Solo registra lo que existe y puede verificarse. No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`; tampoco se imprimieron valores de secretos, cadenas de conexión o claves.

## 1. Fuentes verificadas

- `Dockerfile` y `.dockerignore`.
- `infra/terraform/main.tf`, `variables.tf`, `versions.tf`, `outputs.tf`, `COST.md` y `PRE_APPLY_GATE.md`.
- `.github/workflows/u2-ghcr-publish.yml`, `u2-container-validation.yml` y `u2-linux-validation.yml`.
- `VERSIONING.md`.
- Consultas de solo lectura a Azure Container Apps y ejecución de `terraform fmt`, `terraform validate` y `terraform plan`.

No se ejecutó `terraform apply`, `terraform destroy`, `docker push`, seed ni prueba que escriba datos.

## 2. Estado verificado en Azure

| Elemento | Valor verificado |
| --- | --- |
| Grupo de recursos | `rg-thehouse-u2` |
| Región | East US 2 (`eastus2`) |
| Entorno | `cae-thehouse-u2`, perfil Consumption, `logsDestination: null` |
| Aplicación | `ca-thehouse-u2`, estado `Succeeded` |
| URL pública | `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` |
| Revisión activa | `ca-thehouse-u2--0000008`, `Running`, `Healthy` |
| Réplicas actuales | 1 réplica activa; mínimo 0 y máximo 2 |
| Regla de escala | `http-scaler`, `concurrentRequests: 10` |
| Ingress | Externo, TLS, `allowInsecureConnections: false`, sin sticky sessions |
| Imagen desplegada | `ghcr.io/thehouse-team/thehouse-backend@sha256:15f196af9c32fc843cf1723b4615f896668b0368b292e1246807ab0d03194775` |
| Commit de la imagen | `c6518862788407fe678c1a524ad4d4b764dc6a71`, según `COST.md` y `Observability__ServiceVersion` |
| HEAD del repositorio | `7f26eba`; no está desplegado |
| Recursos en el grupo | Solo `cae-thehouse-u2` y `ca-thehouse-u2` |
| Health checks | `GET /health/live` y `GET /health/ready` respondieron HTTP 200 por HTTPS |
| HSTS | `strict-transport-security: max-age=2592000` |
| Variables de ejecución | Nombres de MongoDB, Redis y JWT presentes; valores no inspeccionados |

La imagen desplegada es anterior al HEAD actual. Por tanto, la evidencia de Azure corresponde a `c651886`, no a los cambios posteriores del repositorio.

## 3. Comandos y resultados

Consultas de solo lectura:

```text
az containerapp show -n ca-thehouse-u2 -g rg-thehouse-u2
az containerapp env show -n cae-thehouse-u2 -g rg-thehouse-u2
az containerapp revision list -n ca-thehouse-u2 -g rg-thehouse-u2
az containerapp replica list -n ca-thehouse-u2 -g rg-thehouse-u2
az resource list -g rg-thehouse-u2
```

Validación de infraestructura:

```text
terraform fmt -check -recursive       -> exit 0
terraform validate -no-color          -> Success! The configuration is valid.
terraform plan -input=false -no-color -> plan guardado fuera del repositorio
```

El plan contiene 3 recursos y **0 cambios en recursos** (`non_noop = 0`). Solo propone actualizar salidas de estado (`container_app_fqdn`, `container_app_url` y `min_replicas`), sin modificar infraestructura real. No se ejecutó el plan.

Verificación HTTPS:

```text
GET https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io/health/live  -> 200
GET https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io/health/ready -> 200
strict-transport-security: max-age=2592000
```

La validación de certificado la realizó `curl` por defecto; no se deshabilitó la verificación TLS.

## 4. Tasks cerradas con evidencia

### T13 — Imagen Docker multi-stage y no root

- `Dockerfile` usa etapas separadas de build y runtime, fija `sdk:10.0.403-noble` y `aspnet:10.0.14-noble`, ejecuta como usuario `app`, declara `HEALTHCHECK` contra `/health/live` y no contiene secretos.
- `.dockerignore` excluye `appsettings.Development.json`, `.env`, claves, estado Terraform y artefactos de build.
- La imagen desplegada se ejecuta correctamente en Azure con la revisión `Healthy`.
- Evidencia: `Dockerfile`, `.dockerignore`, revisión activa y health checks.

### T15 — Estructura Terraform y estado

- `versions.tf` fija Terraform `>= 1.17.1, < 2.0.0` y AzureRM `= 5.12.0`; el lockfile está presente.
- `main.tf` declara variables, etiquetado, precondiciones, probes, ingress TLS, `ignore_changes` para secretos y variables, y la regla `http-scaler`.
- `terraform.tfstate`, `terraform.tfvars` y `.terraform/` están ignorados por Git.
- `terraform fmt -check -recursive` y `terraform validate` pasan; el plan no propone cambios en recursos.
- `terraform apply` y `terraform destroy` no se ejecutaron en esta revisión y siguen sujetos a autorización humana separada.

### T23 — Scale-to-zero y apagado operativo

- Azure confirma `minReplicas: 0`, `maxReplicas: 2` y una revisión activa con 1 réplica.
- La regla `http-scaler` con `concurrentRequests: 10` está presente y fue adoptada en Terraform.
- Comando exacto para mantener el estado operativo:

```text
az containerapp update -n ca-thehouse-u2 -g rg-thehouse-u2 --min-replicas 0 --max-replicas 2
```

- No se destruyeron datos ni recursos externos; MongoDB, Redis y JWT se administran fuera de Terraform.
- Para una ventana de evaluación 2–2, el comando sería `--min-replicas 2 --max-replicas 2`; no se ejecutó.

## 5. Tasks abiertas y motivo

| Task | Estado | Motivo |
| --- | --- | --- |
| T01 | ABIERTA | No existe en el repositorio una matriz fechada que inventaríe sesión, revocación, cuotas, bloqueos y trabajos en segundo plano. |
| T02 | ABIERTA | No hay un diseño de externalización de estado revisado que dependa de T01. |
| T03 | ABIERTA | Las claves, TTL, tamaño, TLS y separación por usuario de Redis no tienen diseño y revisión de seguridad registrados. |
| T04 | ABIERTA | Falta la matriz de invalidación, stampede, timeout, reconexión y degradación con pruebas asociadas. |
| T05 | ABIERTA | La implementación Redis y las pruebas de indisponibilidad existen, pero no se cierra sin la revisión de T03/T04. |
| T06 | ABIERTA | No hay diseño documentado de pooling, timeouts, cancelación, TLS, credenciales e índices justificados por consultas U2. |
| T07 | ABIERTA / REQUIERE DECISIÓN HUMANA | Atlas M0 usa un endpoint público con autenticación TLS; la aceptación literal de “sin exponer la base a Internet” no se cumple sin PrivateLink, VNet o una excepción humana explícita. |
| T08 | ABIERTA | La operación actual es 0–2 con una réplica; no se ejecutó la ventana 2–2 ni se probó reemplazo sin afinidad de sesión. |
| T09 | ABIERTA | Los probes y `/health/live`/`/health/ready` existen y responden, pero no se verificó la retirada de réplicas no listas ni el balanceo con dos réplicas. |
| T10 | ABIERTA | HTTPS, HSTS y validación TLS están verificados; no se indujo renovación de certificado ni se completó el análisis con dos réplicas. |
| T11 | ABIERTA | `Observability__OtlpEnabled=false` y no hay collector; no se demuestran trazas, métricas y logs correlacionados entre réplicas. |
| T12 | ABIERTA | No existe matriz de exportador, muestreo, retención, cardinalidad, PII y costo de telemetría; los logs quedan en streaming. |
| T14 | ABIERTA | Es una task futura no U2; NV-02 sigue bloqueada y no hay handoff de digest/SBOM. |
| T16 | ABIERTA | `COST.md` está desactualizado y marcado BLOCKED; no refleja el estado actual 0–2, MongoDB/Redis activos ni una matriz de costo vigente. |
| T17 | ABIERTA | `PRE_APPLY_GATE.md` sigue en BLOCKED y no existe acta `OK PARA APPLY`; no se puede declarar el gate cerrado. |
| T18 | ABIERTA | Los recursos existen, pero no hay plan aprobado con dos réplicas ni evidencia del gate; además la imagen desplegada no corresponde al HEAD. |
| T19 | ABIERTA | Solo se verificaron health checks y TLS; faltan smoke tests de los 7 endpoints, 429, logout/reuso, dos réplicas y fallos Redis/MongoDB. |
| T20 | ABIERTA | No se ejecutó destrucción ni inventario posterior; destruir requiere autorización humana separada. |
| T21 | ABIERTA | No hay costo observado del ciclo de vida ni informe final de facturación. |
| T22 | ABIERTA | La imagen está publicada y desplegada por digest, pero `VERSIONING.md` no registra la fecha, URL, entorno, estado y run de publicación reales; el acceso a versiones de GHCR devolvió 403 por falta de `read:packages`. |

## 6. Advertencias y pendientes

1. **T07 — Alta / requiere decisión humana:** Atlas M0 no satisface literalmente la exclusión de Internet. Se requiere aceptar el endpoint público con TLS o definir PrivateLink/VNet antes de cerrar T07. No se ocultó el hallazgo.
2. **Imagen desactualizada — Media:** Azure ejecuta el digest asociado a `c651886`; el HEAD `7f26eba` no está desplegado. La evidencia de despliegue no cubre las correcciones posteriores.
3. **Dos réplicas no demostradas — Media:** el estado operativo 0–2 y la única réplica actual no sustituyen la ventana de evaluación 2–2 exigida por T08/T18/T19.
4. **Costo no demostrado — Media:** `COST.md` y `PRE_APPLY_GATE.md` siguen en BLOCKED; no se declara costo $0 observado ni se ejecutó apply/destroy.
5. **Telemetría limitada — Media:** logs solo en streaming y OTLP desactivado; T11/T12 no pueden cerrarse sin una ventana de captura y revisión.
6. **GHCR — Baja:** no se pudo consultar el registro de publicación por permisos del token; no se usó ni imprimió ningún token. La ausencia de ese dato no se sustituyó por una invención.

No se detectaron secretos en los artefactos citados ni se imprimieron valores sensibles. La revisión no ejecutó cambios en Azure ni en el repositorio remoto.

## 7. Resultado

- Evidencia documental: **PASS WITH WARNINGS**.
- Tasks cerradas: **T13, T15, T23**.
- Tasks abiertas: **20**; las 19 U2 restantes no pueden cerrarse sin la evidencia indicada.
- Implementación y ejecución de tasks abiertas: **BLOCKED**.
- `terraform apply` y `terraform destroy`: **NO EJECUTADOS**.
