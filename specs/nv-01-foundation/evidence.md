# NV-01 Foundation — Evidencia de Fase B U2

| Campo | Valor |
| --- | --- |
| Spec / plan / task | NV-01, `1.1.0-u2`, en esta carpeta |
| Fecha de esta revisión | 2026-11-03 |
| Rama / baseline | `feature/u2-entrega` / `53bd45681aa0295da943b639151c93c2191bdea9` |
| Autorización de ejecución | Instrucción humana explícita del usuario en esta sesión: **APROBADO — FASE B U2** |
| Tasks autorizadas | T01–T12, T14–T18 |
| Exclusiones | T13, NV-02, NV-03, NV-04 a NV-08, Terraform y cloud |
| Resultado del cierre | **BLOCKED — NV-01 no cerrada; sin commit ni push** |

## Autorización y preservación

Se leyeron, en orden, `constitution.md`, `AGENTS.md`, `memoria.md` (incluida M-BE-027), `spec.md`, `plan.md` y `task.md`. Los tres documentos NV-01 están aprobados documentalmente por Victor Manuel Jose Jose el 2026-11-02. La autorización posterior de ejecución es la instrucción del usuario de esta sesión; no se presume otra identidad para ese mensaje.

Se declaró antes de implementar:

```text
SPEC ACTIVA: NV-01 Foundation 1.1.0-u2
PLAN ACTIVO: specs/nv-01-foundation/plan.md
TASK ACTIVA: T01-T12, T14-T18
ESTADO AUTORIZADO: APROBADO — FASE B U2
```

La autorización permite únicamente Foundation, sus pruebas y evidencia. Las prohibiciones históricas de preparación documental se levantan solo en ese alcance por la instrucción humana; no se cambia el diseño aprobado ni se aprueba otra spec. Las leyendas documentales «hasta autorización de Fase B» deben leerse con este registro posterior. La autorización de ejecución no equivale a un cierre aprobado.

Se ejecutaron `git status --short`, `git stash push -u -m "wip: interrupted u2 phase b before nv01 cleanup"`, `git stash list` y `git status`. El stash terminó correctamente y dejó el árbol limpio. El estado observado antes del stash tenía 3 archivos rastreados modificados y archivos nuevos, sin eliminaciones rastreadas; no coincidía con las cantidades del resumen humano. No se asumió que los otros stashes fueran descartables.

Se conservaron los tres snapshots existentes después de la preservación:

| Referencia al momento de revisión | SHA |
| --- | --- |
| `stash@{0}` — creado en esta sesión | `0dc404f962ade97ee42d16785f3adc039049b2c0` |
| `stash@{1}` — preexistente | `b25e6c20d3f9519d1fa3e1ce287f2096d493bf1b` |
| `stash@{2}` — preexistente | `527f927b0ab48b9a78462973634bb1322f587207` |

Ningún stash se reaplicó, eliminó ni sobrescribió. No se usó reset, restore, clean o checkout forzado. El código nuevo se preparó sobre el baseline limpio, no reaplicando el trabajo mezclado.

## Entorno y ejecución real

- Windows x64; SDK fijado por `global.json` a .NET `10.0.114`, `latestPatch`, sin prereleases. Antes de fijarlo se seleccionaba SDK 11 RC.
- Target existente `net10.0`; runtime de pruebas .NET `10.0.14`; xUnit `2.9.3`, adaptador `3.1.4`.
- Único paquete agregado: `Microsoft.AspNetCore.Mvc.Testing` `10.0.14` en el proyecto de integración existente. No se cambiaron referencias entre proyectos ni la solución.
- Costo de verificación local: sin aprovisionamiento ni servicios externos. Sin drivers MongoDB/Redis, conexiones, JWT funcional ni herramientas de NV-02.

Ejecuciones incrementales reales; **no sustituyen la validación del árbol final**:

| Comando (filtros `FullyQualifiedName~...`) | Resultado de esa ejecución |
| --- | --- |
| `dotnet test TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj --filter FullyQualifiedName~ConfigurationTests` | 24 PASS |
| `dotnet test TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj --filter FullyQualifiedName~ErrorContractTests` | 15 PASS, después de corregir un using faltante en el fixture |
| `dotnet test TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj --filter FullyQualifiedName~OpenApiContractTests` | 9 PASS antes de incorporar las rutas de health |
| `dotnet test TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj --filter "FullyQualifiedName~CompressionTests\|FullyQualifiedName~CorrelationTests\|FullyQualifiedName~HealthAndRateLimitTests"` | 38 PASS |
| `dotnet test TheHouse.SecurityTests/TheHouse.SecurityTests.csproj --filter FullyQualifiedName~ForwardedHeadersTests` | Compiló tras corregir acceso a headers; ejecución bloqueada por Windows Application Control |
| `dotnet test TheHouse.UnitTests/TheHouse.UnitTests.csproj --filter FullyQualifiedName~PaginationTests` | 15 PASS |
| `dotnet test TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj --filter FullyQualifiedName~PaginationHttpTests` | 11 FAIL por carga de assembly bloqueada, no por aserciones del contrato |

Validación final del código preparado:

| Comando | Resultado |
| --- | --- |
| `dotnet build` | **PASS**, 0 errores y 0 advertencias |
| `dotnet test` | **FAIL**, código de salida 1; desglose debajo |
| `git status --short` | Solo cambios de Foundation y esta evidencia; sin stage |
| `git diff --stat` | Revisado; describe archivos rastreados, no incluye los nuevos sin stage |
| `git diff --check` | PASS; sin errores de whitespace; avisos de conversión LF/CRLF de Git |

| Suite final | PASS | FAIL | Omitidas |
| --- | ---: | ---: | ---: |
| TheHouse.UnitTests | 16 | 0 | 0 |
| TheHouse.IntegrationTests | 2 | 95 | 0 |
| TheHouse.SecurityTests | 0 | 6 | 0 |
| Total | 18 | 101 | 0 |

Una de las 16 pruebas unitarias es el placeholder preexistente `TheHouse.UnitTests/UnitTest1.cs`; no representa cobertura funcional. Los dos PASS de integración finales son comprobaciones directas de cancelación/correlación que no necesitan arrancar el host. No se declara cobertura, rendimiento ni equivalencia con los 73 PASS de una ejecución anterior ajena a esta sesión.

### Bloqueo de plataforma confirmado

`System.IO.FileLoadException`, HRESULT **`0x800711C7`**: «Una directiva de Control de aplicaciones bloqueó este archivo». La ejecución final no puede cargar `TheHouse.Application.dll` desde los directorios de salida de integración y seguridad. Una ejecución anterior tampoco pudo cargar el assembly de SecurityTests.

La lectura de `Microsoft-Windows-CodeIntegrity/Operational` confirmó eventos **3077 y 3033**, a las 11:05:17 hora local del 2026-11-03, que indican incumplimiento de requisitos de firma Enterprise / política de integridad. El SHA-256 de las copias de Application en su salida, UnitTests e IntegrationTests era idéntico: `E12C0AD06DBA885CA9405C39D396DD020BB856E01F972B5617E5FD15D33F01DE`. No hay evidencia de que modificar lógica resuelva esa política.

No se deshabilitaron, alteraron ni eludieron controles de Windows; tampoco se movieron assemblies para evitar la política. **Requiere decisión humana:** el administrador debe revisar la política/firma por el procedimiento aprobado o proporcionar un entorno de desarrollo autorizado. Después hay que repetir build, todas las pruebas, revisión SDD y revisión de seguridad antes de cerrar o publicar. No se cambia código para alterar hashes con ese fin.

## Diseño Foundation preparado y límites

### Composición y configuración — T01/T02

- `TheHouse/Program.cs` es el composition root. Validación de DI y scopes habilitada en todos los entornos; sin cambios de referencias.
- Puertos de salud en Application; implementaciones fail-closed y opciones de tecnología en Infrastructure; HTTP permanece en el host; Domain no cambia.
- Probes y opciones son singleton/snapshot por proceso: no tienen estado de negocio ni conexiones. Los middleware convencionales se construyen una vez; el estado por solicitud queda en variables locales. Health crea su check mediante la factoría DI del framework. No hay repositorios ni service locator de negocio.
- Grupos `Foundation`, `Proxy`, `MongoDB`, `Redis`, `Jwt`: binding tipado estricto, rangos, reglas condicionales y `ValidateOnStart`. La validación inicial sucede antes de Build. Los errores de binding se reemplazan por diagnósticos sin valores ni inner exception. No hay reload parcial.
- MongoDB/Redis/JWT están deshabilitados por defecto. Habilitar una boundary exige configuración y **no** crea un cliente ni hace saludable una dependencia inexistente. El parsing definitivo y TLS del driver requieren el adaptador futuro, no están demostrados aquí.

| Entorno | Configuración y fuente |
| --- | --- |
| Local Foundation | Defaults acotados en `TheHouse/appsettings.json`; dependencias externas deshabilitadas; hosts loopback |
| Pruebas | Overrides aislados de WebApplicationFactory; URI loopback sintética sin credenciales, sin conexión real |
| Azure futuro | Variables externas/almacén aprobado: `MongoDB__ConnectionString=<externo>`, `Redis__ConnectionString=<externo>`, `Jwt__SigningKey=<externo>`, issuer/audience y `AllowedHosts=<host-validado>`; no provisionado |

Los nombres conceptuales de secretos de la spec no se convierten automáticamente en variables: el binding implementado usa la convención .NET `Seccion__Propiedad`. No se versionan valores reales. Límites actuales: body 64 KiB (configurable 1 KiB–1 MiB), timeout de solicitud 15 s, health 2 s; Mongo connect 5 s/pool 100; Redis timeout 5000 ms; límites de duración JWT solo como opciones, sin emisión o validación de tokens.

### Errores y entrada — T03/T04/T12

- `ProblemResponses` centraliza RFC 9457: `type`, `title`, `status`, `traceId`; sin `detail`, `instance`, stack ni valores del binder. Validación añade `errors.request` con mensaje fijo.
- Catálogo: 400 entrada, 401/403 reservados para fases futuras, 404 ruta, 405 método, 408 tiempo de solicitud, 409 conflicto, 413 tamaño, 415 media type, 429 cuota, 503 disponibilidad y 504 timeout. No se implementan operaciones de autenticación por documentar esos códigos.
- Matriz de excepciones: `BadHttpRequestException` 4xx conserva código; `ValidationException` → 400; `TimeoutException` → 504; cancelación interna no solicitada → 503; desconocida → 500. Cancelación del cliente: 499 interno, sin intentar respuesta. Un response ya iniciado no se reescribe.
- Logging propio con código/correlación, sin pasar el objeto excepción. Anotaciones MVC y JSON estricto, profundidad 32, body acotado por longitud y por lectura, incluidos cuerpos sin longitud declarada.
- Timeout del middleware ASP.NET → 504 controlado. Los tests de HTTP usan exclusivamente un controller en el assembly de integración; no agregan endpoints al host de producción.

### Contrato y evolución — T05/T06

- Se materializa la decisión U2 de la spec: versión inicial `/api/v1` (constantes `ApiConventions`), OpenAPI `v1`. Cambios compatibles son aditivos sin ampliar exposición; cambios incompatibles requieren nueva versión/ruta y aprobación humana. Deprecación debe anunciarse y documentarse con plazo revisado antes de retirar una versión; no se inventa una fecha de retiro para v1.
- Health es operacional y no versionado. OpenAPI se genera con el paquete existente y solo se expone en Development. No se anuncian esquemas de seguridad JWT o endpoints funcionales inexistentes.
- El controller WeatherForecast del bootstrap se conserva con `[NonController]`, retirando el scaffold de discovery/rutas; no se borran sus archivos.
- Falta revalidar en el árbol final el documento con health y sus esquemas Problem Details/traceId, bloqueado por la carga del host. No se reporta como contrato final aprobado el documento vacío validado antes de F4.

### Operación y protección — T07–T10

- `/health/live` no consulta dependencias; `/health/ready` consulta únicamente boundaries habilitadas. Probes por defecto devuelven no listo; falla/timeout → 503 sin detalles. Éxito agregado mínimo y `no-store`. Excepciones de probes no se adjuntan a logs/resultados de health.
- `X-Correlation-ID`: 1–64 caracteres ASCII alfanuméricos o `-_.`, un solo valor; inválidos/duplicados se reemplazan por GUID. Se propaga a `TraceIdentifier`, scope del logger, tag de Activity y respuesta, incluidos errores.
- Compresión: opt-in explícito para contenido público finito, GET, MIME `application/json`/`text/plain`, Brotli/Gzip, umbral configurable (default 1024 bytes), negociación por middleware oficial. Sin `Accept-Encoding`, identity, algoritmos no admitidos o q=0: no forzar compresión. HTTPS permitido solo bajo ese opt-in. Excluye Authorization, Cookie, usuario autenticado, Set-Cookie, private/no-store/no-transform y respuestas distintas de 200. Buffer máximo 1 MiB; al excederlo pasa a identity, sin buffer ilimitado; no es una solución streaming/SSE. Vary incluye Accept-Encoding.
- Rate limiting: ventana fija local, sin cola, default 60/60 s; partición por identidad autenticada confiable o IP de conexión, y política opcional por endpoint. 429 Problem Details + Retry-After; health excluido. No se usa X-Forwarded-For crudo como clave y **no** se presenta esta cuota local como global. Coordinación distribuida pendiente en las specs autorizables correspondientes.

### Forwarded headers y Azure

- Deshabilitados por defecto (`ForwardedHeaders.None`). Se desactiva expresamente el auto-startup permisivo `forwardedHeaders_enabled`; no se confía en habilitación indiscriminada por entorno.
- Al habilitar: allowlist explícita de proxies/IP networks validada al inicio, sin direcciones any ni redes /0; límite 1 por defecto, máximo 3; simetría de headers requerida. Solo se procesan For/Proto, nunca Host/Prefix. Se preservan las restricciones de host y la redirección HTTPS/HSTS.
- `ForwardedHeadersTests` incluye proxy desconocido, proxy permitido, cabeceras asimétricas, hop extra y red permitida. **No tiene PASS final** por la política Windows.
- Antes de desplegar en Azure: comprobar IP/rangos reales del ingress, cambios de rango, cadena/número de saltos, simetría, terminación TLS, redirección/HSTS, host público y direcciones IPv4/IPv6; probar spoofing y clave IP efectiva detrás de dos réplicas. No habilitar Proxy con rangos supuestos ni usar confianza universal para resolverlo. Es validación de despliegue futura, no evidencia de Azure obtenida en NV-01.

### Paginación — T11

`PageRequest` y `PageResult<T>`: page ≥ 1, pageSize 1–50 (default 20); metadatos exactos `items`, `page`, `pageSize`, `totalItems`, `totalPages`. Offset en long y cálculo de techo sin overflow; máximo de items validado. Página vacía permite 200; continuación cuando page < totalPages. El repositorio futuro debe aplicar el orden estable aprobado: NV-07 `createdAt`/`_id` descendente; NV-08 `name`/`_id` ascendente. Aquí no se implementan esos repositorios, consultas o endpoints. Las pruebas unitarias pasan; el contrato HTTP de paginación permanece sin validar por el bloqueo.

## Trazabilidad y cierre de tasks

| Requisito | Fase / tasks | Artefactos / pruebas principales | Estado final |
| --- | --- | --- | --- |
| R01 | F1 / T01, T18 | FoundationConfiguration, puertos/probes, ConfigurationTests | Revalidación integrada bloqueada |
| R02 | F1 / T02, T17, T18 | Opciones/configuración, ConfigurationTests | Revalidación integrada bloqueada |
| R03 | F2 / T03, T14, T17, T18 | ProblemResponses, ErrorContractTests | Revalidación integrada bloqueada |
| R04 | F2 / T04, T16, T18 | MVC/JSON, RequestBodyLimitMiddleware, ErrorContractTests | Revalidación integrada bloqueada |
| R05 | F3 / T05, T14, T18 | ApiConventions, política de evolución, OpenApiContractTests | Revalidación integrada bloqueada |
| R06 | F3 / T06, T14, T18 | OpenAPI/schema transformers, OpenApiContractTests | Contrato final con health pendiente |
| R07 | F4 / T07, T15, T18 | FoundationHealth, DependencyReadinessCheck, HealthAndRateLimitTests | Revalidación integrada bloqueada |
| R08 | F4 / T08, T15, T17, T18 | CorrelationIdMiddleware, CorrelationTests | Solo test directo PASS final; HTTP bloqueado |
| R09 | F4 / T09, T15, T18 | PublicResponseCompressionMiddleware, CompressionTests | PASS incremental; revalidación bloqueada |
| R10 | F4 / T10, T15, T17, T18 | FoundationRateLimits, HealthAndRateLimitTests | Revalidación integrada bloqueada |
| R11 | F5 / T11, T16, T18 | PageRequest/PageResult, PaginationTests/PaginationHttpTests | Unitarias PASS; HTTP bloqueado |
| R12 | F2 / T12, T17, T18 | FoundationExceptionMiddleware, RequestTimeouts, ErrorContractTests | Solo cancelación directa PASS final; HTTP bloqueado |
| R13 | Futura / T13 | Ninguna implementación | NO EJECUTADA; fuera de U2 |

Se comprobó que las huellas SHA-256 de los seis documentos normativos leídos permanecían sin cambios durante las revisiones incrementales. La instrucción humana vigente limita todas las iteraciones a NV-01. No se cierra ninguna task: resultados incrementales anteriores no permiten obviar el fallo de la puerta final ni la revisión de seguridad de T17. T01–T12 y T14–T18 permanecen abiertas/en verificación; T13 permanece futura. La fase de cierre no se considera superada ni habilita otro incremento.

## Revisión de alcance y seguridad — M-BE-027

**Hallazgos encontrados:**

| ID | Severidad | Hallazgo / impacto | Decisión y responsable |
| --- | --- | --- | --- |
| NV01-E01 | Medium | Política Windows impide pruebas obligatorias; no se puede asegurar aceptación integrada | **BLOCKED**, administrador/usuario: revisar política o facilitar entorno aprobado; repetir verificaciones |
| NV01-E02 | Informational | PASS incrementales no corresponden al árbol final completo | No usarlos para cierre; agente: todas las casillas permanecen abiertas |
| NV01-E03 | Informational | Topología ingress/TLS Azure y cuota distribuida no demostradas localmente | Fuera de implementación NV-01; responsable del despliegue: validación posterior con autorización separada |
| NV01-E04 | Low | Placeholder unitario preexistente no aporta comprobación funcional | Conservar, excluir de afirmaciones de cobertura; responsable de testing: tratar en revisión futura autorizada |

**Correcciones aplicadas:** validación temprana y diagnósticos sin valores; compresión negociada, acotada y opt-in; forwarded headers deny-by-default y allowlist explícita; límites de entrada; errores/correlación controlados. Se verificó comportamiento parcial con los comandos registrados, sin elevarlo a PASS final.

**Hallazgos pendientes:** NV01-E01 bloquea T14–T18 y el cierre; falta revalidación HTTP/headers y contrato final. No se ha confirmado un Critical/High o secreto expuesto en el código preparado; la revisión parcial **no acredita ausencia total** de vulnerabilidades.

Se revisaron por lectura entradas externas, errores/logging, configuración, secretos, compresión, límites y confianza en proxies. Auth, autorización por recurso, BOLA/BFLA, mass assignment de entidades y datos personales de negocio no se implementan aquí: no existen endpoints/entidades funcionales. Se rechazan miembros JSON desconocidos en el boundary MVC; esto no se presenta como autorización por recurso futura. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación o performance, ni se declara que hayan pasado.

**Resultado de verificación SDD: BLOCKED** — evidencia final insuficiente para cerrar tasks.

**Resultado de seguridad: BLOCKED** — pruebas de cabeceras y revalidación final no completadas.

## Archivos afectados y preservados

- Host: `TheHouse/Program.cs`, `TheHouse/appsettings.json`, `TheHouse/Controllers/WeatherForecastController.cs`; archivos nuevos en `TheHouse/Configuration/`, `TheHouse/Http/`, `TheHouse/Health/`.
- Application: archivos nuevos en `TheHouse.Application/Abstractions/Health/` y `TheHouse.Application/Pagination/`.
- Infrastructure: archivos nuevos en `TheHouse.Infrastructure/Configuration/` y `TheHouse.Infrastructure/Health/`.
- Pruebas: `TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj`; fixtures/tests nuevos en ese proyecto; `TheHouse.UnitTests/PaginationTests.cs`; `TheHouse.SecurityTests/ForwardedHeadersTests.cs`.
- Documentación: `specs/nv-01-foundation/evidence.md` y nota de seguimiento en `task.md`; `global.json` fija el SDK de Foundation.
- Sin cambios: Domain, solución, otros seis `.csproj`, referencias entre proyectos, gobierno raíz, `.opencode/`, specs posteriores y `UnitTest/` residual (solo `obj/`, no eliminado). No se recrearon/movieron proyectos.
- Sin entidades, repositories, seed, Register/Login/Logout, tokens, endpoints de propiedades/universidades, contenedores, workflows o Terraform. El grep de revisión del host no encontró registros de autenticación, clientes Mongo/Redis, repositories, seed o endpoints POST/PUT/DELETE.
- No se hicieron `git add`, commit o push: la condición humana exige que pasen build, tests, SDD y seguridad y no se satisface. Los cambios NV-01 quedan sin commit, y los tres stashes quedan **CONSERVADOS**.

**Estado: BLOCKED. Detenerse; no avanzar a NV-04 ni a otra spec.**
