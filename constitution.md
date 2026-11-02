# TheHouse Backend — Constitution

Versión: 1.0.0. Autoridad: máxima norma del Backend dentro de las instrucciones humanas y políticas del entorno. Alcance de esta entrega: documentación SDD, sin implementación.

## 1. Propósito

Gobernar un backend académico seguro, verificable, mantenible y escalable para TheHouse. Las decisiones deben ser trazables y diferenciar lo existente, lo aprobado como dirección y lo pendiente de implementar.

## 2. Alcance y precondiciones técnicas

La solución `TheHouse.slnx` y sus siete proyectos ya existen y su bootstrap está terminado. La compilación de referencia fue correcta, con 0 errores y 0 advertencias; no se vuelve a ejecutar durante esta entrega documental.

Quedan protegidos `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/`, la solución, todos los `.csproj`, `Program.cs`, `appsettings*.json`, `Controllers/` y `Properties/`. No se recrean, renombran, mueven ni cambian sus referencias. Una futura implementación requerirá levantar explícitamente la restricción aplicable mediante autorización humana de alcance preciso.

## 3. SDD obligatorio — SDD-001

> Ninguna implementación puede comenzar sin una spec, plan y task aprobados.

La preparación de estos documentos no equivale a aprobarlos ni a ejecutarlos. Cada autorización debe identificar aprobador humano, fecha, versiones de los tres documentos, tasks autorizadas, rutas permitidas y restricciones. Una edición material invalida la aprobación de los artefactos afectados hasta nueva revisión. Un agente no puede autoaprobarse.

## 4. Specs independientes — SDD-002

> Cada incremento funcional o arquitectónico relevante deberá tener una spec independiente.

Cada carpeta contiene `spec.md`, `plan.md` y `task.md`. La cadena obligatoria es Requirement → Plan Phase → Task → evidencia. No existen requisitos sin tasks ni tasks sin requisito. Las dependencias entre specs se declaran; no otorgan autorización transitiva. NV-01, NV-02 y NV-03 están en borrador y en estado **⛔ NO EJECUTAR**.

## 5. Clean Architecture

Domain contiene reglas de negocio puras; Application coordina casos de uso y define puertos; Infrastructure implementa adaptadores; TheHouse aloja ASP.NET Core, HTTP y el composition root. No se filtran tipos HTTP, BSON, Redis ni detalles de proveedores a Domain. Los DTO de transporte no sustituyen modelos de dominio.

## 6. Modular Monolith

Se adopta un monolito modular, no microservicios. Los módulos futuros tendrán propiedad explícita sobre sus casos de uso y datos, comunicación por contratos y ausencia de dependencias circulares. No se crean entidades o módulos funcionales en esta entrega.

## 7. Dependency Rule y arquitectura actual

Las referencias internas actuales, que no deben modificarse, son:

```text
TheHouse
├── TheHouse.Application
└── TheHouse.Infrastructure

TheHouse.Infrastructure
├── TheHouse.Application
└── TheHouse.Domain

TheHouse.Application
└── TheHouse.Domain

TheHouse.Domain
└── ninguna dependencia interna
```

La referencia del host a Infrastructure existe para composición, no para colocar lógica de negocio en controllers. Las dependencias apuntan hacia el interior; Domain no referencia Application, Infrastructure o la API.

## 8. REST y JSON

Contratos HTTP orientados a recursos, semántica REST, JSON, métodos y códigos de estado coherentes. Validación, paginación, idempotencia de operaciones que lo requieran y errores Problem Details deben quedar especificados antes del código. No se inventan endpoints funcionales durante el gobierno inicial.

## 9. OpenAPI

Cada operación futura deberá tener contrato OpenAPI coherente con la implementación: versión, esquemas, errores, autenticación y ejemplos sin datos sensibles. Los cambios incompatibles necesitan una estrategia explícita de evolución y comprobación contractual. NV-01 define la base; NV-02 planifica su verificación.

## 10. HTTPS y TLS

Todo acceso público requiere HTTPS con certificados válidos. HSTS, terminación TLS, renovación y confianza en cabeceras de proxy se decidirán y probarán. No se desactiva la validación de certificados ni se confía en proxies arbitrarios. La conexión a MongoDB y Redis debe protegerse conforme al entorno aprobado.

## 11. MongoDB

MongoDB es la persistencia aprobada y `MongoDB.Driver` el driver previsto. Sus detalles pertenecen a Infrastructure; conexiones reutilizables, límites, cancelación y timeouts deben diseñarse antes de implementarse. No existe autorización para instalarlo, conectarlo o crear repositorios ahora.

## 12. NoSQL

El modelo documental se diseña a partir de patrones de acceso, límites de agregados, consistencia, índices y crecimiento de documentos. Embedding, referencias, índices únicos/TTL y atomicidad necesitan justificación. SQL Server y EF Core de los repositorios de referencia no se adoptan por imitación.

## 13. Redis distribuido

Redis es el estado compartido previsto para caché distribuida y, cuando exista una spec de autenticación, controles distribuidos que correspondan. Claves con namespace, TTL, invalidación, atomicidad y comportamiento ante fallos son obligatorios. Redis no sustituye automáticamente la fuente de verdad persistente.

## 14. Stateless

Ninguna instancia de API debe ser propietaria exclusiva de sesiones, revocación de tokens, bloqueos de acceso u otro estado necesario para la corrección. Stateless no significa ausencia de estado: el estado necesario reside en servicios compartidos. No se permite fallback local de seguridad que produzca decisiones distintas entre réplicas.

## 15. Mínimo dos instancias

La arquitectura objetivo debe demostrar funcionamiento con al menos dos instancias de la misma API. Reiniciar o reemplazar una instancia no puede perder estado de negocio ni eludir controles de seguridad. La existencia actual de una solución no constituye evidencia de escalado.

## 16. Load Balancer

Un balanceador distribuirá tráfico a las instancias con health checks y retirada de instancias no listas. No se aceptan sticky sessions como solución al estado local. Deben probarse cabeceras reenviadas, IP del cliente, distribución y fallos.

## 17. COST-001 — costo obligatorio $0

> El costo obligatorio del entorno académico deberá permanecer en $0.

**Todo recurso académico: Costo obligatorio = $0.** Aplica a cómputo, dos instancias, Load Balancer, TLS, red/egress, almacenamiento/CDN, Redis, conectividad MongoDB, observabilidad, CI/CD, registros y retención de artefactos. Un free tier, un crédito o una alerta de presupuesto no prueban costo cero ni constituyen un límite duro de gasto.

Antes de provisionar se exige evidencia vigente de elegibilidad, región, cuotas, duración, tarjeta, riesgo de cobro, destrucción y costo esperado total. Ante incertidumbre o imposibilidad de satisfacer simultáneamente $0 y dos instancias/balanceador, el resultado es **BLOCKED**: se presenta la incompatibilidad a revisión humana, sin rebajar el objetivo, contratar ni desplegar. La demostración local puede ser evidencia técnica, nunca una afirmación de despliegue cloud completado.

## 18. Seguridad

Aplicar mínimo privilegio, deny-by-default, validación de entrada, autorización por recurso, protección de datos y modelos de amenazas. Limitar cuerpos, tiempo de ejecución y exposición de diagnósticos. CORS y headers requieren políticas explícitas. No devolver trazas internas ni registrar contraseñas, tokens o cadenas de conexión.

## 19. JWT futuro

JWT es tecnología aprobada para una futura spec de Login, no una funcionalidad existente. Se deberán validar firma, algoritmo permitido, emisor, audiencia y expiración; definir rotación de claves, claims mínimos y pruebas negativas. No se fijan duraciones o algoritmos copiando ejemplos sin análisis.

## 20. Refresh Tokens

La futura autenticación especificará tokens de refresco revocables, almacenamiento seguro mediante hash, rotación y detección de reutilización, expiración y protección contra concurrencia. El mecanismo de transporte y sus riesgos de CSRF/XSS se decidirán por contrato, sin implementarlos ahora.

## 21. Logout

Una spec independiente definirá revocación de refresh tokens y semántica de invalidación de JWT de acceso, incluido su alcance entre instancias y tiempo efectivo. Eliminar un token del cliente no prueba revocación en servidor. El estado de seguridad debe compartirse y no degradarse a memoria local.

## 22. Rate Limiting

Las políticas deberán distinguir endpoints, identidad/IP y límites globales de límites por instancia. Respuestas 429 y `Retry-After` se documentarán. Un limitador local no se presentará como cuota global bajo balanceo; la coordinación distribuida y el tratamiento de fallos se validarán en NV-03.

## 23. Caché

Cache-aside será evaluado por caso de uso: claves versionadas, TTL explícito, invalidación tras cambios confirmados, prevención de stampede y ausencia de filtración entre usuarios. No cachear secretos ni respuestas privadas sin aislamiento y autorización. El fallback a MongoDB puede admitirse para datos no críticos, pero no para eludir controles de seguridad.

## 24. Observabilidad

OpenTelemetry es la tecnología prevista para trazas, métricas y correlación con logs estructurados. Definir correlation/trace IDs, latencia, errores, disponibilidad y dependencias; evitar PII y cardinalidad ilimitada. Retención y exportadores deben cumplir COST-001. Health checks no son sustitutos de telemetría ni deben exponer credenciales.

## 25. Testing

`TheHouse.UnitTests` verifica reglas de Domain/Application de forma aislada; `TheHouse.IntegrationTests` verificará composición, HTTP y adaptadores; `TheHouse.SecurityTests` verificará abuso, autorización y controles defensivos. xUnit ya está referenciado; la prueba unitaria actual es un placeholder, no cobertura funcional.

Se planifican WebApplicationFactory, Mutation Testing con Stryker.NET, Contract Testing con PactNet, Performance Testing con NBomber, DAST con OWASP ZAP y REST Fuzzing con RESTler. Son herramientas previstas, no instaladas por esta entrega. Ninguna suite vacía representa un control satisfecho.

## 26. DevSecOps

Se prevén SAST (Semgrep, SonarCloud/SonarAnalyzer), SCA (NuGet vulnerability scanning), SBOM (CycloneDX), imágenes (Trivy, Dockle), firma (Cosign), DAST y fuzzing. Findings y excepciones tendrán severidad, responsable, vencimiento y evidencia. Un fallo del scanner o un control obligatorio no ejecutado no equivale a PASS.

Referencias metodológicas consultadas solo en lectura: [WebAPIDevSecOps](https://github.com/edelomeza/WebAPIDevSecOps) y [WebAPIDevSecOpsScalling](https://github.com/edelomeza/WebAPIDevSecOpsScalling). No se clona ni copia su código; sus umbrales, SQL Server, servicios adicionales o fallbacks no son decisiones de TheHouse.

## 27. CI/CD

GitHub Actions es la plataforma prevista. La futura cadena será verificable: build y testing, SAST/SCA, SBOM, contrato/mutación/performance según alcance, imagen/escaneo/firma y DAST/fuzzing en entornos aislados. Gates, permisos mínimos, dependencias fijadas y evidencias preceden cualquier despliegue. No crear workflows ni activar pipelines en esta entrega.

## 28. Docker

La contenerización futura deberá usar imágenes mínimas, usuario no root, versiones/digests controlados, ausencia de secretos, límites y escaneo. Se verificará el costo de licencias y del registro. No crear Dockerfile, compose ni imágenes ahora.

## 29. Terraform

La infraestructura futura se describirá declarativamente y separará plan, aplicación y destrucción con aprobaciones. Estado y credenciales no se versionan ni exponen. Destruir recursos requiere alcance e inventario autorizados; no se supone que todo recurso conlleve limpieza automática ni que `destroy` elimine recursos externos.

## 30. AWS / Azure / Google Cloud

Son los únicos proveedores cloud permitidos. **Ninguno está seleccionado.** NV-03 debe comparar compute, dos instancias, Load Balancer, HTTPS, container hosting, observabilidad, storage/CDN, Redis, conectividad MongoDB, Terraform, tarjeta, riesgo y costo esperado. Los servicios concretos y tarifas quedan pendientes de evidencia vigente y revisión humana.

## 31. Gestión de secretos

Usar variables de entorno, almacenes de secretos u otros mecanismos aprobados con costo $0 y mínimo privilegio. En documentación solo placeholders. Nunca escribir credenciales en specs, repositorio, logs, imágenes o estado Terraform expuesto. Una filtración exige revocación/rotación y revisión; borrarla del último archivo no basta.

## 32. Versionamiento

Documentos y entregas usarán versiones identificables; los contratos API tendrán estrategia de versión y deprecación definida en NV-01. Cambios incompatibles requieren nueva aprobación. Versionar contrato, imagen e infraestructura de forma trazable sin asumir que una aprobación cubre revisiones futuras.

## 33. Git Flow

Convención futura: `main` estable, `develop` de integración, `feature/<spec>-<descripcion>`, `release/<version>` y `hotfix/<descripcion>`. PR con trazabilidad y revisión humana antes de integrar. Esta convención no autoriza crear ramas, inicializar Git, hacer commit o push; no se realiza ninguna de esas acciones en esta entrega.

## 34. Reglas para agentes

Orden normativo: constitution → AGENTS → memoria → spec activa → plan activo → task activo. Las skills y perfiles no alteran ese orden. Antes de implementar, todo agente identifica SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVO y ESTADO AUTORIZADO. Sin autorización verificable: **BLOCKED**. PASS del guardian es revisión de cumplimiento, no permiso humano. No inventar evidencia, aprobación, métricas o costos.

## 35. Prohibiciones y cambios de gobierno

En esta etapa solo se crean los artefactos SDD solicitados. Se prohíbe modificar código, proyectos, referencias, configuración de aplicación, crear entidades/controllers/endpoints/repositorios, implementar MongoDB/Redis/JWT, instalar paquetes, generar infraestructura, Dockerfile, Terraform o GitHub Actions, ejecutar specs o operaciones Git de escritura. No crear carpetas `src` o `tests` ni nuevas carpetas del roadmap funcional.

No eliminar `UnitTest/` residual sin autorización humana. No clonar referencias, copiar código, seleccionar cloud ni asumir gratuidad. Cualquier cambio de constitución o excepción necesita revisión humana explícita, registro de decisión y análisis de impacto; no se relaja COST-001 de manera unilateral. Ante conflicto de alcance o ausencia de evidencia, detener la implementación.
