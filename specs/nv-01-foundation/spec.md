# NV-01 — Foundation (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-01 |
| Documento | `spec.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** (T13 excluida) |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RNF-01, RNF-05, RNF-06, RNF-08, RNF-10 (transversales) (`Requerimientos_TheHouse_v2`) |
| Plan | `plan.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Precondición | Bootstrap técnico existente (M-BE-001); M-BE-027; M-BE-028 |
| Relación | Habilita NV-04 a NV-08 y NV-03 U2, sin autorización transitiva |
| Reemplaza | `1.0.0-borrador`; el historial permanece en Git |

**Cambio 1.2.0-u2 (2026-11-07) — APROBADO Y AUTORIZADO por Victor Manuel Jose Jose el 2026-11-07 («okey si esta bien una vez que quedo esto me dices», respuesta al paquete propuesto: aprobación documental, «EJECUTA» de NV-08 T14, NV-07 T13, NV-01 T19, NV-09 T13, T18 y T20, NV-03 T08, T18 y T19, y autorización del seed en producción; `terraform apply` sigue requiriendo «OK PARA APPLY»):** agrega NV-01-R14 (encabezados de seguridad HTTP; hallazgo H-01 de NV-09: falta `X-Content-Type-Options: nosniff`) y la task T19. Las tasks 1.1.0-u2 no cambian.

Este documento está aprobado documentalmente para la Entrega Unidad 2. La aprobación documental **no autoriza implementación**: conforme a M-BE-027, la ejecución requiere una autorización humana posterior de Fase B con SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVA y ESTADO AUTORIZADO. NV-01 sigue siendo Foundation y no contiene endpoints de negocio.

## 1. Contexto

La Entrega Unidad 2 requiere siete endpoints funcionales: Registration, Login, Logout, dos lecturas de propiedades y dos lecturas de universidades. Antes de implementarlos se necesita una base transversal coherente: composición, configuración, errores, validación, versión, OpenAPI, salud, correlación, compresión, rate limiting, paginación y manejo de excepciones.

NV-01 prepara esa base y los **límites técnicos** para MongoDB, Redis y JWT. No implementa Registration, Login o Logout, no crea entidades funcionales y no abre conexiones reales. Esas responsabilidades pertenecen a NV-04, NV-05, NV-06, NV-07, NV-08 y NV-03.

## 2. Objetivo

Establecer el contrato técnico de Foundation para que la implementación U2 sea coherente, observable, segura y verificable, manteniendo la Dependency Rule y sin alterar el bootstrap existente.

## 3. Precondiciones

1. `TheHouse.slnx` y los siete proyectos existen y conservan sus referencias actuales.
2. `constitution.md`, `AGENTS.md` y `memoria.md` están vigentes.
3. NV-04, NV-05, NV-06, NV-07 y NV-08 están aprobadas documentalmente para U2.
4. NV-03 está aprobada documentalmente para U2 con Azure Container Apps y COST-001.
5. Existe autorización humana de Fase B para las rutas y tasks U2 que se ejecutarán.
6. COST-001 sigue vigente: costo obligatorio objetivo $0.
7. La ejecución de NV-01 no autoriza NV-02, NV-03 ni las specs funcionales por transitividad.

## 4. Alcance

NV-01 cubre exclusivamente:

1. Inyección de dependencias y composition root.
2. Configuración tipada y validada.
3. Problem Details.
4. Base de validación de entrada.
5. Versionado de API.
6. OpenAPI.
7. Health checks de liveness y readiness.
8. Correlation IDs.
9. Compresión de respuestas.
10. Rate limiting foundation.
11. Convenciones de paginación.
12. Manejo global de excepciones.
13. Límites del monolito modular.
14. Límites técnicos de configuración para MongoDB, Redis y JWT: opciones, validación, registro de dependencias y health boundaries, sin repositorios, conexiones reales, emisión de tokens ni revocación.

NV-01 puede preparar infraestructura técnica. NV-01 **no implementa** Register, Login ni Logout.

## 5. Fuera de alcance

- Implementar `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` o cualquier endpoint de negocio.
- Crear entidades, repositorios, controladores, casos de uso o colecciones funcionales de usuarios, propiedades o universidades.
- Implementar hashing de contraseñas, emisión/validación de JWT, refresh tokens o revocación; pertenecen a NV-04, NV-05 y NV-06.
- Abrir conexiones reales a MongoDB o Redis o instalar sus drivers; la conectividad pertenece a NV-03 y a las specs funcionales.
- Crear Dockerfile, `.dockerignore`, Terraform, workflows, imágenes o infraestructura cloud; pertenece a NV-03.
- Ejecutar SAST, SCA, SBOM, DAST, fuzzing, mutación, contrato o performance de NV-02.
- Modificar proyectos, `.csproj`, `Program.cs`, `appsettings*.json`, `Controllers/`, `Properties/` o referencias sin autorización específica de Fase B.
- Marcar tasks como completadas o declarar métricas, cobertura o resultados no ejecutados.

## 6. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-01-R01 | Composición de dependencias en el composition root | Existe un mapa de registros por capa/módulo, sin service locator; los lifetimes están justificados; la validación de dependencias falla al inicio si falta un servicio; no se filtran tipos de Infrastructure hacia controllers; los boundaries de MongoDB, Redis y JWT se registran sin crear repositorios ni conexiones. | F1 | T01, T18 |
| NV-01-R02 | Configuración tipada y validada | Las opciones se enlazan a tipos, se validan al inicio, fallan rápido ante valores inválidos y no contienen secretos versionados; los entornos están separados y documentados con placeholders; existen grupos de opciones para MongoDB, Redis y JWT sin abrir conexiones ni emitir tokens. | F1 | T02, T17, T18 |
| NV-01-R03 | Problem Details uniforme | Todos los errores esperados usan `application/problem+json` con campos estables, código de estado coherente, extensión de correlación y ausencia de stack traces, rutas internas o secretos. | F2 | T03, T14, T17, T18 |
| NV-01-R04 | Base de validación de entrada | Las reglas de entrada se ejecutan en el límite HTTP, producen errores 400 consistentes con Problem Details, respetan límites de tamaño y no dependen de lógica de negocio dentro del controller. | F2 | T04, T16, T18 |
| NV-01-R05 | Versionado de API explícito | Existe una estrategia documentada y justificada, una versión inicial identificable y una política de cambios incompatibles y deprecación; el contrato y las rutas reflejan la misma versión. | F3 | T05, T14, T18 |
| NV-01-R06 | OpenAPI coherente y verificable | El documento OpenAPI describe operaciones, esquemas, errores, seguridad y ejemplos sin datos sensibles; se genera o mantiene de forma reproducible y se comprueba contra el contrato esperado. | F3 | T06, T14, T18 |
| NV-01-R07 | Health checks de liveness y readiness | Liveness no depende de servicios externos; readiness refleja dependencias necesarias, incluidas las boundaries de MongoDB y Redis cuando las specs funcionales las habiliten; ambos tienen timeouts, no exponen credenciales ni detalles internos y devuelven códigos coherentes. | F4 | T07, T15, T18 |
| NV-01-R08 | Correlation IDs controlados | Se acepta o genera un identificador con formato y longitud limitados, se propaga a logs/trazas, se devuelve en la respuesta y no se confía en valores arbitrarios sin validación. | F4 | T08, T15, T17, T18 |
| NV-01-R09 | Compresión de respuestas segura | La negociación respeta `Accept-Encoding`, usa algoritmos y tipos permitidos, aplica umbral de tamaño, no comprime respuestas sensibles y no rompe HTTPS ni clientes que no la soportan. | F4 | T09, T15, T18 |
| NV-01-R10 | Rate limiting foundation | Las políticas distinguen endpoint, identidad e IP, devuelven 429 con `Retry-After`, excluyen health checks cuando corresponda y documentan que la base local no es una cuota global; la coordinación distribuida se implementa en NV-03 y en la spec funcional que la requiera. | F4 | T10, T15, T17, T18 |
| NV-01-R11 | Paginación acotada y estable | Toda colección futura declara estrategia, orden estable, tamaño máximo, metadatos y límites; no existen respuestas ilimitadas ni páginas sin criterio de continuación. | F5 | T11, T16, T18 |
| NV-01-R12 | Manejo global de excepciones | Un middleware o mecanismo único clasifica errores conocidos y desconocidos, respeta cancelación y timeouts, registra con correlación y no filtra detalles internos al cliente. | F2 | T12, T17, T18 |
| NV-01-R13 | Límites del monolito modular | Los módulos declaran contratos públicos, no acceden a internals de otro módulo y no crean ciclos; las reglas de dependencia son comprobables mediante pruebas de arquitectura y documentación. | F5 | T13 |
| NV-01-R14 | Encabezados de seguridad HTTP | Toda respuesta, incluidos errores, health y 304, incluye `X-Content-Type-Options: nosniff`; se agrega en un único middleware al inicio del pipeline sin alterar contratos ni encabezados existentes; prueba de integración que lo verifica en respuestas 200, 4xx y 304. | F6 | T19 |

## 7. Decisiones y restricciones aplicables

- M-BE-001: no recrear ni modificar el bootstrap.
- M-BE-003: Dependency Rule y referencias actuales.
- M-BE-004: monolito modular, no microservicios.
- M-BE-005 y M-BE-027: SDD y autorización humana; documentación aprobada no equivale a ejecución.
- M-BE-012: NV-01 Foundation, materializada en esta revisión U2.
- M-BE-018: OpenTelemetry y correlación como dirección.
- M-BE-028: Azure Container Apps como objetivo U2, condicionado a COST-001.
- COST-001: ningún componente de NV-01 puede requerir un servicio pago.

Decisiones técnicas aprobadas documentalmente para U2:

| Tema | Decisión U2 | Estado |
| --- | --- | --- |
| Versión inicial | Prefijo `/api/v1` | APROBADO U2 |
| Problem Details | RFC 9457 con extensión `traceId` | APROBADO U2 |
| Correlation ID | `X-Correlation-ID`, longitud acotada y validada | APROBADO U2 |
| Paginación | Página/tamaño, máximo 50; orden estable por recurso | APROBADO U2 |
| Rate limiting | Base local en NV-01; coordinación Redis en NV-03/NV-05 | APROBADO U2 |
| Compresión | Brotli/Gzip con exclusión de respuestas sensibles | APROBADO U2 |
| MongoDB/Redis/JWT | Solo opciones, DI y health boundaries en NV-01 | APROBADO U2 |

## 8. Seguridad

- Validar y limitar toda entrada antes de procesarla.
- No devolver excepciones, rutas, nombres de servidores, cadenas de conexión ni tokens.
- No registrar credenciales, tokens, cookies, secretos ni datos personales innecesarios.
- Tratar `X-Correlation-ID` como entrada no confiable: validar formato y longitud; no usarla para autorización.
- Excluir health checks y endpoints sensibles de políticas de rate limiting incompatibles.
- No comprimir respuestas que contengan secretos o tokens.
- No exponer OpenAPI con información interna o ejemplos reales.
- Aplicar deny-by-default a las rutas que se creen en la fase funcional.
- Mantener `JWT_SIGNING_KEY`, `MONGODB_URI` y `REDIS_CONNECTION` fuera del repositorio; NV-01 solo declara sus opciones y validaciones.
- La seguridad funcional de JWT, refresh tokens, Logout, MongoDB y Redis pertenece a NV-04 a NV-06, NV-03 y las specs correspondientes.

## 9. Costo — COST-001

NV-01 se ejecuta localmente y con herramientas gratuitas. No requiere cloud, Redis, MongoDB ni servicios pagos para su verificación. Si una implementación propone un componente con costo incierto, la task se detiene y se eleva a revisión humana.

Costo obligatorio objetivo: **$0**. La aprobación de NV-01 no autoriza gasto ni aprovisionamiento.

## 10. Testing y evidencia

Las pruebas se planifican; no se ejecutan en esta fase documental:

- Unitarias para opciones, validadores y reglas de módulo.
- Integración con `WebApplicationFactory` para errores, correlación, compresión, rate limiting y health checks.
- Pruebas de contrato para Problem Details, versionado y OpenAPI.
- Pruebas de arquitectura para dependencias y límites de módulo.
- Pruebas negativas para entradas malformadas, cabeceras no confiables y respuestas sensibles.

La evidencia futura debe incluir comando reproducible, versión, resultado y artefacto. Ninguna suite vacía o placeholder se reporta como cobertura funcional.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-01-RK01 | Convertir Foundation en implementación funcional | Scope creep y duplicación | Rechazar endpoints, entidades y repositorios en NV-01 |
| NV-01-RK02 | Rate limiting local presentado como global | Límites inconsistentes entre réplicas | Coordinación Redis en NV-03/NV-05 |
| NV-01-RK03 | Compresión de respuestas sensibles | Fuga por side channel | Excluir respuestas con tokens o secretos |
| NV-01-RK04 | OpenAPI desactualizado | Contrato falso y pruebas débiles | Verificación de contrato en F6 |
| NV-01-RK05 | Paginación sin máximo | Consumo de memoria y abuso | Máximo 50 y pruebas de límites |
| NV-01-RK06 | Configurar JWT/Mongo/Redis sin conexión real | Falsa sensación de implementación | Limitar a opciones, DI y health boundaries |
| NV-01-RK07 | Modificar rutas protegidas sin autorización | Violación de alcance | Autorización específica de Fase B |

## 12. Dependencias

- Bootstrap técnico existente (precondición, no task).
- Constitución, AGENTS y memoria vigentes.
- NV-04 a NV-08 aprobadas documentalmente para U2.
- NV-03 aprobada documentalmente para U2.
- Autorización humana de Fase B para ejecutar las tasks U2.
- NV-02 (CI) quedó completada el 2026-11-05 y la CD se especifica en NV-09 (M-BE-049).

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 14 |
| Fases del plan | 6 |
| Tasks | 19 |
| Tasks U2 | 18 |
| Tasks futuras (no U2) | 1 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 17 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** (T13 excluida) |

Cada fila de la tabla del apartado 6 enlaza un requisito con una fase principal y una o más tasks. El detalle de aceptación y evidencia por task está en `task.md`; la secuencia y las puertas están en `plan.md`. R13/T13 se conserva como trabajo futuro y **no forma parte de U2**; por eso no bloquea el cierre de las tasks U2.

## 14. Aprobación documental y ejecución

La aprobación documental U2 quedó registrada con:

1. Aprobador: Victor Manuel Jose Jose.
2. Fecha: 2026-11-02.
3. Versión aprobada: `1.1.0-u2` de `spec.md`, `plan.md` y `task.md`.
4. Alcance: las tasks marcadas **Alcance: U2**.
5. Restricciones: sin endpoints funcionales, sin cloud, sin NV-02 y sin ejecución automática.

La ejecución sigue bloqueada. Para iniciar Fase B se requiere una autorización humana separada que declare SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVA y ESTADO AUTORIZADO = APROBADO. Sin ese registro, el estado permanece **BLOCKED**.
