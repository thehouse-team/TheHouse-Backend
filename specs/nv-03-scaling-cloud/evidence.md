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

## Addendum 1.2.0-u2 — Observabilidad persistente (T24 en curso)

Ejecutado el 2026-11-05 en la rama `fix/u2-review` con la autorización humana «EJECUTA» de Victor Manuel Jose Jose (M-BE-047).

- `infra/terraform/main.tf`: nuevo `azurerm_log_analytics_workspace.this` (`log-thehouse-u2`, `PerGB2018`, `daily_quota_gb = 0.1`, `retention_in_days = 30`) y el entorno `cae-thehouse-u2` con `logs_destination = "log-analytics"`.
- `variables.tf` valida el tope (mayor que 0 y máximo 0.16 GB/día = 5 GB / 31 días) y fija la retención en 30 días; `outputs.tf` expone el nombre del workspace.
- `terraform fmt -check -recursive` → exit 0; `terraform validate` → `Success! The configuration is valid.`
- `terraform plan` (guardado fuera del repositorio): **1 a crear** (workspace), **1 a cambiar en sitio** (entorno: `logs_destination` y `log_analytics_workspace_id`), **0 a destruir**. No reemplaza el entorno ni la app: la opción A es viable y no se necesita la opción B.
- `terraform apply` ejecutado tras el `OK PARA APPLY` humano de Victor Manuel Jose Jose (2026-11-05), con el plan revisado.
- Tras el apply, el estado de Terraform contendrá las claves compartidas del workspace (atributos sensibles): `terraform.tfstate` debe seguir fuera de Git y no compartirse por chat.

### Resultado del apply (T24 completada)

- `Apply complete! Resources: 1 added, 1 changed, 0 destroyed.` El entorno se modificó en sitio en 2 min 9 s; el state previo se respaldó fuera del repositorio.
- Verificación en Azure: workspace `log-thehouse-u2` con `PerGB2018`, retención 30 días, `dailyQuotaGb` 0.1 y `Succeeded`; entorno `cae-thehouse-u2` con destino `log-analytics`; app `ca-thehouse-u2` en la misma revisión (`ca-thehouse-u2--0000008`), misma URL, réplicas 0–2; `/health/live` y `/health/ready` → 200.
- `terraform plan` posterior: `No changes. Your infrastructure matches the configuration.`
- Precio verificado en la página oficial de Azure Monitor (2026-11-05): 5 GB/mes gratuitos por cuenta de facturación en Analytics Logs y 31 días de retención incluidos.

### T25 — en curso (ABIERTA)

- Logs de **sistema** recibidos en `ContainerAppSystemLogs_CL` unos 5 minutos después del apply (7 registros: `ContainerCreated`, `ContainerStarted`, `RevisionUpdate`, `ContainerAppReady`, `ProbeFailed` de arranque). No contienen PII, tokens ni cadenas de conexión.
- Logs de **consola** (`ContainerAppConsoleLogs_CL`): sin registros tras 15 minutos de espera con tráfico de prueba. Hipótesis: la réplica activa no tomó el nuevo destino de logs; requiere reiniciar la revisión, lo que necesita autorización humana.
- Consulta usada (API de Log Analytics vía `az rest`, sin extensiones):
  `union isfuzzy=true ContainerAppConsoleLogs_CL, ContainerAppSystemLogs_CL | where TimeGenerated > ago(60m) | summarize registros=count() by Type`
- Pendiente para cerrar T25: logs de consola con registros reales del backend, revisión de PII/secretos en ellos y registro del uso de ingestión y del costo en Cost Management.

### T25 — completada (2026-11-07)

- KQL sobre `log-thehouse-u2` (últimos 3 días): `ContainerAppConsoleLogs_CL` con 264 registros de 8 revisiones y `ContainerAppSystemLogs_CL` con 319 registros de 17 revisiones; los logs de consola de la aplicación llegan.
- Búsqueda de datos sensibles en los logs de consola (JWT `eyJ…`, `mongodb://`, `redis://`, correos, `password`, `refreshToken`, `JWT_SIGNING_KEY`): **0 coincidencias**.
- Niveles: 115 `info`, 10 `warn` (`ForwardedHeadersMiddleware[1]`, encabezados reenviados no simétricos enviados por clientes externos; informativo) y 139 líneas de continuación.
- Ingestión (`Usage`): ~0.0001 GB/día, frente al tope de 0.1 GB/día.
- Cost Management (Month to date, 2026-11-07): **0.00 USD** (servicio facturable listado: Log Analytics).

### T08, T18 y T19 — diferidas a la ventana de evaluación (decisión de Victor, 2026-11-07)

El plan de Terraform para la ventana de dos réplicas está revisado (`min_replicas` 0 → 2; `0 to add, 1 to change, 0 to destroy`; imagen, secretos y variables sin cambios por `ignore_changes`). Por decisión de Victor (opción B), la ventana 2–2, el smoke con dos instancias y su evidencia se ejecutan el 3 y 4 de diciembre (M-BE-039), con «OK PARA APPLY» para subir y para regresar a 0–2. Las fallas de Redis y MongoDB de T19 se demuestran con las pruebas automatizadas y el entorno efímero, no en producción.

## Cierre documental 1.2.0-u2 (2026-11-19) — 10 tasks con evidencia existente

Autorización: «EJECUTA cierre documental de NV-03» — Victor Manuel Jose Jose, 2026-11-19 (M-BE-080). Solo documentación: se cierran las tasks que el sistema desplegado ya cumple, con evidencia del código, de las specs NV-04 a NV-25 y de Azure; no se cambió código ni infraestructura. No se leyeron `terraform.tfvars`, `terraform.tfstate` ni `appsettings.Development.json`.

### T01 y T02 — Inventario y externalización de estado

| Estado | Dónde vive | Réplicas | Fuente |
| --- | --- | --- | --- |
| Sesiones (refresh tokens, solo hash) | MongoDB `refresh_tokens` | Compartido | NV-10 |
| Revocación de JWT (`jti`) y de sesiones (`sid`) | Redis `thehouse:u2:auth:revoked:*` con TTL | Compartido; sin caché local (falla cerrado) | NV-06, NV-10 |
| Cuotas y rate limit (≈40 llaves `thehouse:u2:<módulo>:<alcance>:v1:<hash>`) | Redis con TTL de ventana | Compartido; Redis caído → 503 | NV-05 a NV-25 |
| Candados (visitas, mensajes, rentas, retención de documentos) | Redis `SET NX` con TTL | Compartido | NV-13, NV-16, NV-17, NV-18 |
| Sal diaria de vistas | Redis `thehouse:u2:analytics:view-salt:v1:{día}`, `SET NX`, 48 h | Compartido | NV-25 |
| Datos de negocio | MongoDB Atlas (colecciones por módulo) | Compartido | NV-07 a NV-25 |
| Tarea de retención de documentos (cada 6 h) | `MediaRetentionWorker` con candado de Redis | Una réplica a la vez | NV-13 |
| Cola de push | `Channel` acotado en memoria (1000) | **Local por diseño**: la envía la réplica que guardó la notificación; la bandeja en MongoDB es la fuente de verdad (M-BE-078) | NV-20 |
| Caché HTTP | `ETag`/`Cache-Control` en el cliente; sin caché del servidor | Sin estado | NV-07, NV-08 |
| Llaves de cifrado de documentos y firma JWT | Secretos de la Container App | Iguales en todas las réplicas | NV-13, NV-05 |

Resultado: ningún estado de seguridad (revocación, cuotas, candados) vive en memoria de una réplica; el único estado local (cola de push) está justificado y no afecta la corrección.

### T03 — Topología y claves Redis

- Un solo Redis (Upstash, plan gratuito) con namespace `thehouse:u2:<módulo>:<alcance>:v1:`; los identificadores de usuario e IP van como hash (`RateLimitKeys.Hash`), nunca en claro.
- TTL en toda llave (ventanas de rate limit, revocación igual a la vida del access token más tolerancia, candados de segundos, sal de 48 h).
- TLS obligatorio para cualquier host que no sea loopback (`RedisAccess`: sin `Ssl` → configuración inválida); credencial como secreto `redis-connection` de la Container App (M-BE-038).

### T04 y T05 — Fallas de Redis y estado distribuido de autenticación y límites

- Redis no se usa como caché de datos, así que no hay invalidación tras escritura ni *stampede* (la caché pública es HTTP con `ETag`).
- Ante timeout o caída: revocación, rate limit, candados y sal fallan **cerrado** (503, nunca se omite el control); `AbortOnConnectFail = false` para reconectar solo. Probado en cada spec con los dobles `Unavailable` (por ejemplo `Views_never_break_the_room_detail_and_stats_fail_closed`, `Deletion_attempts_are_limited...`) y en el smoke `full` del CD (429 de login, reuso del token revocado 401).
- Sin fallback local de seguridad: la revocación por `sid` se consulta en Redis en cada solicitud autenticada en todas las réplicas.

### T06 — Conexión MongoDB, pooling e índices

- `MongoClientSettings`: TLS obligatorio fuera de loopback y `AllowInsecureTls` rechazado; `ConnectTimeout`, `ServerSelectionTimeout` y `SocketTimeout` de 5 s; `MaxConnectionPoolSize` 100; cancelación propagada en todas las llamadas.
- Credencial solo en el secreto `mongodb-uri` (M-BE-038).
- Índices creados por cada repositorio para sus consultas (únicos donde hay idempotencia y TTL donde hay retención), documentados en cada spec (por ejemplo `ux_room_views_room_day_actor`, `ux_push_devices_token`, `ttl_moderation_rejections_created`).

### T10 — TLS, HSTS y confianza en el proxy

| Verificación (2026-11-19) | Resultado |
| --- | --- |
| Certificado del dominio público | Administrado y renovado por Azure: `CN=grayisland-cb722b5f.eastus2.azurecontainerapps.io`, emisor `Microsoft TLS G2 RSA CA OCSP 02`, vence 2027-02-12 |
| Protocolo | TLS 1.3 |
| HTTP | `301` a HTTPS; ingress con `allowInsecure = false` |
| HSTS | `Strict-Transport-Security: max-age=2592000` (30 días) en las respuestas |
| Proxy | `ForwardedHeaders` solo con `Proxy__Enabled`, `RequireHeaderSymmetry`, `ForwardLimit` y redes del ingress de Container Apps; validación de certificados activa hacia MongoDB, Redis, FCM y Content Safety |

La revisión con dos réplicas activas se repite en T19 (ventana del 3 y 4 de diciembre).

### T16 y T17 — Costo y gate de apply

| Componente | Plan | Costo observado |
| --- | --- | --- |
| Azure Container Apps (0–2 réplicas, consumo) | Concesión gratuita mensual de consumo | 0.00 USD |
| Log Analytics | Tope de 0.1 GB/día y 30 días | 0.00 USD |
| Azure AI Content Safety | F0 (30 000 llamadas cada 30 días; se detiene sin cobrar) | 0.00 USD |
| Identidades administradas y roles | Sin costo | — |
| GHCR | Paquete público | 0 USD |
| GitHub Actions | Repositorio público | 0 USD |
| MongoDB Atlas M0 y Upstash Redis | Planes gratuitos | 0 USD |
| Firebase Cloud Messaging | Sin costo por mensaje | 0 USD |

- Cost Management (`ActualCost`, `rg-thehouse-u2`, del 2026-11-01 al 2026-11-19, por servicio): **0.00 USD** (filas «Foundry Tools» y «Log Analytics»). Presupuesto `thehouse-budget` de 1 USD con alertas al 50 % y 100 %.
- Gate cumplido en cada cambio de infraestructura: el plan se mostró y se aplicó solo tras «OK PARA APPLY» de Victor (NV-09 el 2026-11-06, observabilidad de NV-03 1.2.0-u2 y NV-21 el 2026-11-19 con `Plan: 3 to add, 1 to change, 0 to destroy`).

### T22 — Publicación en GHCR y `VERSIONING.md`

Cubierta por NV-09: cada despliegue publica `ghcr.io/thehouse-team/thehouse-backend:sha-<commit>` y se despliega por digest firmado (Cosign keyless, SBOM atestado), sin tokens impresos (`GITHUB_TOKEN` con `packages: write` solo en `publish`); `VERSIONING.md` §11 registra fecha, commit, digest, run, aprobador, revisión y estado (NV-09 T17, 2026-11-17).

### Tasks que siguen abiertas

| Task | Motivo |
| --- | --- |
| T07 Resiliencia y seguridad MongoDB | Falta verificar en Atlas la lista de acceso de red y el rol mínimo del usuario de la base (lo revisa Victor en el portal de Atlas) |
| T08, T09 y T19 | Ventana de dos réplicas del 3 y 4 de diciembre con «OK PARA APPLY» (M-BE-059, opción B). Ya verificado: probes `/health/live` y `/health/ready` en Terraform y sin sesiones fijas |
| T11 y T12 | La app usa OpenTelemetry con logs de consola correlacionados en Log Analytics, pero sin exportador de trazas ni métricas (`OtlpEnabled = false`); falta decidir el exportador con costo $0 |
| T14 | Fuera de U2 |
| T18 | Infraestructura creada y verificada, pero el criterio incluye el plan con dos réplicas (ventana) |
| T20 y T21 | Destrucción y costo del ciclo completo: se propone probarlo en un entorno desechable (otro grupo de recursos) con autorización y «OK PARA APPLY» |

Resultado del cierre: **15 de 25** tasks completas.

## T20 y T21 — Ciclo de vida: crear, verificar y destruir (2026-11-19)

Autorización: «EJECUTA NV-03 T20 y T21» — Victor Manuel Jose Jose, 2026-11-19 (M-BE-082); cada `apply` con «OK PARA APPLY». Procedimiento repetible en `infra/terraform/LIFECYCLE.md`.

### Diseño

- Mismo Terraform de producción en el workspace `lifecycle` (state propio, fuera de git) con `lifecycle.tfvars` (todos los nombres `*-lifecycle`).
- Variables nuevas, con valor por defecto igual a producción:
  - `content_safety_enabled`: Azure admite un solo F0.
  - `cd_identity_enabled`: el desechable no otorga roles sobre recursos compartidos.
  - `existing_environment_id`: usar un entorno existente sin administrarlo.
- Bloques `moved` para que el state de producción no destruya ni recree nada. `terraform plan` de producción después de cada cambio: **0 to add, 0 to change, 0 to destroy**.

### Límites encontrados en la suscripción

| Intento | Resultado |
| --- | --- |
| Entorno de Container Apps propio en `eastus2` | 409 `MaxNumberOfRegionalEnvironmentsInSubExceeded`: la suscripción admite un solo entorno (el de producción) |
| Mismo entorno en `eastus` | Mismo 409; el límite aplica a toda la suscripción |
| Aumento de cuota (`Microsoft.Quota` registrado con autorización de Victor; solicitud de `ManagedEnvironmentCount` = 2) | Rechazada: `ResourceNotAvailableForOffer` (la oferta de la suscripción no lo permite) |
| Decisión de Victor (opción 1) | La app desechable se crea **dentro** del entorno de producción sin administrarlo (Terraform no lo crea, cambia ni destruye) y sin identidad del CD |

Los recursos parciales de los dos primeros intentos (grupo, identidades, rol personalizado, credencial federada y Log Analytics, sin costo) se destruyeron con el mismo Terraform en el `apply` siguiente.

### Crear y verificar

| Paso | Resultado |
| --- | --- |
| Plan aprobado | `4 to add, 0 to change, 6 to destroy` (los 6 eran los parciales del desechable) |
| `terraform apply` | **Apply complete! Resources: 4 added, 0 changed, 6 destroyed** (339 s) |
| Inventario (`az`) | `rg-thehouse-lifecycle` (`eastus2`) con `id-thehouse-lifecycle-app`, `log-thehouse-lifecycle` y `ca-thehouse-lifecycle` (revisión `--4pa4xrz`, `Succeeded`, 0–1 réplicas) en el entorno compartido |
| App desechable | Arrancó y respondió `400` en `/health/*`: el filtro de hosts (`AllowedHosts`) rechaza un host no configurado, que es el comportamiento seguro esperado |
| Producción durante la prueba | `ca-thehouse-u2--cea10a4c-64-1` Healthy al 100 %, `/health/ready` 200 |

### Destruir y comprobar

| Paso | Resultado |
| --- | --- |
| Plan aprobado | `0 to add, 0 to change, 4 to destroy` (solo la app, Log Analytics, la identidad y el grupo del desechable; el entorno compartido no aparece) |
| `terraform apply` | **Apply complete! Resources: 0 added, 0 changed, 4 destroyed** (126 s) |
| Recursos residuales | `az group exists rg-thehouse-lifecycle` → `false`; 0 recursos y 0 roles personalizados con «lifecycle» en la suscripción; `terraform state list` del workspace vacío |
| Producción después | Entorno `Succeeded`, revisión Healthy al 100 %, `/health/ready` 200, `terraform plan` sin cambios |

### T21 — Costo del ciclo de vida

| Fuente | Resultado |
| --- | --- |
| Cost Management (`ActualCost`, 2026-11-19 y 2026-11-20, por grupo de recursos) | Solo `rg-thehouse-u2` con 0.00 USD; `rg-thehouse-lifecycle` todavía **PENDIENTE DE REFLEJO** (Azure tarda hasta 24–48 h) |
| Esperado | 0.00 USD: la app escaló a cero dentro de la concesión gratuita de Container Apps, Log Analytics sin ingesta (tope de 0.1 GB/día), identidades y roles sin costo |
| Revisión posterior (2026-11-24) | Cost Management `ActualCost` del 2026-11-19 al 2026-11-23 por grupo de recursos: solo `rg-thehouse-u2` con 0.00 USD cada día; `rg-thehouse-lifecycle` **sin ningún cargo** pasadas más de 48 h de reflejo, y el grupo ya no existe. **Costo observado del ciclo de vida: 0.00 USD** |

### Observación

En el entorno `cae-thehouse-u2` existe además la app `ca-thehouse-genai-u2`, que no creó este Terraform (por su nombre, del equipo de GenAI). Queda fuera del state del backend y no se tocó; se aclara con ese equipo, porque su guía de integración menciona Azure App Service.

Resultado: **T20 y T21 completas**; el costo de T21 quedó confirmado en **0.00 USD** el 2026-11-24. NV-03 queda en **17 de 25**.
