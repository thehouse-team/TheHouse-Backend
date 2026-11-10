# TheHouse Backend — Constitution

| Campo | Valor |
| --- | --- |
| Versión | 1.2.0 |
| Fecha | 2026-11-10 |
| Estado | **✅ APROBADA — VIGENTE** (sustituye a la versión 1.1.0) |
| Aprobación humana | Victor Manuel Jose Jose, responsable del backend (M-BE-040) — 2026-11-10 («Apruebo 1.2.0») |
| Decisión | M-BE-062 |

Autoridad: máxima norma del Backend dentro de las instrucciones humanas y políticas del entorno.

**Cambios 1.2.0 (2026-11-10):** reconoce los dos proyectos separados que pidió el profesor sin romper el monolito modular: `TheHouse-ML` como **satélite de artefacto** (no se despliega; entrega un modelo ONNX que el backend carga en proceso) y `TheHouse-GenAI` como **servicio auxiliar** (se despliega aparte, solo lo llama el backend y su ausencia no rompe ninguna función). Agrega §6.1, precisa §15, §17 y §30 y actualiza §2. Las demás reglas no cambian. Detalle en M-BE-062.

**Cambios 1.1.0 (2026-11-06):** la versión 1.0.0 describía la fase inicial solo documental. Esta versión conserva todas las reglas de fondo (SDD, COST-001, seguridad, Clean Architecture, monolito modular) y actualiza lo que quedó desfasado tras ejecutar NV-01 a NV-09: estado de la solución, proveedor cloud seleccionado, CI/CD vigente, protocolo de autorizaciones y flujo de Git. Detalle en `specs/README.md` §6.2 y en M-BE-056/M-BE-057.

## 1. Propósito

Gobernar un backend académico seguro, verificable, mantenible y escalable para TheHouse. Las decisiones deben ser trazables y diferenciar lo existente, lo aprobado como dirección y lo pendiente de implementar.

## 2. Alcance y estado técnico

La solución `TheHouse.slnx` y sus siete proyectos existen. Con autorización humana se implementaron y verificaron NV-01 (fundamentos), NV-04 a NV-08 (registro, login, logout, propiedades y universidades) y la infraestructura de NV-03; la CI (NV-02) y la CD (NV-09) están operativas y el backend corre en Azure. Después se completaron NV-10 a NV-12, NV-14, NV-15, NV-17 y NV-21. El estado vigente de cada spec vive en su `task.md` y se resume en `specs/README.md`.

Existen dos proyectos separados del backend, gobernados por §6.1: `thehouse-team/TheHouse-ML` (Smart Match, satélite de artefacto) y `thehouse-team/TheHouse-GenAI` (IA generativa, servicio auxiliar). Cada uno tiene su propio repositorio, constitución y specs; ninguno modifica este repositorio.

Quedan protegidos `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/`, la solución, todos los `.csproj`, `Program.cs`, `appsettings*.json`, `Controllers/` y `Properties/`. Solo se modifican cuando una autorización «EJECUTA» incluye esas rutas de forma explícita. No se recrean, renombran ni mueven proyectos, y agregar proyectos o paquetes requiere que la autorización lo nombre.

## 3. SDD obligatorio — SDD-001

> Ninguna implementación puede comenzar sin una spec, plan y task aprobados.

La preparación de estos documentos no equivale a aprobarlos ni a ejecutarlos. Cada autorización debe identificar aprobador humano, fecha, versiones de los tres documentos, tasks autorizadas, rutas permitidas y restricciones. Una edición material invalida la aprobación de los artefactos afectados hasta nueva revisión. Un agente no puede autoaprobarse.

## 4. Specs independientes — SDD-002

> Cada incremento funcional o arquitectónico relevante deberá tener una spec independiente.

Cada carpeta contiene `spec.md`, `plan.md` y `task.md`, y `evidence.md` cuando hay ejecución. La cadena obligatoria es Requerimiento de origen (RF/RNF) → Requirement → Plan Phase → Task → evidencia. No existen requisitos sin tasks ni tasks sin requisito, y la trazabilidad es bidireccional. Las dependencias entre specs se declaran; no otorgan autorización transitiva. Los tres documentos de una spec muestran el mismo estado. El índice `specs/README.md` mantiene el mapa de specs y la cobertura de requerimientos.

## 5. Clean Architecture

Domain contiene reglas de negocio puras; Application coordina casos de uso y define puertos; Infrastructure implementa adaptadores; TheHouse aloja ASP.NET Core, HTTP y el composition root. No se filtran tipos HTTP, BSON, Redis, librerías de ML ni detalles de proveedores a Domain. Los DTO de transporte no sustituyen modelos de dominio.

## 6. Modular Monolith

Se adopta un monolito modular, no microservicios. Cada módulo tiene propiedad exclusiva sobre sus casos de uso y sus colecciones; otros módulos solo acceden mediante contratos (puertos de Application) o eventos de dominio en proceso, idempotentes y sin dependencias circulares. Separar un módulo en un servicio desplegable aparte requiere reformar esta sección con revisión humana.

### 6.1 Proyectos separados del backend

El backend sigue siendo un monolito modular: ningún módulo se separa. Fuera de él solo existen estos dos tipos de proyecto, cada uno con su repositorio, su constitución, sus specs y su CI:

1. **Satélite de artefacto** (`TheHouse-ML`). No se despliega, no expone API y no guarda datos de usuarios. Entrega artefactos versionados e inmutables (por ejemplo, un modelo ONNX) con SHA-256, esquema de entrada y casos dorados. El backend los carga **en proceso** detrás de un puerto de Application, verifica el hash al cargar y prueba la paridad con los casos dorados. Nunca se carga código ejecutable ni formatos serializados de Python (pickle).
2. **Servicio auxiliar** (`TheHouse-GenAI`). Se despliega aparte y solo se admite si cumple todo lo siguiente:
   - No es fuente de verdad ni dueño de datos: recibe del backend el contexto ya calculado y no puede alterar puntajes, precios, distancias ni disponibilidad.
   - Solo el backend lo llama; Mobile y cualquier cliente externo nunca. La llamada usa credencial de servicio guardada como secreto (§31), nunca el JWT del usuario.
   - Se consume detrás de un puerto de Application con un adaptador en Infrastructure que aplica timeout, reintentos acotados, circuit breaker y límite de tamaño.
   - **Degradación segura:** si falla, tarda o no está configurado, el backend responde igual con un resultado determinista (por ejemplo, una explicación por plantilla) o sin la parte opcional. Ninguna función principal depende de él.
   - Recibe el mínimo de datos: nunca contraseñas, tokens, dirección exacta ni datos privados del arrendador.
   - Su contrato está versionado y se prueba en ambos lados con dobles de prueba, sin red en el CI del backend.
   - COST-001 aplica completo a su hosting y a su proveedor de IA (§17 y §30).

El contrato entre el backend y un proyecto separado se define en una spec del backend y en la spec correspondiente del otro repositorio; cambiarlo requiere nueva versión en ambos. Agregar otro proyecto separado requiere reformar esta sección.

## 7. Dependency Rule y arquitectura actual

Las referencias internas actuales son:

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

La referencia del host a Infrastructure existe para composición, no para colocar lógica de negocio en endpoints. Las dependencias apuntan hacia el interior; Domain no referencia Application, Infrastructure o la API. Cambiar estas referencias requiere autorización explícita.

## 8. REST y JSON

Contratos HTTP orientados a recursos, semántica REST, JSON, métodos y códigos de estado coherentes. Validación, paginación, idempotencia de operaciones que lo requieran y errores Problem Details en español deben quedar especificados en la spec antes del código. No se inventan endpoints fuera de una spec aprobada.

## 9. OpenAPI

Cada operación debe tener contrato OpenAPI coherente con la implementación: versión, esquemas, errores, autenticación y ejemplos sin datos sensibles. El documento se expone solo en `Development` y `Testing`. Los cambios incompatibles necesitan una estrategia explícita de evolución y deprecación.

## 10. HTTPS y TLS

Todo acceso público requiere HTTPS con certificados válidos y HSTS; en Azure Container Apps el TLS termina en el ingress administrado y solo se confía en sus cabeceras reenviadas. No se desactiva la validación de certificados ni se confía en proxies arbitrarios. Las conexiones a MongoDB y Redis usan TLS.

## 11. MongoDB

MongoDB es la persistencia aprobada, con `MongoDB.Driver` en Infrastructure y MongoDB Atlas M0 como servicio (M-BE-035). Conexiones reutilizables, límites, cancelación y timeouts se definen en la spec antes de implementarse.

## 12. NoSQL

El modelo documental se diseña a partir de patrones de acceso, límites de agregados, consistencia, índices y crecimiento de documentos. Embedding, referencias, índices únicos/TTL, transacciones y atomicidad necesitan justificación. SQL Server y EF Core no se adoptan por imitación.

## 13. Redis distribuido

Redis es el estado compartido para caché distribuida y controles de seguridad distribuidos (rate limit, revocación de tokens), con Upstash como servicio (M-BE-034). Claves con namespace, TTL, invalidación, atomicidad y comportamiento ante fallos son obligatorios; ante fallo de Redis, los controles de seguridad fallan cerrados. Redis no sustituye la fuente de verdad persistente.

## 14. Stateless

Ninguna instancia de API debe ser propietaria exclusiva de sesiones, revocación de tokens, bloqueos de acceso u otro estado necesario para la corrección. Stateless no significa ausencia de estado: el estado necesario reside en servicios compartidos. No se permite fallback local de seguridad que produzca decisiones distintas entre réplicas, ni tareas programadas que se ejecuten una vez por instancia.

## 15. Mínimo dos instancias

La arquitectura debe demostrar funcionamiento con al menos dos instancias de la misma API. Reiniciar o reemplazar una instancia no puede perder estado de negocio ni eludir controles de seguridad. La operación normal puede escalar a cero para cumplir COST-001; la demostración con dos réplicas se hace en una ventana controlada (M-BE-039).

Esta regla aplica al backend. Un servicio auxiliar de §6.1 puede operar con una sola instancia porque su caída no afecta ninguna función principal (degradación segura).

## 16. Load Balancer

Un balanceador distribuye tráfico a las instancias con health checks y retirada de instancias no listas; en Azure lo provee el ingress de Container Apps. No se aceptan sticky sessions como solución al estado local. Deben probarse cabeceras reenviadas, IP del cliente, distribución y fallos.

## 17. COST-001 — costo obligatorio $0

> El costo obligatorio del entorno académico deberá permanecer en $0.

**Todo recurso académico: Costo obligatorio = $0.** Aplica a cómputo, dos instancias, Load Balancer, TLS, red/egress, almacenamiento/CDN, Redis, conectividad MongoDB, observabilidad, CI/CD, registros, servicios externos (correo, IA, archivos) y retención de artefactos. Un free tier, un crédito o una alerta de presupuesto no prueban costo cero ni constituyen un límite duro de gasto.

Para un servicio auxiliar (§6.1) y su proveedor de IA se exige además: plan sin método de pago asociado o límite de gasto en cero que el proveedor haga cumplir, cuotas por minuto y por día documentadas, y que exceder la cuota produzca un error (que el backend degrada) y nunca un cobro. Si no se puede demostrar, el resultado es **BLOCKED** y el backend usa solo el resultado determinista.

Antes de provisionar o contratar se exige evidencia vigente de elegibilidad, región, cuotas, duración, tarjeta, riesgo de cobro, destrucción y costo esperado total. Ante incertidumbre o imposibilidad de satisfacer simultáneamente $0 y dos instancias/balanceador, el resultado es **BLOCKED**: se presenta la incompatibilidad a revisión humana, sin rebajar el objetivo, contratar ni desplegar.

## 18. Seguridad

Aplicar mínimo privilegio, deny-by-default, validación de entrada, autorización por recurso (BOLA) y por función (BFLA), protección de datos y modelos de amenazas. Limitar cuerpos, tiempo de ejecución y exposición de diagnósticos. CORS y headers requieren políticas explícitas. No devolver trazas internas ni registrar contraseñas, tokens, cadenas de conexión ni datos personales innecesarios.

## 19. JWT

El access token es un JWT firmado (NV-05) con validación de firma, algoritmo permitido, emisor, audiencia y expiración, y claims mínimos. Duraciones, algoritmos y rotación de llaves se deciden en la spec con análisis y pruebas negativas, sin copiar ejemplos. La llave de firma vive solo en el almacén de secretos (§31).

## 20. Refresh Tokens

Los tokens de refresco deben ser revocables, almacenados solo como hash, con rotación, detección de reutilización, expiración y protección contra concurrencia. El transporte y sus riesgos de CSRF/XSS se deciden por contrato (spec NV-10).

## 21. Logout

La revocación del access token se comparte entre instancias en Redis (NV-06); cuando existan refresh tokens (NV-10), el logout revoca también su familia. Eliminar un token del cliente no prueba revocación en servidor. El estado de seguridad no se degrada a memoria local.

## 22. Rate Limiting

Las políticas distinguen endpoints, identidad/IP y límites globales, coordinadas en Redis (M-BE-033). Respuestas 429 con `Retry-After`. Un limitador local no se presenta como cuota global bajo balanceo.

## 23. Caché

Cache-aside y caché HTTP se evalúan por caso de uso: claves versionadas, TTL explícito, invalidación tras cambios confirmados, prevención de stampede y ausencia de filtración entre usuarios. La caché HTTP pública solo aplica a lecturas públicas (M-BE-046); las respuestas autenticadas son `private` o `no-store`. No cachear secretos ni respuestas privadas sin aislamiento y autorización.

## 24. Observabilidad

OpenTelemetry es la tecnología prevista para trazas y métricas, con logs estructurados y correlation/trace IDs; los logs persistentes usan Log Analytics con tope diario (M-BE-047). Evitar PII y cardinalidad ilimitada. Retención y exportadores deben cumplir COST-001. Health checks no sustituyen telemetría ni exponen credenciales.

## 25. Testing

`TheHouse.UnitTests` verifica reglas de Domain/Application; `TheHouse.IntegrationTests`, composición, HTTP y adaptadores en el entorno `Testing` (M-BE-042); `TheHouse.SecurityTests`, abuso, autorización y controles defensivos.

Herramientas en uso: Stryker.NET (mutación, NV-02), OWASP ZAP (DAST, NV-09). Previstas: Contract Testing con PactNet, Performance con NBomber (sujeto a su licencia) y REST Fuzzing con RESTler. Ninguna suite vacía o no ejecutada representa un control satisfecho.

## 26. DevSecOps

La CI (NV-02) ejecuta SAST (Semgrep), SCA (NuGet), SBOM (CycloneDX), secretos (Gitleaks), imágenes (Trivy, Dockle) y validación de Terraform; la CD (NV-09) firma imágenes con Cosign y ejecuta DAST. Findings y excepciones tienen severidad, responsable, vencimiento y evidencia. Un fallo del scanner o un control obligatorio no ejecutado no equivale a PASS.

Referencias metodológicas consultadas solo en lectura: [WebAPIDevSecOps](https://github.com/edelomeza/WebAPIDevSecOps) y [WebAPIDevSecOpsScalling](https://github.com/edelomeza/WebAPIDevSecOpsScalling). No se clona ni copia su código.

## 27. CI/CD

GitHub Actions es la plataforma. Todo cambio llega a `main` por pull request con los checks obligatorios de la CI y una aprobación humana (ruleset de `main`). La CD publica la imagen por digest, la firma, la verifica en un entorno efímero y solo despliega con aprobación en el Environment `production`, con rollback automático. Acciones fijadas por SHA, permisos mínimos y sin secretos de larga duración (OIDC).

## 28. Docker

Imágenes mínimas, usuario no root, versiones y digests controlados, parches del sistema operativo aplicados, ausencia de secretos, límites y escaneo. Se verifica el costo de licencias y del registro.

## 29. Terraform

La infraestructura se describe en `infra/terraform/` y separa plan, aplicación y destrucción. `terraform apply` requiere la confirmación humana «OK PARA APPLY» sobre un plan revisado; `destroy`, una autorización aparte con inventario. El estado y los valores de variables no se versionan ni se exponen. Secretos y variables de ejecución de la aplicación se administran fuera de Terraform (M-BE-038).

## 30. Proveedores cloud y servicios externos

AWS, Azure y Google Cloud son los únicos proveedores cloud permitidos. **Azure es el proveedor seleccionado** para cómputo e infraestructura (M-BE-028): Container Apps, Log Analytics e identidad administrada. Un servicio gestionado de terceros (por ejemplo MongoDB Atlas o Upstash) solo se usa con una decisión M-BE aprobada que justifique la excepción y demuestre COST-001 (precedentes: M-BE-034 y M-BE-035).

Los proyectos separados de §6.1 son del equipo, pero su hosting y sus proveedores siguen esta misma regla: el hosting de un servicio auxiliar se elige entre los proveedores cloud permitidos, y un proveedor de IA (API de un LLM) es un servicio de terceros que requiere su propia decisión M-BE con la evidencia de §17 antes de usarse en producción.

## 31. Gestión de secretos

Secretos de la aplicación como secretos de Container Apps (M-BE-038); GitHub Actions usa OIDC y `GITHUB_TOKEN` sin secretos de larga duración. En documentación solo placeholders. Nunca escribir credenciales en specs, repositorio, logs, imágenes o estado Terraform expuesto. Una filtración exige revocación/rotación y revisión; borrarla del último archivo no basta.

## 32. Versionamiento

Documentos y entregas usan versiones identificables; los contratos API tienen estrategia de versión y deprecación (NV-01). Cambios incompatibles requieren nueva aprobación. Contrato, imagen (digest) e infraestructura se versionan de forma trazable; `VERSIONING.md` registra cada despliegue.

## 33. Flujo de Git

`main` es la rama estable y protegida: sin push directo, sin force push ni borrado. El trabajo se hace en ramas cortas (`feat/`, `fix/`, `docs/` + spec o tema) que llegan a `main` por pull request con trazabilidad a la spec, los checks obligatorios y la aprobación de otra persona. Los agentes solo hacen commit o push cuando un humano lo pide, nunca reescriben historial publicado y no mezclan pull requests por iniciativa propia.

## 34. Reglas para agentes

Orden normativo: constitution → AGENTS → memoria → spec activa → plan activo → task activo. Las skills y perfiles no alteran ese orden. Antes de implementar, todo agente identifica SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVO y ESTADO AUTORIZADO. Sin autorización verificable: **BLOCKED**. PASS del guardian es revisión de cumplimiento, no permiso humano. No inventar evidencia, aprobación, métricas o costos.

Autorizaciones humanas reconocidas, cada una independiente:

| Frase | Autoriza |
| --- | --- |
| «EJECUTA» + spec, versión, tasks y rutas | Implementar solo esas tasks en esas rutas |
| «OK PARA APPLY» | Aplicar un plan de Terraform ya revisado |
| Aprobación en el Environment `production` | Un despliegue concreto de la CD |
| Autorización explícita de seed en Production | Una carga de datos con `--allow-production-seed` (M-BE-043) |

Cambios en cuentas o configuración externa (suscripción, GitHub, proveedores) requieren permiso explícito para esa acción concreta.

## 35. Prohibiciones y cambios de gobierno

Sin la autorización correspondiente se prohíbe: modificar rutas protegidas, crear endpoints, entidades o repositorios fuera de una spec aprobada, instalar paquetes, cambiar infraestructura, workflows o configuración de GitHub, desplegar, ejecutar seeds en producción y realizar operaciones Git de escritura sobre `main`. No crear carpetas `src` o `tests`; los proyectos nuevos solo con autorización que los nombre.

No eliminar `UnitTest/` residual sin autorización humana. No clonar referencias ni copiar código. Cualquier cambio de esta constitución o excepción necesita revisión humana explícita, registro de decisión y análisis de impacto; no se relaja COST-001 de manera unilateral. Ante conflicto de alcance o ausencia de evidencia, detener la implementación.
