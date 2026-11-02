# NV-01 — Foundation

| Campo | Valor |
| --- | --- |
| ID | NV-01 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | Bootstrap técnico existente (M-BE-001) |
| Relación | Habilita foundations para NV-02 y NV-03, sin autorización transitiva |

Este documento es un borrador de planificación. No autoriza implementación, instalación de paquetes, modificación de proyectos ni ejecución de pruebas. La frase **⛔ NO EJECUTAR** prevalece hasta que exista aprobación humana de `spec.md`, `plan.md` y `task.md` con alcance, fechas, versiones y rutas explícitas.

## 1. Contexto

La solución `TheHouse.slnx` y los siete proyectos ya existen. El bootstrap está terminado y la compilación de referencia fue correcta con 0 errores y 0 advertencias. No se repite build, restore ni test en esta entrega documental.

Los proyectos no contienen todavía los fundamentos transversales de una API REST verificable. NV-01 define esos fundamentos antes de abrir funcionalidad de negocio: composición, configuración, errores, validación, contrato, operación y límites modulares.

La existencia de los proyectos es una **precondición**, no una task. NV-01 no recrea, renombra, mueve ni referencia proyectos.

## 2. Objetivo

Establecer el contrato técnico de la capa Foundation para que una futura implementación autorizada sea coherente, observable, segura y verificable, sin introducir endpoints de negocio ni alterar el bootstrap actual.

## 3. Precondiciones

1. `TheHouse.slnx` y los siete proyectos existen y conservan sus referencias actuales.
2. `constitution.md`, `AGENTS.md` y `memoria.md` están vigentes.
3. No existe spec de implementación activa ni task autorizada.
4. COST-001 sigue vigente: costo obligatorio $0.
5. Una futura ejecución deberá autorizar explícitamente las rutas protegidas que necesite modificar.
6. Este documento, su plan y su task list deben aprobarse juntos antes de cualquier cambio.

## 4. Alcance

NV-01 cubre exclusivamente:

1. Inyección de dependencias y composition root.
2. Configuración tipada y validada.
3. Problem Details.
4. Base de validación de entrada.
5. Versionado de API.
6. OpenAPI.
7. Health checks.
8. Correlation IDs.
9. Compresión de respuestas.
10. Rate limiting.
11. Paginación.
12. Manejo global de excepciones.
13. Límites del monolito modular.

## 5. Fuera de alcance

- Implementar endpoints de negocio, entidades, repositorios o controladores funcionales.
- Implementar MongoDB, Redis, JWT, Refresh Tokens, Logout o autenticación.
- Crear Dockerfile, Terraform, workflows o infraestructura cloud.
- Modificar proyectos, `.csproj`, `Program.cs`, `appsettings*.json`, `Controllers/`, `Properties/` o referencias sin autorización específica.
- Ejecutar build, restore, tests, scanners o despliegues en esta entrega.
- Crear las specs NV-04 a NV-17 o sus carpetas.
- Seleccionar proveedor cloud.
- Declarar métricas, cobertura o resultados no ejecutados.

## 6. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-01-R01 | Composición de dependencias en el composition root | Existe un mapa de registros por capa/módulo, sin service locator; los lifetimes están justificados; la validación de dependencias falla al inicio si falta un servicio; no se filtran tipos de Infrastructure hacia controllers. | F1 | T01, T18 |
| NV-01-R02 | Configuración tipada y validada | Las opciones se enlazan a tipos, se validan al inicio, fallan rápido ante valores inválidos y no contienen secretos versionados; los entornos están separados y documentados con placeholders. | F1 | T02, T17, T18 |
| NV-01-R03 | Problem Details uniforme | Todos los errores esperados usan `application/problem+json` con campos estables, código de estado coherente, extensión de correlación y ausencia de stack traces, rutas internas o secretos. | F2 | T03, T14, T17, T18 |
| NV-01-R04 | Base de validación de entrada | Las reglas de entrada se ejecutan en el límite HTTP, producen errores 400 consistentes con Problem Details, respetan límites de tamaño y no dependen de lógica de negocio dentro del controller. | F2 | T04, T16, T18 |
| NV-01-R05 | Versionado de API explícito | Existe una estrategia documentada y justificada, una versión inicial identificable y una política de cambios incompatibles y deprecación; el contrato y las rutas reflejan la misma versión. | F3 | T05, T14, T18 |
| NV-01-R06 | OpenAPI coherente y verificable | El documento OpenAPI describe operaciones, esquemas, errores, seguridad y ejemplos sin datos sensibles; se genera o mantiene de forma reproducible y se comprueba contra el contrato esperado. | F3 | T06, T14, T18 |
| NV-01-R07 | Health checks de liveness y readiness | Liveness no depende de servicios externos; readiness refleja dependencias necesarias; ambos tienen timeouts, no exponen credenciales ni detalles internos y devuelven códigos coherentes. | F4 | T07, T15, T18 |
| NV-01-R08 | Correlation IDs controlados | Se acepta o genera un identificador con formato y longitud limitados, se propaga a logs/trazas, se devuelve en la respuesta y no se confía en valores arbitrarios sin validación. | F4 | T08, T15, T17, T18 |
| NV-01-R09 | Compresión de respuestas segura | La negociación respeta `Accept-Encoding`, usa algoritmos y tipos permitidos, aplica umbral de tamaño, no comprime respuestas sensibles y no rompe HTTPS ni clientes que no la soportan. | F4 | T09, T15, T18 |
| NV-01-R10 | Rate limiting explícito | Las políticas distinguen endpoint, identidad e IP, devuelven 429 con `Retry-After`, excluyen health checks cuando corresponda y documentan que un límite local no es una cuota global entre instancias. | F4 | T10, T15, T17, T18 |
| NV-01-R11 | Paginación acotada y estable | Toda colección futura declara estrategia, orden estable, tamaño máximo, metadatos y límites; no existen respuestas ilimitadas ni páginas sin criterio de continuación. | F5 | T11, T16, T18 |
| NV-01-R12 | Manejo global de excepciones | Un middleware o mecanismo único clasifica errores conocidos y desconocidos, respeta cancelación y timeouts, registra con correlación y no filtra detalles internos al cliente. | F2 | T12, T17, T18 |
| NV-01-R13 | Límites del monolito modular | Los módulos declaran contratos públicos, no acceden a internals de otro módulo y no crean ciclos; las reglas de dependencia son comprobables mediante pruebas de arquitectura y documentación. | F5 | T13, T18 |

## 7. Decisiones y restricciones aplicables

- M-BE-001: no recrear ni modificar el bootstrap.
- M-BE-003: Dependency Rule y referencias actuales.
- M-BE-004: monolito modular, no microservicios.
- M-BE-005: SDD y autorización humana.
- M-BE-012: NV-01 propuesta, pendiente de revisión.
- M-BE-018: OpenTelemetry y correlación como dirección.
- COST-001: ningún componente de NV-01 puede requerir un servicio pago.

Las decisiones concretas que todavía no están aprobadas se registran como propuestas en el plan y no se implementan hasta revisión:

| Tema | Propuesta inicial | Estado |
| --- | --- | --- |
| Versión inicial | Prefijo `/api/v1` | PROPUESTO |
| Problem Details | RFC 9457 con extensión `traceId` | PROPUESTO |
| Correlation ID | `X-Correlation-ID`, longitud acotada | PROPUESTO |
| Paginación | Página/tamaño con máximo configurable, evaluar cursor en NV-03 | PROPUESTO |
| Rate limiting | Políticas locales en NV-01; coordinación global en NV-03 | PROPUESTO |
| Compresión | Brotli/Gzip con exclusión de respuestas sensibles | PROPUESTO |

## 8. Seguridad

- Validar y limitar toda entrada antes de procesarla.
- No devolver excepciones, rutas, nombres de servidores, cadenas de conexión ni tokens.
- No registrar credenciales, tokens, cookies ni datos personales.
- Tratar `X-Correlation-ID` como entrada no confiable: validar formato y longitud; no usarla para autorización.
- Excluir health checks y endpoints sensibles de políticas de rate limiting incompatibles.
- No comprimir respuestas que contengan secretos o tokens; evaluar el riesgo de side channels.
- No exponer OpenAPI con información interna o ejemplos reales.
- Aplicar deny-by-default a las rutas que se creen en el futuro.
- La seguridad de JWT, Refresh Tokens y Logout pertenece a specs funcionales independientes.

## 9. Costo — COST-001

NV-01 se diseña para ejecutarse localmente y con herramientas gratuitas. No requiere cloud, Redis, MongoDB ni servicios pagos para su verificación. Si una futura implementación propone un componente con costo incierto, la task se detiene y se eleva a revisión humana.

Costo obligatorio objetivo: **$0**. La aprobación de NV-01 no autoriza gasto ni aprovisionamiento.

## 10. Testing y evidencia

Las pruebas de NV-01 se planifican, no se ejecutan en esta entrega:

- Unitarias para opciones, validadores y reglas de módulo.
- Integración con `WebApplicationFactory` para errores, correlación, compresión, rate limiting y health checks.
- Pruebas de contrato para Problem Details, versionado y OpenAPI.
- Pruebas de arquitectura para dependencias y límites de módulo.
- Pruebas negativas para entradas malformadas, cabeceras no confiables y respuestas sensibles.

La evidencia futura debe incluir comando reproducible, versión, resultado y artefacto. Ninguna suite vacía o placeholder se reporta como cobertura funcional.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación propuesta |
| --- | --- | --- | --- |
| NV-01-RK01 | Elegir versionado o paginación sin evaluar consumidores | Retrabajo y cambios incompatibles | Registrar decisión y revisar antes de implementar |
| NV-01-RK02 | Rate limiting local presentado como global | Límites inconsistentes entre instancias | Documentar alcance local y resolver cuota global en NV-03 |
| NV-01-RK03 | Compresión de respuestas sensibles | Fuga por side channel | Excluir respuestas con tokens o secretos |
| NV-01-RK04 | OpenAPI desactualizado | Contrato falso y pruebas débiles | Verificación de contrato y revisión en CI futuro |
| NV-01-RK05 | Paginación sin máximo | Consumo de memoria y abuso | Máximo obligatorio y pruebas de límites |
| NV-01-RK06 | Sobreingeniería de módulos | Complejidad sin funcionalidad | Mantener límites mínimos y verificables |
| NV-01-RK07 | Modificar rutas protegidas sin autorización | Violación de alcance | Autorización específica por ruta antes de ejecutar |

## 12. Dependencias

- Bootstrap técnico existente (precondición, no task).
- Constitución, AGENTS y memoria vigentes.
- Aprobación humana de los tres documentos de NV-01.
- NV-02 podrá verificar los fundamentos, pero su aprobación es independiente.
- NV-03 podrá usar la base stateless, pero su aprobación es independiente.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Fases del plan | 6 |
| Tasks | 18 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado de ejecución | **⛔ NO EJECUTAR** |

Cada fila de la tabla del apartado 6 enlaza un requisito con una fase principal y una o más tasks. El detalle de aceptación y evidencia por task está en `task.md`; la secuencia y las puertas están en `plan.md`.

## 14. Aprobación pendiente

Para retirar el bloqueo se requiere una revisión humana que registre, como mínimo:

1. Aprobador y fecha.
2. Versión aprobada de `spec.md`, `plan.md` y `task.md`.
3. IDs de requisitos y tasks autorizados.
4. Rutas permitidas y restricciones.
5. Confirmación de costo $0 y de que no se autoriza NV-02 ni NV-03 por transitividad.

Sin ese registro, el estado permanece **BLOCKED** y **⛔ NO EJECUTAR**.
