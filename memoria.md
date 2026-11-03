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
