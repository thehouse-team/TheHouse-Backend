# NV-03 — Scaling Cloud U2

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `spec.md` |
| Versión | 1.4.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **🟡 EJECUCIÓN PARCIAL — 20 de 25 tasks (telemetría con Application Insights el 2026-11-25); abiertas: T08, T09, T18 y T19 (ventana de dos réplicas del 3 de diciembre) y T14 (U2 desde 1.4.0-u2, PROPUESTA); ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RNF-05, RNF-06, RNF-08 (transversales) (`Requerimientos_TheHouse_v2`) |
| Plan | `plan.md` 1.4.0-u2 |
| Tasks | `task.md` 1.4.0-u2 |
| Precondición | NV-01 U2 y specs funcionales U2 aprobadas documentalmente; M-BE-028; COST-001 |
| Relación | No autoriza NV-01, NV-02 ni specs funcionales por transitividad |
| Reemplaza | `1.0.0-borrador`; el historial permanece en Git |

Este documento está aprobado documentalmente para la Entrega Unidad 2. No autoriza crear recursos, ejecutar Docker, ejecutar Terraform ni desplegar. **La ejecución requiere autorización humana de Fase B y, para `terraform apply` o `terraform destroy`, una confirmación humana separada.**

**Cambio 1.2.0-u2 (2026-11-05):** agrega NV-03-R15 (observabilidad persistente a costo $0) y las tasks T24 y T25. Aprobación documental: Victor Manuel Jose Jose, 2026-11-05 (M-BE-047). T24 y T25 quedan **⛔ NO EJECUTAR** hasta la autorización humana «EJECUTA»; `terraform apply` sigue requiriendo `OK PARA APPLY`.

**Cambio 1.3.0-u2 (2026-11-25) — APROBADO Y EJECUTADO por Victor Manuel Jose Jose el 2026-11-25 («Apruebo, autorizo y EJECUTA»: spec, paquete `Azure.Monitor.OpenTelemetry.Exporter` 1.10.0 y T11/T12; «Autorizo y OK PARA APPLY» para registrar `Microsoft.Insights` y aplicar; M-BE-090):** define cómo se cierran T11 y T12 (§7.2): las trazas y métricas de OpenTelemetry que la API ya produce se exportan a Application Insights basado en el mismo workspace de Log Analytics (tope de 0.1 GB/día), con la identidad administrada de la app y sin llaves. T11 deja de depender de T08: se implementa y verifica con una réplica, y la correlación entre dos réplicas se demuestra en la ventana del 3 de diciembre (T19).

**Cambio 1.4.0-u2 (2026-11-26) — PROPUESTO, pendiente de «Apruebo NV-01 1.3.0-u2, NV-03 1.4.0-u2 y NV-09 1.3.0-u2 y EJECUTA NV-01 T13, NV-03 T14 y NV-09 T15»:** T14 (entrega de la imagen a NV-02) pasa de «Futuro» a **U2**. El CD de NV-09 ya produce todo lo que pide (digest, SBOM CycloneDX de la imagen, Trivy sobre el digest, firma y atestación con Cosign, y Dockle en la CI); T14 se cierra con el registro de la entrega y su verificación (§7.3).

## 1. Contexto

La Entrega Unidad 2 requiere desplegar los siete endpoints funcionales con al menos dos réplicas, HTTPS, health checks, conectividad MongoDB/Redis y evidencia de funcionamiento. Azure Container Apps es el objetivo de despliegue U2 conforme a M-BE-028, condicionado a COST-001.

La solución local parte del bootstrap y de las specs NV-01 y NV-04 a NV-08. Este documento define el contrato de infraestructura, no afirma que el despliegue exista ni que el costo $0 esté garantizado.

## 2. Objetivo

Definir los requisitos verificables para contenerizar, publicar, aprovisionar, desplegar, verificar, apagar y destruir la API U2 en Azure Container Apps, manteniendo stateless, dos o más réplicas, TLS, observabilidad y costo objetivo $0.

## 3. Precondiciones

1. Bootstrap técnico existente y sin modificar.
2. NV-01 U2 aprobada documentalmente: DI, configuración, health checks, Problem Details y boundaries.
3. NV-04 a NV-08 aprobadas documentalmente para U2.
4. M-BE-028 registra Azure Container Apps como objetivo U2.
5. Existe evidencia de costo compatible con COST-001 antes de `terraform apply`; si no, BLOCKED.
6. Existe autorización humana de Fase B para Docker, GHCR, Terraform y Azure.
7. Existe una suscripción Azure, región autorizada y credenciales temporales suministradas por el humano.
8. Los scanners y quality gates viven en NV-02 (CI, completada el 2026-11-05) y la publicación en NV-09 (CD); no forman parte de esta spec.

## 4. Alcance U2

1. `Dockerfile` multi-stage y `.dockerignore`.
2. Imagen y publicación en GHCR con tag trazable.
3. Terraform reproducible en `infra/terraform/`.
4. Azure Container Apps con mínimo dos réplicas durante la evaluación.
5. HTTPS y TLS gestionado.
6. `/health/live` y `/health/ready`.
7. Observabilidad mínima U2 sin PII ni secretos.
8. Conectividad MongoDB y Redis.
9. Verificación de despliegue y smoke tests de los siete endpoints.
10. Scale-to-zero al finalizar la evaluación, si la arquitectura lo permite.
11. Destrucción con `terraform destroy` y verificación de recursos residuales.
12. `VERSIONING.md` con versión, fecha, imagen, tag, commit, entorno, URL y estado.
13. Observabilidad persistente en la nube a costo $0: Log Analytics con tope diario de ingestión (1.2.0-u2).

## 5. Fuera de alcance

- Implementar código, endpoints, entidades, repositorios o autenticación; pertenece a NV-01 y NV-04 a NV-08.
- Ejecutar SAST, SCA, SBOM, Trivy, Dockle, Cosign, DAST, RESTler, mutación, contrato o performance de NV-02.
- Comparar AWS, Azure y Google Cloud para una selección general; M-BE-011 sigue pendiente y M-BE-028 solo aplica a U2.
- Crear recursos pagos sin evidencia de costo $0.
- Ejecutar `terraform apply` sin `OK PARA APPLY` humano.
- Ejecutar `terraform destroy` sin autorización humana.
- Usar sticky sessions para ocultar estado local.
- Declarar métricas, latencias, disponibilidad o costos no medidos.

## 6. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-03-R01 | API stateless | Ningún estado necesario para la corrección depende de una instancia; revocación, cuotas y sesión se externalizan; reemplazar una instancia no pierde estado ni control de seguridad. | F1 | T01, T02, T05, T19 |
| NV-03-R02 | Dos o más instancias | La misma imagen y configuración se ejecuta en al menos dos réplicas independientes; cada una reporta salud; el reemplazo no requiere afinidad de sesión. | F3 | T08, T18, T19 |
| NV-03-R03 | Load balancing y health checks | Azure Container Apps distribuye tráfico, usa `/health/live` y `/health/ready`, retira réplicas no listas y no depende de sticky sessions. | F3 | T09, T18, T19 |
| NV-03-R04 | Redis distribuido | Redis tiene topología, namespace, TTL, autenticación/TLS cuando aplique, timeouts y comportamiento ante fallo; no es fuente de verdad ni fallback local de seguridad. | F2 | T03, T04, T05, T18, T19 |
| NV-03-R05 | Conectividad MongoDB | La conexión usa pooling, timeouts, cancelación, TLS, credenciales protegidas e índices justificados; el fallo se refleja en readiness sin filtrar secretos. | F2 | T06, T07, T18, T19 |
| NV-03-R06 | HTTPS/TLS | Todo acceso público usa HTTPS con certificado válido; HSTS, terminación, renovación y confianza en cabeceras de proxy están definidos y probados; no se desactiva la validación. | F3 | T10, T18, T19 |
| NV-03-R07 | Observabilidad U2 | Trazas, métricas y logs comparten correlación; se controlan cardinalidad, PII y retención; el exportador y la retención caben en COST-001. | F4 | T11, T12, T19, T24 |
| NV-03-R08 | Imagen Docker y GHCR | La imagen es multi-stage, mínima, no root, sin secretos, con base fijada, healthcheck y tag inmutable; se publica en GHCR con trazabilidad a commit. | F4 | T13, T14, T22 |
| NV-03-R09 | Terraform | La infraestructura se describe declarativamente con módulos, variables, versiones fijadas, estado protegido, etiquetado y plan/apply/destroy; no contiene secretos. | F5 | T15, T18, T20 |
| NV-03-R10 | Azure U2 y evidencia de costo | Azure Container Apps se usa conforme a M-BE-028; existe evidencia vigente de región, SKU, free allowance, cuotas, tarjeta, riesgo y costo esperado; si no se demuestra $0, BLOCKED. | F5 | T16, T17 |
| NV-03-R11 | Creación de infraestructura | La creación sigue un plan aprobado, incluye dos réplicas, HTTPS, Redis, MongoDB y observabilidad, y produce inventario y evidencia de costo inicial. | F6 | T18 |
| NV-03-R12 | Destrucción y apagado | Existe inventario, scale-to-zero cuando aplique, plan de destrucción, verificación de recursos residuales y evidencia de costo posterior. | F6 | T20, T23 |
| NV-03-R13 | Arquitectura de costo $0 | El costo total del ciclo de vida es $0, incluidos compute, dos réplicas, TLS, logs, Redis, MongoDB, registry y egress; si no se demuestra, BLOCKED. | F6 | T12, T17, T20, T21, T23, T24, T25 |
| NV-03-R14 | Versionamiento del despliegue | `VERSIONING.md` registra versión, fecha, imagen, tag, commit, entorno, URL y estado, sin secretos; la información coincide con GHCR y Azure. | F4 | T22 |
| NV-03-R15 | Observabilidad persistente en la nube | Los logs de consola y de sistema de Container Apps (eventos y errores) se conservan en un servicio de Azure con ingestión limitada (`daily_quota_gb` de 0.1) y retención de 30 días; existe una consulta de errores documentada; sin PII ni secretos; costo $0 con evidencia de precio vigente; si Terraform propone reemplazar el entorno o la app, BLOCKED y se usa la alternativa documentada. | F4 | T24, T25 |

## 7. Azure Container Apps U2 — condiciones COST-001

Azure Container Apps es el compute U2. La siguiente tabla debe completarse con evidencia **antes** de `terraform apply`; una celda pendiente no autoriza gasto.

| Componente | Requisito U2 | Evidencia previa a apply |
| --- | --- | --- |
| Compute | Azure Container Apps, Consumption cuando aplique | Región, SKU/plan, cuota, free allowance vigente y estimación mensual |
| Réplicas | Mínimo 2 durante la evaluación | Configuración `minReplicas=2`, `maxReplicas` acotado y costo de dos réplicas |
| Registry | GHCR | Visibilidad, free allowance, autenticación y tag inmutable |
| Logging | Azure Monitor / Log Analytics o alternativa gratuita | Retención, ingestión, límite y costo esperado |
| Egress | Salida a MongoDB, Redis y clientes | Estimación y free allowance |
| Redis | Servicio Redis con TLS | Proveedor, plan, free allowance, límite y fallback definido |
| MongoDB | MongoDB Atlas M0 o alternativa elegible | Plan, cuota, límite, TLS y costo $0 |
| TLS | HTTPS gestionado por Container Apps o certificado válido | Dominio, certificado, renovación y HSTS |
| Estado Terraform | Local protegido o backend gratuito | Ubicación, cifrado, exclusión de Git y acceso |
| Tarjeta y cobro | Riesgo explícito | Si requiere tarjeta, política de cobro y mecanismo de apagado |
| Scale-to-zero | Mínimo de réplicas a 0 al finalizar | Comando exacto con nombres reales |
| Destrucción | `terraform destroy` y verificación | Inventario, plan y evidencia posterior |

Reglas:

1. No se afirma costo $0 sin evidencia vigente del conjunto completo.
2. Un free tier, crédito o alerta de presupuesto no es un límite duro de gasto.
3. Si dos réplicas o algún componente no pueden demostrarse $0, el resultado es **BLOCKED**.
4. El apply requiere mostrar el plan y recibir **OK PARA APPLY** humano.
5. El destroy requiere autorización humana separada.

## 7.1 Observabilidad persistente (1.2.0-u2)

Hoy los logs del entorno quedan solo en streaming (M-BE-037): no se conservan eventos ni errores, y el requisito académico pide un servicio en la nube para manejarlos. Decisión PROPUESTA (M-BE-047):

- **Opción A (preferida):** `azurerm_log_analytics_workspace` en `rg-thehouse-u2` con SKU `PerGB2018`, `daily_quota_gb = 0.1` y `retention_in_days = 30`; el `azurerm_container_app_environment` envía sus logs a ese workspace (`logs_destination = "log-analytics"`). Conserva los logs de consola del backend y los de sistema (reinicios, probes, escalado).
- **Opción B (si A recrea el entorno o la app):** conservar el entorno y exportar logs y trazas desde la aplicación con OpenTelemetry hacia Application Insights basado en el mismo workspace, con la cadena de conexión como secreto de Container Apps.
- Consulta mínima documentada en `evidence.md`: errores y eventos del backend de las últimas 24 h por revisión y `correlationId`.
- Los logs no contienen PII, tokens, contraseñas ni cadenas de conexión; se verifica con datos reales antes de cerrar.

Evidencia de precio (Azure Retail Prices API, `prices.azure.com`, región `eastus2`, consultada el 2026-11-05):

| Medidor | Tramo | Precio |
| --- | --- | --- |
| Analytics Logs Data Ingestion | 0–5 GB/mes | 0.00 USD/GB |
| Analytics Logs Data Ingestion | más de 5 GB/mes | 2.76 USD/GB |
| Analytics Logs Data Retention | más allá del periodo incluido | 0.12 USD/GB/mes |

Cálculo: tope de 0.1 GB/día × 31 días = 3.1 GB/mes, por debajo del tramo de 5 GB a 0.00 USD. **Verificado el 2026-11-05** en la página oficial de precios de Azure Monitor: los primeros 5 GB/mes por cuenta de facturación son gratuitos en Analytics Logs (pago por uso) y se incluyen 31 días de retención sin costo, por lo que 30 días quedan dentro del periodo incluido.

Al alcanzar el tope diario, Azure deja de ingerir logs hasta el día siguiente; se acepta la pérdida de los logs restantes de ese día (NV-03-RK11).

## 7.2 Telemetría con Application Insights (1.3.0-u2, T11 y T12)

Hallazgos (2026-11-25):

1. La API ya está instrumentada con OpenTelemetry 1.20.1 (`ObservabilityConfiguration`): trazas de ASP.NET Core (sin `/health`), de `MongoDB.Driver` y de Redis (`TheHouse.Redis`, solo `db.system.name`), métricas de ASP.NET Core y del runtime, muestreo padre-hijo de 10 % y `service.instance.id` = nombre de la réplica. **El exportador OTLP está apagado**: las trazas y métricas no salen de la réplica.
2. Los logs de consola llegan a Log Analytics (T24/T25) en varias filas por evento (`info: …` y el mensaje en filas distintas), así que el `TraceId` no queda junto al mensaje: la correlación con las trazas no se puede hacer solo con esos logs.
3. Ingestión actual del workspace: ~0.2 MB/día (`Usage`, últimos 7 días) contra un tope de 100 MB/día.

| Punto | Decisión |
| --- | --- |
| Destino | Application Insights `appi-thehouse-u2` **basado en el workspace existente** (`azurerm_application_insights` con `workspace_id`): misma retención de 30 días y mismo tope de 0.1 GB/día del workspace; además `daily_data_cap_in_gb = 0.05` para que la telemetría nunca use más de la mitad del tope y no deje sin espacio a los logs de consola. Sin pruebas de disponibilidad ni alertas con costo |
| Autenticación | **Microsoft Entra ID con la identidad administrada de la app** (la misma de Content Safety), con el rol `Monitoring Metrics Publisher` solo sobre `appi-thehouse-u2`, y `local_authentication_disabled = true`: sin llave ni secreto. La cadena de conexión solo identifica el recurso; se carga como variable `Observability__AzureMonitorConnectionString` (Terraform no administra las variables de la app) |
| Exportador | Paquete nuevo **`Azure.Monitor.OpenTelemetry.Exporter` 1.10.0** (Microsoft, MIT, publicado el 2026-10-07; requiere OpenTelemetry ≥ 1.18, compatible con 1.20.1). Trae `Azure.Core` y `OpenTelemetry.PersistentStorage.FileSystem` como dependencias; el almacenamiento local de reintentos se **desactiva** (contenedor sin disco persistente). Se descarta la distro `Azure.Monitor.OpenTelemetry.AspNetCore` (agrega instrumentación y paquetes que no hacen falta) y el agente OpenTelemetry administrado de Container Apps (requiere el proveedor `azapi`, no envía métricas a Application Insights y toca el entorno, NV-03-RK12) |
| Credencial | Adaptador `TokenCredential` sobre la fuente de tokens de identidad administrada que ya usa Content Safety, generalizada para pedir el recurso `https://monitor.azure.com`; sin `Azure.Identity` |
| Trazas | Solicitudes HTTP (sin `/health`), dependencias MongoDB, Redis y HTTP salientes (`System.Net.Http`, nativo en .NET 10: GenAI, Content Safety y FCM). Muestreo padre-hijo de 10 % por defecto (`Observability__TraceSampleRatio`), compatible con los conteos de Application Insights |
| Métricas | Lista permitida por `View`: `http.server.request.duration`, `http.server.active_requests`, `dotnet.process.cpu.time`, `dotnet.process.memory.working_set`, `dotnet.gc.collections` y `dotnet.thread_pool.queue.length`; el resto se descarta. Exportación cada 60 s |
| Logs | Sin cambios en la consola (todos los niveles siguen en Log Analytics). Además, los logs **Warning y superiores** van a Application Insights con el `operation_Id` de su traza, para ver cada error dentro de su transacción |
| Réplicas | `cloud_RoleInstance` = nombre de la réplica: el 3 de diciembre se comparan las dos réplicas en el mapa de aplicación y en las consultas (T19) |
| Configuración | `Observability__AzureMonitorEnabled` (falso por defecto; en `Testing` y en el CI nunca exporta) y la cadena de conexión; si está activo sin cadena válida, la app no arranca (validación de opciones) |

Datos sensibles (T12):

| Dato | Regla |
| --- | --- |
| Cuerpos, encabezados y cookies | No se registran |
| Query string | Redactada por la instrumentación de ASP.NET Core (búsquedas con texto libre) |
| Consultas de MongoDB | Sin texto de la consulta (solo colección y operación) |
| Redis | Solo `db.system.name`; nunca claves (las de los límites de login contienen correos) |
| IP del cliente | Application Insights la enmascara por defecto (no se desactiva) |
| Usuario | Sin ids de usuario, correos ni tokens en atributos; el id de correlación (`thehouse.correlation_id`) no es dato personal |
| Mensajes de log | Los mismos que hoy, ya revisados sin PII; se verifican con datos reales antes de cerrar |

Costo (T12): Application Insights basado en workspace no tiene cargo propio; su ingestión cuenta en el mismo workspace (primeros 5 GB/mes gratis, §7.1) y queda limitada por los dos topes. Estimado con el tráfico actual: menos de 5 MB/día de métricas mientras haya réplica y menos de 1 MB/día de trazas al 10 %. Si `Usage` muestra más de 50 MB/día, se baja el muestreo o la lista de métricas. Se verifica en Cost Management y con `Usage` antes de cerrar.

Evidencia: consultas KQL documentadas en `evidence.md` (solicitudes con sus dependencias por `operation_Id`, errores con su traza, latencia p95 por ruta y réplica, y `Usage` por tipo de dato).

## 7.3 Entrega de la imagen (1.4.0-u2, T14)

| Lo que pide T14 | Dónde ya ocurre | Evidencia para cerrar |
| --- | --- | --- |
| Digest inmutable | `cd.yml` → `publish`: la imagen se publica en GHCR y se identifica por `sha256:` | Digest de la imagen en producción (revisión activa de `ca-thehouse-u2`) igual al del registro del CD |
| SBOM | `publish`: SBOM CycloneDX de la imagen publicada | Artefacto del CD y su número de componentes |
| Trivy | `publish`: Trivy sobre el digest publicado (además del escaneo de la CI) | Paso en verde del CD que publicó la imagen activa |
| Dockle | CI `container-security` sobre el mismo Dockerfile | Paso en verde de la CI del mismo commit |
| Cosign | `publish`: firma keyless y atestación del SBOM; `deploy` verifica la firma antes de desplegar | Paso de verificación en verde y comando reproducible `cosign verify` / `cosign verify-attestation` con la identidad de `cd.yml` en `main` |

Registro de entrega: tabla en `evidence.md` con commit, digest, run del CD, resultado de cada control y los comandos para verificarlo de nuevo. Sin cambios de código ni de Terraform.

## 8. Decisiones y restricciones aplicables

- M-BE-007: MongoDB con `MongoDB.Driver`, encapsulado en Infrastructure.
- M-BE-008: Redis para caché y estado compartido que la spec requiera.
- M-BE-009: stateless, dos instancias y Load Balancer.
- M-BE-010: COST-001, costo obligatorio $0.
- M-BE-011: matriz general de proveedor pendiente; no se resuelve por esta spec.
- M-BE-018: OpenTelemetry y correlación.
- M-BE-019: Docker, Terraform y GitHub Actions futuros.
- M-BE-028: Azure Container Apps como objetivo U2, sin autorizar apply.
- Nota de reemisión: M-BE-028 se aprobó antes de `1.1.0-u2`; en esta versión, R10/T16 documentan evidencia de costo Azure U2, no una matriz comparativa general. M-BE-011 sigue pendiente.
- COST-001: ningún recurso puede quedar fuera del cálculo.

## 9. Seguridad

- No exponer MongoDB ni Redis a Internet sin control.
- Usar TLS para conexiones públicas y para dependencias cuando el proveedor lo permita.
- Aplicar mínimo privilegio a identidades, roles, tokens y claves.
- No almacenar secretos en Terraform, imágenes, repositorio, `VERSIONING.md` ni logs.
- Usar variables de entorno, secretos de Azure Container Apps o variables sensibles no versionadas.
- Validar cabeceras de proxy antes de confiar en IP o esquema.
- No desactivar la validación de certificados.
- No usar sticky sessions ni fallbacks locales de seguridad.
- Revisar la exposición de health checks, métricas y paneles.
- La imagen debe ejecutarse como usuario no root y sin secretos embebidos.
- El escaneo y la firma de imagen quedan en NV-02; no se ejecutan en U2.

## 10. Testing y evidencia

La verificación U2 debe incluir:

- Smoke test HTTPS de los siete endpoints U2.
- `/health/live` y `/health/ready` con semántica correcta.
- Login rate limit hasta HTTP 429 con el umbral real.
- Logout con token válido, reutilización y rechazo.
- Dos réplicas activas verificadas con Azure CLI.
- Fallo de MongoDB: readiness no-ready, error controlado y sin stack trace.
- Fallo de Redis: funciones dependientes fallan de forma controlada y sin abrir controles.
- Distribución de tráfico y reemplazo de una réplica.
- TLS válido y redirección HTTPS cuando aplique.
- Cost Management consultado y registrado.
- Scale-to-zero o destroy al finalizar, con autorización humana.
- Observabilidad persistente: consulta de errores y eventos con registros reales del backend, sin PII ni secretos, y uso de ingestión registrado (1.2.0-u2).

Ninguna de estas pruebas se ejecuta en esta fase documental. No se declaran latencias, disponibilidad ni costos sin evidencia.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-03-RK01 | Free allowance insuficiente para dos réplicas | Cobro inesperado | Evidencia previa y scale-to-zero/destroy |
| NV-03-RK02 | Redis o MongoDB sin opción $0 elegible | Arquitectura inviable | BLOCKED y revisión humana |
| NV-03-RK03 | Logs o egress no contabilizados | Costo oculto | Límites, retención y monitoreo |
| NV-03-RK04 | Estado local en revocación o cuotas | Divergencia entre réplicas | Redis compartido y pruebas |
| NV-03-RK05 | Terraform con secretos o estado expuesto | Compromiso | Variables protegidas y estado fuera de Git |
| NV-03-RK06 | Recursos residuales tras destroy | Cobro continuo | Inventario y verificación posterior |
| NV-03-RK07 | Apply automático tras plan | Gasto no autorizado | Gate `OK PARA APPLY` obligatorio |
| NV-03-RK08 | GHCR con token expuesto | Acceso no autorizado | Token efímero, nunca impreso ni versionado |
| NV-03-RK09 | Certificado o DNS mal configurado | TLS inválido | Verificación HTTPS y renovación |
| NV-03-RK10 | Observabilidad con cardinalidad excesiva | Costo y ruido | Muestreo, límites y retención |
| NV-03-RK11 | Tope diario de ingestión alcanzado | Pérdida de logs del resto del día | Tope de 0.1 GB/día aceptado; revisar uso y ajustar solo con evidencia de costo |
| NV-03-RK12 | Cambiar el destino de logs fuerza recrear el entorno o la app | Caída o URL nueva | Revisar el plan; si propone reemplazo, BLOCKED y usar la opción B |
| NV-03-RK13 | La telemetría llena el tope diario del workspace | Se pierden logs de consola el resto del día | `daily_data_cap_in_gb = 0.05` en Application Insights, métricas en lista permitida y muestreo de 10 %; revisar `Usage` |
| NV-03-RK14 | El exportador no obtiene token de la identidad | Sin telemetría (la API sigue funcionando) | El exportador falla sin afectar las solicitudes; se verifica la llegada de datos antes de cerrar T11 |

## 12. Dependencias

- Bootstrap existente (precondición).
- NV-01 U2 para stateless, salud, correlación y configuración.
- NV-04 a NV-08 para los siete endpoints.
- M-BE-028 para Azure Container Apps U2.
- Evidencia de costo $0 y autorización de Fase B.
- Suscripción, región y credenciales temporales proporcionadas por el humano.
- NV-02 no se ejecuta y no es precondición de U2.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 15 |
| Fases del plan | 6 |
| Tasks | 25 |
| Tasks U2 | 24 |
| Tasks futuras (no U2) | 1 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 20 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **🟡 EJECUCIÓN PARCIAL — 20 de 25 tasks (telemetría con Application Insights el 2026-11-25); abiertas: T08, T09, T18 y T19 (ventana de dos réplicas del 3 de diciembre) y T14 (U2 desde 1.4.0-u2, PROPUESTA); ver `evidence.md`** |

## 14. Aprobación documental y ejecución

La aprobación documental U2 quedó registrada con:

1. Aprobador: Victor Manuel Jose Jose.
2. Fecha: 2026-11-02.
3. Versión aprobada: `1.1.0-u2` de `spec.md`, `plan.md` y `task.md`.
4. Proveedor U2: Microsoft Azure; compute: Azure Container Apps; mínimo 2 réplicas.
5. Restricciones: COST-001, sin apply automático y sin destroy automático.

La ejecución sigue bloqueada. El apply y el destroy requieren confirmación humana explícita. Sin ese registro, el estado permanece **BLOCKED**.

Cambio 1.2.0-u2: aprobación documental de Victor Manuel Jose Jose, 2026-11-05 (M-BE-047). T24 y T25 requieren la autorización humana «EJECUTA»; el apply, además, `OK PARA APPLY`.
