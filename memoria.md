# TheHouse Backend — Memoria de decisiones

Este documento no es un diario. Registra decisiones estables y pendientes bajo `constitution.md` y `AGENTS.md`. **APROBADO** significa directriz aceptada en el encargo, no implementación realizada ni autorización de specs. **PROPUESTO** requiere revisión; **PENDIENTE** carece de resolución/evidencia; **DESCARTADO** no se adopta en el alcance actual.

## M-BE-001 — Bootstrap técnico existente

- **Decisión:** conservar `TheHouse.slnx` y los siete proyectos raíz existentes, sin `src/` ni `tests/`.
- **Estado:** APROBADO.
- **Justificación:** precondición técnica satisfecha; compilación previa con 0 errores y 0 advertencias.
- **Impacto:** esta entrega no recrea, renombra, mueve o modifica proyectos, código, referencias ni configuración de aplicación. No repite build/restore/tests.

## M-BE-002 — Stack de backend

- **Decisión:** C#, .NET 10 y ASP.NET Core Web API; REST, JSON, HTTPS y OpenAPI.
- **Estado:** APROBADO.
- **Justificación:** tecnologías aprobadas y alineadas con los proyectos actuales.
- **Impacto:** los contratos y fundamentos futuros deben usar este stack; aprobar tecnología no instala paquetes.

## M-BE-003 — Clean Architecture y referencias actuales

- **Decisión:** TheHouse → Application e Infrastructure; Infrastructure → Application y Domain; Application → Domain; Domain sin dependencias internas.
- **Estado:** APROBADO.
- **Justificación:** referencias verificadas en la solución actual; Dependency Rule hacia el núcleo.
- **Impacto:** host como composition root, puertos internos y adaptadores externos. No modificar referencias ni introducir tipos de infraestructura en Domain.

## M-BE-004 — Modular Monolith

- **Decisión:** monolito modular con límites de módulo y contratos explícitos, no microservicios.
- **Estado:** APROBADO.
- **Justificación:** reduce complejidad operativa académica conservando separación y escalado horizontal.
- **Impacto:** futura propiedad de datos por módulo y ausencia de ciclos; no se crean módulos de negocio ahora.

## M-BE-005 — SDD, independencia y autorización

- **Decisión:** ninguna implementación sin spec, plan y task aprobados; cada incremento relevante tiene spec independiente.
- **Estado:** APROBADO.
- **Justificación:** control de alcance, trazabilidad y revisión humana.
- **Impacto:** NV-01/02/03 son borradores bloqueados. La preparación de gobierno está autorizada, la ejecución no. No existe spec de implementación activa.

## M-BE-006 — Gobierno de agentes y skills

- **Decisión:** jerarquía constitution → AGENTS → memoria → spec → plan → task; siete perfiles y ocho skills reutilizables.
- **Estado:** APROBADO.
- **Justificación:** especialización con una autoridad común y sin autoaprobación.
- **Impacto:** perfiles OpenCode de revisión en lectura; declarar SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVO y ESTADO AUTORIZADO. Sin permiso: BLOCKED. PASS no es autorización humana.

## M-BE-007 — Persistencia NoSQL

- **Decisión:** MongoDB con MongoDB.Driver como dirección de persistencia, encapsulada en Infrastructure.
- **Estado:** APROBADO.
- **Justificación:** elección explícita del proyecto; diseño por patrones de acceso e índices.
- **Impacto:** modelado documental, consistencia y conexiones se planifican en NV-03; no hay conexión, driver nuevo, repositorio o entidad implementados por esta entrega.

## M-BE-008 — Redis y caché distribuida

- **Decisión:** Redis para caché compartida y controles distribuidos que futuras specs requieran; claves, TTL e invalidación explícitos.
- **Estado:** APROBADO.
- **Justificación:** dos instancias no pueden depender de memoria privada para coherencia.
- **Impacto:** definir cache-aside, atomicidad, reconexión y degradación por tipo de dato; no autoriza implementar Redis.

## M-BE-009 — Stateless, réplicas y balanceo

- **Decisión:** API stateless, mínimo dos instancias y Load Balancer, sin dependencia de sticky sessions.
- **Estado:** APROBADO.
- **Justificación:** escalado horizontal y reemplazo de instancias sin pérdida de estado.
- **Impacto:** NV-03 debe probar distribución, fallos y consistencia entre réplicas; no describe un despliegue ya existente.

## M-BE-010 — COST-001

- **Decisión:** costo obligatorio del entorno académico = $0 para todo recurso y ciclo de vida.
- **Estado:** APROBADO.
- **Justificación:** restricción académica no negociable por un agente.
- **Impacto:** costo incierto, expiración de créditos o riesgo no controlado bloquean aprovisionamiento. Presupuesto/alerta no limita por sí solo cobros. Ningún proveedor se considera gratuitamente viable sin evidencia.

## M-BE-011 — Proveedor cloud y matriz pendiente

- **Decisión:** comparar únicamente AWS, Azure y Google Cloud; no seleccionar todavía proveedor.
- **Estado:** PENDIENTE.
- **Justificación:** faltan evidencia vigente de cuotas, costos y elegibilidad para el conjunto completo.
- **Impacto:** NV-03-R10/T16-T17 reserva la matriz comparativa y el gate de costo; registrar para cada proveedor compute, dos instancias, Load Balancer, HTTPS, container hosting, observabilidad, storage/CDN, Redis, MongoDB connectivity, Terraform, requiere tarjeta, riesgo de cobro y costo esperado. No provisionar antes del gate de costo.

## M-BE-012 — Contrato y fundamentos HTTP

- **Decisión:** NV-01 preparará DI, Configuration, Problem Details, Validation Foundation, API Versioning, OpenAPI, Health Checks, Correlation IDs, Response Compression, Rate Limiting, Pagination, Exception Handling y Modular Boundaries.
- **Estado:** PROPUESTO.
- **Justificación:** fundamentos transversales anteriores al negocio, pendientes de revisión de la spec.
- **Impacto:** no crear endpoints funcionales ni reabrir bootstrap. Convenciones y valores concretos se aprueban antes de implementar.

## M-BE-013 — Seguridad futura y ciclo de tokens

- **Decisión:** JWT, Refresh Tokens y Logout como capacidades futuras de specs funcionales separadas.
- **Estado:** APROBADO.
- **Justificación:** se requiere autenticación con validación completa, rotación, revocación y pruebas de abuso.
- **Impacto:** NV-05 definirá Login y ciclo de refresh; NV-06 definirá Logout y revocación compartida. No hay JWT implementado o autorizado; se prohíbe fallback local de revocación que diverja entre réplicas.

## M-BE-014 — Secretos y defensa por defecto

- **Decisión:** mínimo privilegio, autorización por recurso, validación, TLS y secretos fuera del repositorio, imágenes, logs y documentos.
- **Estado:** APROBADO.
- **Justificación:** prevención de exposición y abuso antes de despliegue.
- **Impacto:** placeholders en planificación; rate limiting global debe distinguirse del local; los fallos de dependencias de seguridad no pueden abrir acceso silenciosamente.

## M-BE-015 — Proyectos de testing existentes

- **Decisión:** conservar TheHouse.UnitTests para Domain/Application, TheHouse.IntegrationTests para HTTP/composición/adaptadores y TheHouse.SecurityTests para controles y abuso.
- **Estado:** APROBADO.
- **Justificación:** proyectos ya presentes, xUnit y herramientas base ya referenciados.
- **Impacto:** la prueba unitaria actual está vacía; las otras suites no contienen pruebas de negocio. No confundir existencia o un placeholder correcto con cobertura funcional. No se ejecutan ni modifican pruebas en esta entrega.

## M-BE-016 — Testing ampliado

- **Decisión:** planificar WebApplicationFactory, Stryker.NET (mutación), PactNet (contratos), NBomber (performance), OWASP ZAP (DAST) y RESTler (REST fuzzing), además de xUnit.
- **Estado:** PROPUESTO.
- **Justificación:** verificar comportamiento, compatibilidad, resistencia y calidad más allá de cobertura de líneas.
- **Impacto:** NV-02 fija alcance, fixtures, permisos, evidencias y umbrales revisables; ninguna herramienta adicional se instala y no se crean proyectos nuevos aquí.

## M-BE-017 — DevSecOps y supply chain

- **Decisión:** planificar Semgrep, SonarCloud/SonarAnalyzer, NuGet vulnerability scanning, CycloneDX, Trivy, Dockle y Cosign junto con testing dinámico y quality gates.
- **Estado:** PROPUESTO.
- **Justificación:** seguridad de código, dependencias, artefactos y ejecución en un flujo verificable.
- **Impacto:** fallos/omisiones de controles obligatorios bloquean, no se reportan como aprobados. Licencias, cuotas, compatibilidad .NET 10, baselines y umbrales se revisan antes de activación; SonarCloud solo si cumple costo $0.

## M-BE-018 — Observabilidad

- **Decisión:** OpenTelemetry para trazas/métricas y correlación con logs estructurados, con retención limitada y sin PII/secretos.
- **Estado:** APROBADO.
- **Justificación:** diagnóstico de peticiones y dependencias a través de varias instancias.
- **Impacto:** NV-01 define IDs y health; NV-03 define exportación, cardinalidad y evidencia de costo. Exportadores y backends concretos pendientes.

## M-BE-019 — Docker, Terraform y GitHub Actions

- **Decisión:** tecnologías aprobadas para contenerización, IaC y CI/CD futuras.
- **Estado:** APROBADO.
- **Justificación:** reproducibilidad, revisión, escaneo y ciclo de vida controlado.
- **Impacto:** NV-02 planifica pipeline/seguridad; NV-03 planifica imagen e infraestructura con creación y destrucción. No crear archivos operativos ni recursos ahora. La validación de imágenes en NV-02 depende de un artefacto futuro autorizado en NV-03.

## M-BE-020 — Versionamiento y Git Flow

- **Decisión:** documentos y artefactos versionados; Git Flow con main, develop, feature, release y hotfix, PR y revisión humana.
- **Estado:** APROBADO.
- **Justificación:** trazabilidad de contratos y cambios académicos.
- **Impacto:** convención futura, no autorización para iniciar repositorio o hacer operaciones Git de escritura. La estrategia concreta de versión API se propone en NV-01.

## M-BE-021 — Referencias metodológicas analizadas

- **Decisión:** usar solo como referencia los README de [WebAPIDevSecOps](https://github.com/edelomeza/WebAPIDevSecOps) y [WebAPIDevSecOpsScalling](https://github.com/edelomeza/WebAPIDevSecOpsScalling), consultados por HTTPS en lectura.
- **Estado:** APROBADO.
- **Justificación:** muestran secuenciación de SAST/SCA/SBOM/DAST, suites, escaneo/firma de imágenes, Redis, escalado y observabilidad.
- **Impacto:** no hubo clone ni copia de código. Son declaraciones documentales externas, no resultados ejecutados ni auditoría de sus workflows. NV-02 contiene el análisis y adaptación, sin importar su SQL Server, cantidades de tests, umbrales o fallbacks locales de seguridad.

## M-BE-022 — Soluciones no adoptadas

- **Decisión:** descartar en el alcance actual microservicios, SQL Server/EF Core por imitación de ejemplos, estado de seguridad exclusivamente local y proveedores cloud distintos de AWS/Azure/GCP.
- **Estado:** DESCARTADO.
- **Justificación:** contradicen stack, scope o objetivo de consistencia horizontal.
- **Impacto:** ni los repositorios externos ni las herramientas sugeridas amplían la arquitectura aprobada. Cambiar una decisión requiere revisión formal.

## M-BE-023 — Roadmap funcional; solo registro

- **Decisión:** reservar la siguiente secuencia, sin crear carpetas, requisitos operativos ni tasks funcionales todavía:

| NV | Incremento futuro |
| --- | --- |
| NV-04 | User Registration |
| NV-05 | Login |
| NV-06 | Logout |
| NV-07 | Property Core |
| NV-08 | University Catalog |
| NV-09 | Buildings and Rooms |
| NV-10 | Search and Map |
| NV-11 | Favorites |
| NV-12 | Visits |
| NV-13 | Chat |
| NV-14 | Rental Closing |
| NV-15 | Reviews |
| NV-16 | Notifications |
| NV-17 | Smart Match |

- **Estado:** PROPUESTO.
- **Justificación:** separar incrementos de negocio y evitar implementación sin diseño.
- **Impacto:** cada incremento necesitará su propia spec, plan, task y aprobación humana. El roadmap no aprueba ejecución ni crea contratos.

## M-BE-024 — Prioridad académica próxima

- **Decisión:** priorizar Registration, Login, Logout, cuatro endpoints adicionales y backend desplegado en el siguiente ciclo de revisión.
- **Estado:** APROBADO.
- **Justificación:** prioridad académica comunicada; requiere una entrega pequeña y demostrable.
- **Impacto:** los cuatro endpoints adicionales aún no están definidos; su selección, contratos y aceptación quedan pendientes. La prioridad no permite omitir fundamentos, seguridad, costo $0 ni aprobación del despliegue.

## M-BE-025 — UnitTest residual

- **Decisión:** conservar `UnitTest/` y reportarlo sin eliminar.
- **Estado:** PENDIENTE.
- **Justificación:** contiene solo `obj/` con `project.assets.json`, `project.nuget.cache`, `UnitTest.csproj.nuget.dgspec.json`, `UnitTest.csproj.nuget.g.props` y `UnitTest.csproj.nuget.g.targets`; el dgspec referencia el antiguo UnitTest.csproj, que no existe físicamente allí ni está en la solución.
- **Impacto:** parece residuo de restauración del proyecto renombrado, sin código fuente. Cualquier limpieza requiere autorización humana independiente; esta entrega no toca su contenido.

## M-BE-026 — Estado de las specs y decisiones pendientes

- **Decisión:** preparar NV-01 Foundation, NV-02 DevSecOps y NV-03 Scaling Cloud con trazabilidad completa y todas las tasks abiertas.
- **Estado:** APROBADO.
- **Justificación:** completar exclusivamente gobierno y planificación, no implementación.
- **Impacto:** las tres specs, planes y task lists siguen en borrador: **⛔ NO EJECUTAR**. Pendientes de revisión humana: convenciones HTTP, umbrales de calidad, contratos consumidores, SLO de carga, módulos de negocio, proveedor/servicios cloud, elegibilidad $0 y autorización de rutas futuras. Siguiente paso: revisar y autorizar la primera spec, sin iniciar código automáticamente.

## M-BE-027 — Separación SDD/implementación y verificación iterativa DevSecOps

- **ID:** M-BE-027
- **Decisión:** Separar obligatoriamente la preparación SDD de la implementación y exigir verificación iterativa de coherencia SDD y seguridad durante cada incremento ejecutable.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-02.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** Evitar que un agente interprete la creación de una spec como autorización de implementación y detectar contradicciones, secretos, fallos de autorización, validación o exposición de datos antes de cerrar cada task.
- **Impacto:** Todos los agentes, specs y futuras implementaciones del Backend deberán respetar este flujo. Ninguna task podrá cerrarse con contradicciones SDD o hallazgos bloqueantes de seguridad dentro del alcance.

## M-BE-028 — Azure Container Apps como objetivo de despliegue U2

- **ID:** M-BE-028
- **Decisión:** Designar Microsoft Azure con Azure Container Apps como objetivo de despliegue exclusivo de la Entrega Unidad 2, condicionado a COST-001 y a la demostración de viabilidad de costo $0.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-02.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** autorización humana explícita para la entrega U2; permite planificar imagen, registry, Container Apps, réplicas y observabilidad sobre un proveedor concreto.
- **Impacto:** autorización limitada a U2; no resuelve la matriz comparativa NV-03-R10/T16 ni la decisión general M-BE-011, y no autoriza `terraform apply`, creación de recursos ni gasto. Antes de aplicar debe documentarse servicios, SKU, free allowance, dos réplicas, registry, logging, egress, Redis, MongoDB, tarjeta, riesgo y destrucción; si no se demuestra compatibilidad con COST-001, el resultado es BLOCKED.

## M-BE-029 — Endpoints U2 aprobados

- **ID:** M-BE-029
- **Decisión:** Unidad 2 utilizará exactamente estos siete endpoints: `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`, `GET /api/v1/properties`, `GET /api/v1/properties/{id}`, `GET /api/v1/universities` y `GET /api/v1/universities/{id}`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-02.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** cerrar el alcance académico de U2 y evitar invención, sustitución o fusión de endpoints durante la implementación.
- **Impacto:** no se agregan endpoints a U2 sin nueva spec y aprobación humana. La decisión no autoriza implementación; la ejecución sigue bloqueada hasta la autorización de Fase B. Los contratos y tasks están en NV-04 a NV-08.

## M-BE-030 — División funcional NV-04 a NV-08

- **ID:** M-BE-030
- **Decisión:** dividir U2 en cinco specs funcionales independientes: NV-04 User Registration, NV-05 Login, NV-06 Logout, NV-07 Property Core (solo lectura de listado y detalle) y NV-08 University Catalog (solo lectura de listado y detalle, con seed mínimo).
- **Estado:** APROBADO.
- **Fecha:** 2026-11-02.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** aplicar SDD-002 y mantener requisitos, planes, tasks y evidencia separados por incremento, sin fusionar scopes.
- **Impacto:** cada una de las cinco specs funcionales tiene `spec.md`, `plan.md` y `task.md` en versión `1.0.0-u2`, aprobación documental y ejecución bloqueada. NV-07 no autoriza CRUD ni módulos de favoritos, visitas, chat o Smart Match. NV-08 no autoriza búsqueda avanzada ni catálogo masivo. NV-02 permanece ⛔ NO EJECUTAR.

## M-BE-031 — Reemisión documental NV-01/NV-03 1.1.0-u2

- **ID:** M-BE-031
- **Decisión:** reemitir NV-01 Foundation y NV-03 Scaling Cloud como versión `1.1.0-u2`, con tasks U2 explícitas, límites técnicos de MongoDB/Redis/JWT en NV-01 y Azure Container Apps en NV-03.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-02.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** superar el bloqueo del preflight U2 sin implementar, manteniendo la separación entre documentación aprobada y ejecución autorizada.
- **Impacto:** la documentación queda aprobada para U2, pero la implementación sigue **BLOCKED** hasta la autorización de Fase B. `terraform apply` y `terraform destroy` requieren confirmación humana separada. M-BE-028 sigue vigente y M-BE-011 continúa pendiente para la selección general de proveedor. La referencia de M-BE-028 a NV-03-R10/T16 se interpreta conforme a esta versión: R10/T16 documentan evidencia de costo Azure U2, no una matriz comparativa general.

## M-BE-032 — Hash de contraseñas con PBKDF2 210000 iteraciones

- **ID:** M-BE-032
- **Decisión:** usar PBKDF2-HMAC-SHA256 con 210000 iteraciones, sal aleatoria de 16 bytes y subclave de 32 bytes, con formato versionado `pbkdf2-sha256$210000$...`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** aumentar el costo de la fuerza bruta sin almacenar contraseñas en claro; la implementación y las pruebas de seguridad ya verifican el parámetro.
- **Impacto:** cualquier reducción de iteraciones o cambio de algoritmo requiere nueva decisión y migración controlada. No se almacenan credenciales ni sales en repositorios, logs o evidencia.

## M-BE-033 — Rate limit distribuido en Redis con script Lua

- **ID:** M-BE-033
- **Decisión:** aplicar los límites de intentos con un contador atómico en Redis mediante script Lua (`INCR`, `PEXPIRE`, `PTTL`), compartido entre réplicas.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** evitar ventanas de carrera y límites inconsistentes cuando hay más de una réplica; no usar memoria local como respaldo de seguridad.
- **Impacto:** si Redis no está disponible, la operación se degrada de forma controlada y no abre controles; la disponibilidad del rate limit depende de Redis. Los cambios de claves, TTL o umbrales requieren nueva decisión y pruebas.

## M-BE-034 — Redis en Upstash gratuito en lugar de Redis Cloud

- **ID:** M-BE-034
- **Decisión:** usar Redis en Upstash con su plan gratuito para el estado compartido de U2, en lugar de Redis Cloud.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** cubrir revocación y límites distribuidos dentro de COST-001 sin introducir un recurso facturable.
- **Impacto:** la conexión TLS y las credenciales se administran como secreto fuera de Terraform y fuera del repositorio. El límite de conexiones, la latencia y la disponibilidad del plan gratuito deben revisarse antes de ampliar carga; el proveedor no se aprovisiona mediante Terraform.

## M-BE-035 — MongoDB Atlas M0

- **ID:** M-BE-035
- **Decisión:** usar MongoDB Atlas M0 gratuito para la persistencia de U2, con TLS y credenciales administradas fuera de Terraform.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** disponer de persistencia administrada sin crear un recurso MongoDB facturable en Azure.
- **Impacto:** Atlas M0 no se aprovisiona con Terraform y su endpoint es público con autenticación TLS; la aceptación literal de T07 sobre no exponer la base a Internet queda **REQUIERE DECISIÓN HUMANA** y T07 permanece abierta. No se deben imprimir cadenas de conexión ni credenciales.

## M-BE-036 — Imagen GHCR desplegada por digest

- **ID:** M-BE-036
- **Decisión:** publicar la imagen en `ghcr.io/thehouse-team/thehouse-backend` y desplegarla por digest OCI inmutable `@sha256:...`; prohibir `latest` y etiquetas mutables.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** garantizar que cada revisión ejecute exactamente el artefacto publicado y trazable a un commit.
- **Impacto:** la imagen actualmente desplegada es `@sha256:15f196af9c32fc843cf1723b4615f896668b0368b292e1246807ab0d03194775`, asociada al commit `c6518862788407fe678c1a524ad4d4b764dc6a71`; el HEAD posterior no está desplegado. Terraform valida el digest y no se permiten credenciales GHCR en Git, tfvars o state.

## M-BE-037 — Logs solo en streaming, sin Log Analytics

- **ID:** M-BE-037
- **Decisión:** mantener los logs de Container Apps en Streaming Only, sin aprovisionar Log Analytics Workspace ni Application Insights para U2.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** preservar COST-001 y evitar un recurso de observabilidad facturable.
- **Impacto:** no hay retención centralizada de trazas, métricas o logs; la evidencia operativa debe capturarse durante la ventana de verificación. Las tasks T11 y T12 permanecen abiertas porque no se demuestra correlación entre réplicas ni costo/retensión de telemetría.

## M-BE-038 — Secretos y variables administrados fuera de Terraform

- **ID:** M-BE-038
- **Decisión:** administrar secretos y variables de ejecución de Container Apps fuera de Terraform; el template de IaC solo crea el bootstrap y declara `ignore_changes` para secretos y variables de entorno.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** impedir que cadenas de conexión, claves JWT, credenciales de Redis o MongoDB entren en `.tf`, `terraform.tfvars` o `terraform.tfstate`.
- **Impacto:** el estado real puede divergir del bootstrap y debe revisarse con `terraform plan`; no se deben revertir variables validadas en Azure. La ausencia de secretos en el repositorio no autoriza a imprimirlos en evidencia o reportes.

## M-BE-039 — Réplicas 0–2 en operación y 2–2 solo en evaluación

- **ID:** M-BE-039
- **Decisión:** operar NV-03 con mínimo 0 y máximo 2 réplicas (scale-to-zero); usar exactamente 2–2 réplicas únicamente durante una ventana controlada de evaluación.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** reducir consumo fuera de la ventana de evaluación sin perder la capacidad de escalar; la regla HTTP `http-scaler` con `concurrent_requests=10` controla la escala.
- **Impacto:** el despliegue actual está en 0–2 y puede tener cero o una réplica; no satisface por sí solo la aceptación de dos réplicas de T08, T18 o T19. La ventana 2–2 debe activarse y evidenciarse de forma explícita. Esta decisión actualiza el estado de NV-03 frente a M-BE-026: T13, T15 y T23 quedan con evidencia; el resto sigue abierto.

## M-BE-040 — Responsable del backend

- **ID:** M-BE-040
- **Decisión:** designar a Victor Manuel Jose Jose como responsable del backend desde 2026-11-05.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** centralizar la responsabilidad sobre decisiones técnicas, autorizaciones, evidencia, seguridad y costo del backend.
- **Impacto:** el responsable debe aprobar cambios materiales y revisiones de evidencia; la designación no autoriza por sí sola implementación, `terraform apply`, `terraform destroy` ni gasto.

## M-BE-041 — Adopción de la regla HTTP http-scaler

- **ID:** M-BE-041
- **Decisión:** adoptar en el template Terraform la regla existente `http-scaler` de Azure Container Apps, con `concurrent_requests=10`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** eliminar el drift entre Azure y Terraform sin cambiar el comportamiento operativo ya validado.
- **Impacto:** la regla queda declarada en `infra/terraform/main.tf`; `terraform plan` no propone cambios en recursos y solo actualiza salidas de estado. No se ejecutó `terraform apply`; cualquier cambio futuro de umbral debe registrarse y reflejarse en IaC.

## M-BE-042 — Entorno Testing para pruebas automatizadas

- **ID:** M-BE-042
- **Decisión:** los factories de pruebas usan el entorno `Testing` (`ApiConventions.TestingEnvironment`), que se comporta como Development (OpenAPI visible, sin HSTS) pero no carga `appsettings.Development.json`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** la configuración local de desarrollo contiene conexiones reales a MongoDB y Redis; las pruebas no deben depender de ella ni poder escribir en servicios reales.
- **Impacto:** `dotnet test` pasa con o sin `appsettings.Development.json` presente. Production y Development no cambian su comportamiento.

## M-BE-043 — Seed en Production solo con bandera explícita

- **ID:** M-BE-043
- **Decisión:** los comandos `--seed-universities` y `--seed-properties` se rechazan en Production salvo con la bandera exacta `--allow-production-seed`; la ejecución autorizada se registra en el log como advertencia. La regla vive en `SeedCommandPolicy` y tiene pruebas de seguridad.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** cargar datos de demostración en el entorno de evaluación es intencional, pero no debe lograrse cambiando el nombre del entorno (`ASPNETCORE_ENVIRONMENT=Staging`), que desactiva la protección sin dejar rastro.
- **Impacto:** la bandera no omite `UniversitySeed__Enabled` ni `PropertySeed__Enabled`. NV-07 y NV-08 (spec y evidence) documentan el comando `az containerapp exec` actualizado. Requiere desplegar una imagen que incluya este cambio.

## M-BE-044 — Publicación GHCR desde main

- **ID:** M-BE-044
- **Decisión:** `u2-ghcr-publish.yml` permite la publicación manual (`workflow_dispatch` + `PUBLISH`) desde `main` además de `feature/u2-entrega`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** `feature/u2-entrega` ya se integró a `main` (PR #2); las imágenes nuevas deben salir de la rama integrada.
- **Impacto:** se mantienen el gate manual, la confirmación explícita y el despliegue por digest. `VERSIONING.md` refleja ambas ramas.

## M-BE-045 — Finales de línea LF en el repositorio

- **ID:** M-BE-045
- **Decisión:** `.gitattributes` fija `* text=auto eol=lf`, en línea con `.editorconfig`.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** evitar mezclas CRLF/LF entre Windows y los runners Linux de CI.
- **Impacto:** el índice ya estaba en LF; no hubo renormalización de archivos existentes.

## M-BE-046 — Caché HTTP condicional en propiedades y universidades

- **ID:** M-BE-046
- **Decisión:** NV-07 y NV-08 pasan a 1.1.0-u2 con el requisito R08: los GET públicos de listado y detalle devuelven `ETag` débil por contenido y `Cache-Control: public, max-age=60`, responden 304 a un `If-None-Match` coincidente y mantienen `no-store` sin `ETag` en errores. Tasks nuevas: NV-07 T11–T12 y NV-08 T12–T13.
- **Estado:** APROBADO (documental). Ejecución: PENDIENTE de la autorización humana «EJECUTA».
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** cubrir el requisito académico 9 (caché en frontend): el móvil revalida con `If-None-Match` y evita descargar de nuevo datos públicos sin cambios.
- **Impacto:** no cambia el contrato JSON ni aplica a respuestas autenticadas. El valor `max-age=60` es propuesto y configurable entre 0 y 300 s.

## M-BE-047 — Observabilidad persistente a costo $0

- **ID:** M-BE-047
- **Decisión:** NV-03 pasa a 1.2.0-u2 con el requisito R15: Log Analytics (`PerGB2018`, `daily_quota_gb = 0.1`, retención de 30 días) como destino de logs del entorno de Container Apps (opción A); si el plan propone recrear el entorno o la app, se usa OpenTelemetry hacia Application Insights sobre el mismo workspace (opción B). Tasks nuevas: T24 y T25.
- **Estado:** APROBADO (documental). Ejecución: PENDIENTE de la autorización humana «EJECUTA» y, para el apply, de `OK PARA APPLY`.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** el requisito académico pide un servicio en la nube para logs de eventos y errores; M-BE-037 dejó los logs solo en streaming. La API de precios de Azure (`eastus2`, 2026-11-05) muestra la ingestión de 0–5 GB/mes a 0.00 USD; el tope diario limita el uso a ~3.1 GB/mes.
- **Impacto:** sustituye a M-BE-037 una vez ejecutado T24. Antes del apply debe verificarse en la documentación vigente que el tramo gratuito aplica a la cuenta y que 30 días de retención están incluidos; si no, BLOCKED. Al llegar al tope diario se pierden los logs restantes de ese día (NV-03-RK11).

## M-BE-048 — Errores de lecturas públicas con no-store y max-age configurable

- **ID:** M-BE-048
- **Decisión:** los errores de `/api/v1/properties` y `/api/v1/universities` envían `Cache-Control: no-store`; el `max-age` de las respuestas cacheables se configura con `Foundation:PublicCacheMaxAgeSeconds` (0–300, por defecto 60).
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** al implementar M-BE-046 las pruebas mostraron que esos errores no enviaban `no-store`, contrario a NV-07-R08 y NV-08-R08; también se observó así en Azure el 2026-11-04.
- **Impacto:** ningún error de estos endpoints es cacheable por clientes o intermediarios. Configurar el `max-age` fuera de rango impide el arranque.

## M-BE-049 — NV-02 como CI y NV-09 como CD

- **ID:** M-BE-049
- **Decisión:** reemitir NV-02 como "CI y DevSecOps" versión `1.1.0-u2` (workflow `ci.yml` en cada pull request y push a `main` con build, pruebas, cobertura, Semgrep, SCA, SBOM, Gitleaks, Trivy, Dockle, Terraform, mutación programada y quality gate) y especificar la entrega continua en una spec nueva NV-09 (publicación, firma, despliegue manual con aprobación, smoke tests, DAST, fuzzing y performance). El roadmap funcional de M-BE-023 se recorre a partir de NV-10 cuando se reemita.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose («EJECUTA» de NV-02 1.1.0-u2).
- **Justificación:** en la revisión académica del 2026-11-05 el profesor observó que no hay CI/CD visible en los pull requests: los workflows existentes solo corren en `feature/u2-entrega` o de forma manual. Separar CI y CD sigue la estructura pedida: base → infraestructura → endpoints → CI → CD.
- **Impacto:** `u2-linux-validation.yml` y `u2-container-validation.yml` se retirarán al implementar `ci.yml`; `u2-ghcr-publish.yml` pasa a NV-09. La CI no usa secretos ni despliega.

## M-BE-050 — Ruleset de main no aplicado por plan y visibilidad

- **ID:** M-BE-050
- **Decisión:** registrar que el ruleset "Main Proteccion" de `main` no se aplica: la organización `thehouse-team` está en el plan Free y el repositorio es privado (respuesta de la API de GitHub del 2026-11-05: "Upgrade to GitHub Pro or make this repository public to enable this feature"). Recomendación: hacer público el repositorio después de que Gitleaks pase sobre todo el historial; alternativa: GitHub Pro/Team con beneficio educativo.
- **Estado:** APROBADO — repositorio hecho público por el equipo el 2026-11-05.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose.
- **Justificación:** sin aplicación del ruleset, los checks requeridos y la aprobación obligatoria no bloquean el merge. Un repositorio público además da minutos ilimitados de Actions y habilita gratis SARIF en code scanning y SonarCloud (COST-001).
- **Impacto:** antes de hacerlo público se buscó localmente en todo el historial patrones de secretos, sin hallazgos (Gitleaks lo confirmará en CI). Con el repositorio público, `main` todavía no tiene ruleset: las capturas del profesor eran la referencia a replicar. Se crea después del primer run de CI con los checks requeridos (NV-02 T14). **Actualización 2026-11-05:** ruleset `Main Proteccion` (ID 26657131) creado por Victor y activo, sin bypass, con 1 aprobación y los 7 checks de `CI` requeridos; verificado con la API (NV-02 `evidence.md` §9).

## M-BE-051 — Aprobación de NV-09 (CD) y sus decisiones

- **ID:** M-BE-051
- **Decisión:** aprobar NV-09 1.0.0-u2 y autorizar T01–T14 con las decisiones de su §7: el CD es dueño de la imagen (`ignore_changes` sobre `template[0].container[0].image`); identidad administrada `id-thehouse-u2-deploy` con credencial federada solo para el Environment `production`; rol de mínimo privilegio con alcance al recurso; Environment `production` con Victor e Isai como revisores, sin autoaprobación y solo desde `main`; firma Cosign keyless; smoke en producción sin escrituras persistentes; DAST activo solo en el entorno efímero y pasivo en producción; seed en producción como operación humana; `VERSIONING.md` actualizado por pull request.
- **Estado:** APROBADO.
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose («EJECUTA NV-09 1.0.0-u2, tasks T01–T14»).
- **Justificación:** observación del profesor (sin CD) y despliegue manual desde la computadora del responsable; se busca una entrega trazable, firmada y aprobada, a costo $0 y sin secretos de larga duración.
- **Impacto:** `terraform apply` de T01/T02 sigue requiriendo «OK PARA APPLY». T15–T19 (RESTler, performance, primer despliegue, seed y cierre) no están autorizadas. NBomber queda PENDIENTE DE VERIFICAR por licencia.

## M-BE-052 — Rol personalizado para el CD en lugar del rol integrado

- **ID:** M-BE-052
- **Decisión:** usar el rol personalizado `TheHouse CD Deployer (rg-thehouse-u2)` (lectura y escritura de la Container App, lectura de revisiones y réplicas, `managedEnvironments/join/action` y lectura de operaciones) asignado a `ca-thehouse-u2` y a `cae-thehouse-u2`, en lugar del rol integrado `Container Apps Contributor`.
- **Estado:** APROBADO (aplicación de la §7 de NV-09: "rol integrado si cubre la actualización, o personalizado").
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose (vía M-BE-051).
- **Justificación:** el rol integrado incluye `Microsoft.App/containerApps/*/delete` y `*/action`, que abarca `listSecrets`: la identidad del CD podría borrar la app o leer las cadenas de conexión y la llave JWT.
- **Impacto:** si `az containerapp update` requiere una acción adicional, el primer despliegue fallará sin cambiar la app y la acción se agregará con registro, nunca `listSecrets` ni `delete` sin una decisión nueva.

## M-BE-053 — MongoDB 7.0 en el entorno efímero del CD

- **ID:** M-BE-053
- **Decisión:** usar `mongo:7.0.45` como servicio del entorno efímero de NV-09 (smoke completo y ZAP).
- **Estado:** APROBADO (ajuste técnico dentro de T09).
- **Fecha:** 2026-11-05.
- **Aprobador:** Victor Manuel Jose Jose (vía M-BE-051).
- **Justificación:** MongoDB 8.0+ no arranca en los runners de GitHub con kernel Linux ≥ 6.19 (tcmalloc/rseq; SERVER-121912 cerrado, SERVER-125742 abierto). Verificado en el run 42170097726.
- **Impacto:** producción sigue en Atlas 8.0. Las consultas de la API son compatibles con 7.0; se vuelve a 8.x cuando MongoDB publique la corrección.

## M-BE-054 — Roadmap funcional reemitido como specs NV-10 a NV-25

- **ID:** M-BE-054
- **Decisión:** sustituir la tabla de M-BE-023 (cuyo NV-09 pasó a ser la CD, M-BE-049) por 16 specs funcionales en borrador que cubren todos los RF de `Requerimientos_TheHouse_v2`: NV-10 Sesión y cuenta, NV-11 Perfil y preferencias, NV-12 Arrendador, edificios y cuartos, NV-13 Fotografías y documentos, NV-14 Búsqueda y mapa, NV-15 Favoritos y comparación, NV-16 Contacto, solicitudes y chat, NV-17 Visitas, NV-18 Cierre de trato con QR y renta, NV-19 Reseñas, NV-20 Notificaciones, NV-21 Verificación y moderación, NV-22 Compañeros de vivienda, NV-23 Smart Match ML, NV-24 Smart Match IA generativa y NV-25 Métricas. El índice `specs/README.md` muestra el mapa y la cobertura RF → spec.
- **Estado:** PROPUESTO — cada spec está en `1.0.0-borrador` y requiere aprobación documental y «EJECUTA» propios.
- **Fecha:** 2026-11-06.
- **Aprobador:** pendiente (Victor Manuel Jose Jose).
- **Justificación:** observación del profesor del 2026-11-05 ("faltan specs para todo el proyecto, no solo los 7 endpoints").
- **Impacto:** RF-67, RF-69 y RF-74 son solo del cliente móvil; RF-70 (SMS) queda BLOCKED por COST-001. Decisiones abiertas que bloquean tasks: proveedor de correo (NV-10), almacenamiento y cifrado de archivos (NV-13), migración de NV-07 y detalle con sesión (NV-12/NV-14), asignación de roles (NV-21), enfoque de modelo (NV-23) y proveedor de IA generativa (NV-24).

## M-BE-055 — Auditoría SDD de las specs NV-01 a NV-09

- **ID:** M-BE-055
- **Decisión:** corregir solo documentación: estados de spec/plan alineados con `task.md`, conteos de tasks completadas, referencias obsoletas a NV-02 bloqueada, trazabilidad bidireccional (NV-03, NV-07, NV-08) y requerimientos de origen RF/RNF en cada spec. Las `evidence.md` no se modifican porque son registros fechados.
- **Estado:** APLICADO EN BORRADOR — pendiente de revisión de Victor en el pull request.
- **Fecha:** 2026-11-06.
- **Aprobador:** pendiente (Victor Manuel Jose Jose).
- **Justificación:** las inconsistencias de estado y trazabilidad contradicen la regla de un solo estado por spec y dificultan la revisión del profesor.
- **Impacto:** sin cambios de código ni de infraestructura. Hallazgos y correcciones en `specs/README.md` §5. Queda registrada la contradicción RF-24 (detalle con sesión) frente a NV-07 (detalle público) como decisión PENDIENTE en NV-14.

