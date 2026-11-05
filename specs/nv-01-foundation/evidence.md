# NV-01 Foundation — Evidencia de Fase B U2

| Campo | Valor |
| --- | --- |
| Spec / plan / task | NV-01, `1.1.0-u2`, en esta carpeta |
| Fecha de esta revisión | 2026-11-03 |
| Rama / baseline | `feature/u2-entrega` / `53bd45681aa0295da943b639151c93c2191bdea9` |
| Commit probado | `024d5f7` (`024d5f73190f2973d2d79b4f1faec634241aac4e`) |
| Autorización de ejecución | Instrucción humana explícita del usuario en esta sesión: **APROBADO — FASE B U2** |
| Tasks autorizadas | T01–T12, T14–T18 |
| Exclusiones | T13, NV-02, NV-03, NV-04 a NV-08, Terraform y cloud |
| Resultado del cierre U2 | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** — T01–T12, T14–T18; T13 excluida |

## Validación CI utilizada para el cierre

El usuario comunicó como **estado comprobado** la validación remota ya realizada de `024d5f7`. Este cierre documental usa esa confirmación humana y la revisión en lectura de los artefactos del commit; no vuelve a ejecutar restore, build, tests ni controles de NV-02.

| Campo / paso | Evidencia comunicada por el usuario |
| --- | --- |
| Rama | `feature/u2-entrega` |
| Commit probado | `024d5f7` |
| Workflow | `U2 Linux Validation` |
| Entorno | GitHub Actions / Ubuntu |
| Restore | **PASS** |
| Build | **PASS** |
| Run tests | **PASS** |
| Workflow completo | **SUCCESS** |
| Artifact | `u2-test-results` |

Se comprobó localmente mediante lectura de Git que HEAD corresponde al commit completo indicado, sin modificaciones de código rastreado. `.github/workflows/u2-linux-validation.yml` concuerda con el nombre, Ubuntu (`ubuntu-latest`), SDK `10.0.x`, pasos y artifact comunicados. Sus comandos son:

```text
dotnet restore TheHouse.slnx
dotnet build TheHouse.slnx --configuration Release --no-restore --nologo
dotnet test TheHouse.slnx --configuration Release --no-build --nologo --logger trx --results-directory "${{ github.workspace }}/TestResults"
```

**Procedencia:** resultados CI y existencia del artifact confirmados por el usuario; workflow, commit y contenido de las suites inspeccionados en lectura. GitHub CLI no tiene autenticación en este entorno: no se descargaron ni se inspeccionaron independientemente los TRX remotos. No se inventan URL/run ID, duración, cantidad de tests CI, cobertura o evidencia de scanners. El artifact indicado es la referencia de resultados; los números Windows de la sección histórica no son números CI.

La validación integrada Linux supera la limitación probatoria del intento Windows. **Windows local bloqueado por Smart App Control / Code Integrity no es un fallo funcional de NV-01** ni exige desactivar esos controles para cerrar U2. El resultado CI no se presenta como ejecución de NV-02 o validación de despliegue Azure.

### Brechas conocidas y decisión de cierre

| Tema | Resultado NV-01 | Soporte de aceptación |
| --- | --- | --- |
| Configuración inválida | **RESUELTA** | Validación antes de Build y al arranque, rangos y binding estricto; `ConfigurationTests` verifica rechazo y ausencia de valores sensibles en errores |
| Compression negotiation | **RESUELTA** | Negociación oficial `Accept-Encoding`, Brotli/Gzip, q=0, identity, umbral, MIME y exclusiones; matriz de `CompressionTests` |
| Forwarded headers | **CONFIGURACIÓN SEGURA** | Deshabilitados por defecto; confianza explícita, límite de saltos, simetría y For/Proto únicamente; `ForwardedHeadersTests` |
| Azure ingress real | **PENDIENTE SOLO EN NV-03** | Verificación de rangos, TLS, cadena de proxies y dos réplicas requiere despliegue separado; no es aceptación pendiente de Foundation |

Se revisaron esos criterios y el resto de R01–R12 contra las suites incluidas en el commit probado y el resultado CI comunicado. No se detectaron criterios U2 incumplidos ni hallazgos bloqueantes dentro de NV-01. Las advertencias no bloqueantes y sus responsables están en la revisión M-BE-027 más abajo.

## Autorización y preservación — registro de implementación previo

Se leyeron, en orden, `constitution.md`, `AGENTS.md`, `memoria.md` (incluida M-BE-027), `spec.md`, `plan.md` y `task.md`. Los tres documentos NV-01 están aprobados documentalmente por Victor Manuel Jose Jose el 2026-11-02. La autorización posterior de ejecución es la instrucción del usuario de esta sesión; no se presume otra identidad para ese mensaje.

Se declaró antes de implementar:

```text
SPEC ACTIVA: NV-01 Foundation 1.1.0-u2
PLAN ACTIVO: specs/nv-01-foundation/plan.md
TASK ACTIVA: T01-T12, T14-T18
ESTADO AUTORIZADO: APROBADO — FASE B U2
```

La autorización permitió únicamente Foundation, sus pruebas y evidencia. Las prohibiciones históricas de preparación documental se levantaron solo en ese alcance por la instrucción humana; no se cambió el diseño aprobado ni se aprobó otra spec. Las leyendas documentales «hasta autorización de Fase B» deben leerse con ese registro posterior. La autorización de ejecución no equivalía por sí sola a un cierre aprobado: el cierre actual añade la validación CI y la revisión documental de aceptación.

La solicitud humana actual autoriza exclusivamente este cierre en `task.md` y `evidence.md`. `spec.md` y `plan.md` `1.1.0-u2` conservan su contenido aprobado y sus estados de planificación originales; el estado posterior de ejecución/cierre se registra aquí y en `task.md`, sin cambiar requisitos, versiones o alcance. No es una nueva autorización de implementación.

Se ejecutaron `git status --short`, `git stash push -u -m "wip: interrupted u2 phase b before nv01 cleanup"`, `git stash list` y `git status`. El stash terminó correctamente y dejó el árbol limpio. El estado observado antes del stash tenía 3 archivos rastreados modificados y archivos nuevos, sin eliminaciones rastreadas; no coincidía con las cantidades del resumen humano. No se asumió que los otros stashes fueran descartables.

Se conservaron los tres snapshots existentes después de la preservación:

| Referencia al momento de revisión | SHA |
| --- | --- |
| `stash@{0}` — creado en esta sesión | `0dc404f962ade97ee42d16785f3adc039049b2c0` |
| `stash@{1}` — preexistente | `b25e6c20d3f9519d1fa3e1ce287f2096d493bf1b` |
| `stash@{2}` — preexistente | `527f927b0ab48b9a78462973634bb1322f587207` |

Ningún stash se reaplicó, eliminó ni sobrescribió. No se usó reset, restore, clean o checkout forzado. El código nuevo se preparó sobre el baseline limpio, no reaplicando el trabajo mezclado.

## Histórico de ejecución local Windows — no es el resultado final CI

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

Intento de validación final local anterior a CI:

| Comando | Resultado |
| --- | --- |
| `dotnet build` | **PASS**, 0 errores y 0 advertencias |
| `dotnet test` | **FAIL**, código de salida 1; desglose debajo |
| `git status --short` | Solo cambios de Foundation y esta evidencia; sin stage |
| `git diff --stat` | Revisado; describe archivos rastreados, no incluye los nuevos sin stage |
| `git diff --check` | PASS; sin errores de whitespace; avisos de conversión LF/CRLF de Git |

| Suite local Windows (histórica) | PASS | FAIL por ejecución bloqueada | Omitidas |
| --- | ---: | ---: | ---: |
| TheHouse.UnitTests | 16 | 0 | 0 |
| TheHouse.IntegrationTests | 2 | 95 | 0 |
| TheHouse.SecurityTests | 0 | 6 | 0 |
| Total | 18 | 101 | 0 |

Una de las 16 pruebas unitarias locales es el placeholder preexistente `TheHouse.UnitTests/UnitTest1.cs`; no representa cobertura funcional. Los dos PASS de integración locales son comprobaciones directas de cancelación/correlación que no necesitan arrancar el host. Los FAIL del intento Windows se debieron a carga de assemblies bloqueada y no se clasifican como defectos funcionales de Foundation. No se declara cobertura, rendimiento ni equivalencia con los 73 PASS de una ejecución anterior.

### Bloqueo de plataforma confirmado

`System.IO.FileLoadException`, HRESULT **`0x800711C7`**: «Una directiva de Control de aplicaciones bloqueó este archivo». El intento Windows no pudo cargar `TheHouse.Application.dll` desde los directorios de salida de integración y seguridad. Una ejecución anterior tampoco pudo cargar el assembly de SecurityTests.

La lectura de `Microsoft-Windows-CodeIntegrity/Operational` confirmó eventos **3077 y 3033**, a las 11:05:17 hora local del 2026-11-03, que indican incumplimiento de requisitos de firma Enterprise / política de integridad. El SHA-256 de las copias de Application en su salida, UnitTests e IntegrationTests era idéntico: `E12C0AD06DBA885CA9405C39D396DD020BB856E01F972B5617E5FD15D33F01DE`. No hay evidencia de que modificar lógica resuelva esa política.

No se deshabilitaron, alteraron ni eludieron controles de Windows; tampoco se movieron assemblies para evitar la política ni se cambió código para alterar hashes con ese fin. La validación posterior en GitHub Actions / Ubuntu satisface la revalidación integrada para este cierre documental. La limitación local permanece como incidencia del entorno: su eventual tratamiento corresponde al administrador por el procedimiento aprobado, no es remediación funcional pendiente de NV-01.

## Diseño Foundation revisado en el commit validado y límites

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
- La validación CI comunicada corresponde al commit completo, incluidas las rutas health, no al documento vacío anterior a F4. Se revisaron `ApiConventions`, el registro de respuestas/esquemas en `FoundationHealth`, la fábrica de errores y `OpenApiContractTests`: versión, generación reproducible, rutas operacionales y ausencia de endpoints de negocio/secretos. No se anuncian esquemas JWT o ejemplos sensibles ficticios. No se afirma haber descargado el OpenAPI generado remotamente.

### Operación y protección — T07–T10

- `/health/live` no consulta dependencias; `/health/ready` consulta únicamente boundaries habilitadas. Probes por defecto devuelven no listo; falla/timeout → 503 sin detalles. Éxito agregado mínimo y `no-store`. Excepciones de probes no se adjuntan a logs/resultados de health.
- `X-Correlation-ID`: 1–64 caracteres ASCII alfanuméricos o `-_.`, un solo valor; inválidos/duplicados se reemplazan por GUID. Se propaga a `TraceIdentifier`, scope del logger, tag de Activity y respuesta, incluidos errores.
- Compresión: opt-in explícito para contenido público finito, GET, MIME `application/json`/`text/plain`, Brotli/Gzip, umbral configurable (default 1024 bytes), negociación por middleware oficial. Sin `Accept-Encoding`, identity, algoritmos no admitidos o q=0: no forzar compresión. HTTPS permitido solo bajo ese opt-in. Excluye Authorization, Cookie, usuario autenticado, Set-Cookie, private/no-store/no-transform y respuestas distintas de 200. Buffer máximo 1 MiB; al excederlo pasa a identity, sin buffer ilimitado; no es una solución streaming/SSE. Vary incluye Accept-Encoding.
- Rate limiting: ventana fija local, sin cola, default 60/60 s; partición por identidad autenticada confiable o IP de conexión, y política opcional por endpoint. 429 Problem Details + Retry-After; health excluido. No se usa X-Forwarded-For crudo como clave y **no** se presenta esta cuota local como global. Coordinación distribuida pendiente en las specs autorizables correspondientes.

### Forwarded headers y Azure

- Deshabilitados por defecto (`ForwardedHeaders.None`). Se desactiva expresamente el auto-startup permisivo `forwardedHeaders_enabled`; no se confía en habilitación indiscriminada por entorno.
- Al habilitar: allowlist explícita de proxies/IP networks validada al inicio, sin direcciones any ni redes /0; límite 1 por defecto, máximo 3; simetría de headers requerida. Solo se procesan For/Proto, nunca Host/Prefix. Se preservan las restricciones de host y la redirección HTTPS/HSTS.
- `ForwardedHeadersTests` incluye proxy desconocido, proxy permitido, cabeceras asimétricas, hop extra y red permitida; forma parte de la solución validada con Run tests **PASS** en CI Linux. El bloqueo Windows no invalida esa validación.
- Antes de desplegar en Azure: comprobar IP/rangos reales del ingress, cambios de rango, cadena/número de saltos, simetría, terminación TLS, redirección/HSTS, host público y direcciones IPv4/IPv6; probar spoofing y clave IP efectiva detrás de dos réplicas. No habilitar Proxy con rangos supuestos ni usar confianza universal para resolverlo. Es validación de despliegue futura, no evidencia de Azure obtenida en NV-01.

### Paginación — T11

`PageRequest` y `PageResult<T>`: page ≥ 1, pageSize 1–50 (default 20); metadatos exactos `items`, `page`, `pageSize`, `totalItems`, `totalPages`. Offset en long y cálculo de techo sin overflow; máximo de items validado. Página vacía permite 200; continuación cuando page < totalPages. El repositorio futuro debe aplicar el orden estable aprobado: NV-07 `createdAt`/`_id` descendente; NV-08 `name`/`_id` ascendente. Aquí no se implementan esos repositorios, consultas o endpoints. `PaginationTests` y `PaginationHttpTests` cubren límites, metadatos, vacío, overflow y entradas malformadas, incluidos en la solución con Tests PASS comunicado en CI.

## Trazabilidad y cierre de tasks

| Requisito | Fase / tasks | Artefactos / pruebas principales | Estado final |
| --- | --- | --- | --- |
| R01 | F1 / T01, T18 | FoundationConfiguration, puertos/probes, ConfigurationTests | CUMPLIDO: revisión DI/lifetimes + CI PASS |
| R02 | F1 / T02, T17, T18 | Opciones/configuración, ConfigurationTests | CUMPLIDO: validación temprana y matriz de entornos + CI PASS |
| R03 | F2 / T03, T14, T17, T18 | ProblemResponses, ErrorContractTests | CUMPLIDO: contrato y ausencia de fugas + CI PASS |
| R04 | F2 / T04, T16, T18 | MVC/JSON, RequestBodyLimitMiddleware, ErrorContractTests | CUMPLIDO: validación HTTP/400/413 + CI PASS |
| R05 | F3 / T05, T14, T18 | ApiConventions, política de evolución, OpenApiContractTests | CUMPLIDO: /api/v1 y política documentada + CI PASS |
| R06 | F3 / T06, T14, T18 | OpenAPI/schema transformers, FoundationHealth, OpenApiContractTests | CUMPLIDO: contrato operacional reproducible + CI PASS |
| R07 | F4 / T07, T15, T18 | FoundationHealth, DependencyReadinessCheck, HealthAndRateLimitTests | CUMPLIDO: live/ready/boundaries/timeouts + CI PASS |
| R08 | F4 / T08, T15, T17, T18 | CorrelationIdMiddleware, CorrelationTests | CUMPLIDO: formato, longitud, propagación y eco + CI PASS |
| R09 | F4 / T09, T15, T18 | PublicResponseCompressionMiddleware, CompressionTests | CUMPLIDO: negociación, umbral, exclusiones y HTTPS + CI PASS |
| R10 | F4 / T10, T15, T17, T18 | FoundationRateLimits, HealthAndRateLimitTests | CUMPLIDO: políticas, 429/Retry-After y alcance local + CI PASS |
| R11 | F5 / T11, T16, T18 | PageRequest/PageResult, PaginationTests/PaginationHttpTests | CUMPLIDO: límites/metadatos y convención de orden + CI PASS |
| R12 | F2 / T12, T17, T18 | FoundationExceptionMiddleware, RequestTimeouts, ErrorContractTests | CUMPLIDO: clasificación, cancelación y timeout + CI PASS |
| R13 | Futura / T13 | Ninguna implementación | NO EJECUTADA; fuera de U2 |

En la implementación se comprobaron las huellas de los documentos normativos durante las revisiones incrementales. En este cierre se releen constitución, AGENTS, memoria, spec, plan y task en orden y se contrasta aceptación con los artefactos existentes y el resultado CI comunicado. R01–R12 tienen traza requisito → fase → task → implementación/pruebas → CI/revisión; no hay tasks U2 sin requisito ni requisitos U2 pendientes. T14 cubre contrato, T15 operación/protección y T16 negativos; T17 es la revisión de seguridad siguiente y T18 este paquete de evidencia y acta de cierre. Las 17 tasks U2 se marcan completadas tras esa revisión; T13 queda abierta y no bloquea U2 según spec/plan. No se habilita otro incremento.

## Revisión documental de cierre SDD y seguridad — M-BE-027

**Hallazgos encontrados:**

| ID | Severidad | Hallazgo / impacto | Decisión y responsable |
| --- | --- | --- | --- |
| NV01-E01 | Informational (Medium en el intento previo) | Bloqueo local Windows de Code Integrity / Smart App Control; no defecto funcional NV-01 | Bloqueo de cierre **SUPERADO** con CI Ubuntu de `024d5f7`; administrador: tratar incidencia local por proceso aprobado si se requiere ejecución Windows |
| NV01-E02 | Informational | PASS incrementales no corresponden al árbol final completo | **RESUELTO** para cierre: se usa CI del commit completo y revisión de aceptación, no resultados incrementales aislados |
| NV01-E03 | Informational | Topología ingress/TLS Azure y cuota distribuida no demostradas por pruebas Foundation | Fuera de cierre NV-01; responsable NV-03/despliegue: validar topología Azure antes de desplegar, con autorización separada; sin conexiones o cuota global ficticias |
| NV01-E04 | Low | Placeholder unitario preexistente no aporta comprobación funcional | Conservar, excluir de afirmaciones de cobertura; responsable de testing: tratar en revisión futura autorizada |

**Correcciones aplicadas en el commit validado:** validación temprana y diagnósticos sin valores; compresión negociada, acotada y opt-in; forwarded headers deny-by-default y allowlist explícita; límites de entrada; errores/correlación controlados. CI comunicado: Restore/Build/Tests PASS. En este cierre solo se actualizan estados/evidencia; no se modifica código.

**Hallazgos pendientes:** sin bloqueantes NV-01. NV01-E01 queda como limitación del entorno local, NV01-E03 es validación futura fuera de alcance y NV01-E04 es la advertencia Low del placeholder, no contabilizado como cobertura. T13 queda futura por autorización expresa. En la revisión acotada del commit no se identificaron secretos confirmados, Critical/High, fugas internas, validaciones obligatorias ausentes ni confianza indiscriminada en headers. Esto no equivale a ausencia universal de vulnerabilidades ni a auditoría DevSecOps completa.

Se revisaron por lectura entradas externas, errores/logging, configuración, secretos, compresión, límites, exposición de OpenAPI y confianza en proxies, junto con los casos de pruebas existentes. Auth, autorización por recurso, BOLA/BFLA, mass assignment de entidades y datos personales de negocio no se implementan aquí: no existen endpoints/entidades funcionales. Se rechazan miembros JSON desconocidos en el boundary MVC; esto no se presenta como autorización por recurso futura. La clave IP no usa X-Forwarded-For sin el middleware confiable; respuestas de error/logs propios no incluyen mensajes de excepción ni entradas; opciones de secretos no tienen valores reales en configuración versionada. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación o performance, ni se declara que hayan pasado. El workflow existente se lee solo para trazar esta validación, no se crea, modifica, dispara ni convierte en ejecución de NV-02.

**Resultado de verificación SDD: PASS WITH WARNINGS** — aceptación R01–R12 cubierta por revisión y CI comunicado, con procedencia explícita; 17 tasks U2 completas y T13 excluida. Las advertencias enumeradas no bloquean Foundation ni conceden autorización transitiva.

**Resultado de seguridad: PASS WITH WARNINGS** — configuración segura y suites negativas incluidas en la validación CI, sin hallazgos bloqueantes en el alcance revisado; Azure sigue pendiente exclusivamente en NV-03 y el placeholder no aporta cobertura.

**Verificación documental ejecutada:** `git diff --check` PASS; comprobación de texto UTF-8 contra HEAD PASS: exactamente 17 casillas `[x]`, T13 como única `[ ]`, 17 estados COMPLETADA en la matriz y R01–R12 con evidencia de cierre. Descripciones, criterios de aceptación, IDs y dependencias de las 18 tasks permanecen idénticos a HEAD salvo el marcado de completitud. No se usó un nuevo build/test para validar Markdown.

## Archivos de la implementación previa y preservación del cierre

- Host: `TheHouse/Program.cs`, `TheHouse/appsettings.json`, `TheHouse/Controllers/WeatherForecastController.cs`; archivos nuevos en `TheHouse/Configuration/`, `TheHouse/Http/`, `TheHouse/Health/`.
- Application: archivos nuevos en `TheHouse.Application/Abstractions/Health/` y `TheHouse.Application/Pagination/`.
- Infrastructure: archivos nuevos en `TheHouse.Infrastructure/Configuration/` y `TheHouse.Infrastructure/Health/`.
- Pruebas: `TheHouse.IntegrationTests/TheHouse.IntegrationTests.csproj`; fixtures/tests nuevos en ese proyecto; `TheHouse.UnitTests/PaginationTests.cs`; `TheHouse.SecurityTests/ForwardedHeadersTests.cs`.
- Documentación: `specs/nv-01-foundation/evidence.md` y nota de seguimiento en `task.md`; `global.json` fija el SDK de Foundation.
- Sin cambios: Domain, solución, otros seis `.csproj`, referencias entre proyectos, gobierno raíz, `.opencode/`, specs posteriores y `UnitTest/` residual (solo `obj/`, no eliminado). No se recrearon/movieron proyectos.
- Sin entidades, repositories, seed, Register/Login/Logout, tokens, endpoints de propiedades/universidades, contenedores o Terraform. El host no incorpora autenticación, clientes Mongo/Redis, repositories, seed o endpoints POST/PUT/DELETE. Existe el workflow independiente de validación Linux ya aportado; no se audita o ejecuta NV-02 por utilizar su resultado.
- El registro local previo terminó sin commit/push; posteriormente el código quedó en `024d5f7`, que es el commit probado indicado por el usuario y verificado en HEAD. En **esta revisión documental** no se hacen `git add`, commit, push, restore, build, tests o scanners.
- Solo se modifican `specs/nv-01-foundation/task.md` y `specs/nv-01-foundation/evidence.md`. Código, pruebas, `.csproj`, solución, workflow, gobierno y otros documentos permanecen intactos. `global.json` ya estaba sin seguimiento al comenzar y se conserva sin tocar; no se atribuye ese archivo local al commit probado.
- Inventario SHA-256 antes/después de los 92 archivos rastreados o no ignorados fuera de los dos documentos editables: `B54C8EBE3333E55707339EABF94985340AE587334E037E478B1E9C7653780E50`. Incluye los siete proyectos, solución y `global.json`; no incluye salidas ignoradas de build. Los tres stashes conservan sus referencias y SHA registrados; no se reaplican ni eliminan.
- COST-001 no se relaja: esta revisión solo lee evidencia y modifica Markdown; no dispara CI, servicios, despliegue ni gasto. No se afirma validación de elegibilidad/facturación de Azure o de la plataforma CI por el hecho de que el workflow termine SUCCESS.

**NV-01 Foundation 1.1.0-u2: ✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

Tasks U2 pendientes: **ninguna**. T13: **NO EJECUTADA, [ ] abierta, fuera de U2**. NV-02 continúa **⛔ NO EJECUTAR**. Terraform no autorizado. Detenerse; no avanzar a NV-04 ni ejecutar otra spec.

## Addendum — Correcciones de revisión U2 (2026-11-05)

Correcciones autorizadas por Victor Manuel Jose Jose el 2026-11-05 en la rama `fix/u2-review`, limitadas a los hallazgos de la revisión U2. Este addendum no reabre el cierre de NV-01 ni autoriza nuevas tasks.

- Codificación: se repararon secuencias de doble codificación (mojibake) en documentos y pruebas, y reemplazos literales `Validaci?n`; los archivos de texto quedaron en UTF-8 válido y sin BOM. Se agregó `.editorconfig` con `charset = utf-8` y finales de línea LF.
- SDK: `global.json` usa `rollForward: latestFeature` y acepta SDK 10.0.x desde 10.0.114 en adelante, sin fijar una revisión inexistente.
- Problem Details: títulos y mensajes unificados en español; `ProblemResponses.TitleFor(int)` es la fuente única de títulos para 400, 401, 403, 404, 405, 408, 409, 413, 415, 429, 500, 503 y 504.
- Se retiró el BOM de `.github/workflows/u2-linux-validation.yml`, `TheHouse/Properties/launchSettings.json`, `TheHouse.UnitTests/TheHouse.UnitTests.csproj` y `TheHouse.UnitTests/UnitTest1.cs`.
- Verificación ejecutada: `dotnet build --verbosity quiet` con 0 errores y 0 advertencias; `dotnet test --verbosity quiet` con 130 pruebas unitarias, 39 de seguridad y 227 de integración: 396 aprobadas y 0 fallidas.
- Nota de entorno: `TheHouse/appsettings.Development.json` es un archivo local ignorado por Git, no forma parte del repositorio y habilita JWT sin `JWT_SIGNING_KEY`; para ejecutar la suite se excluyó temporalmente y se restauró byte a byte sin incorporarlo al commit. Esta limitación se declara para no atribuir a la suite una ejecución que dependa de configuración no versionada.
- Seguridad: revisión de OWASP API Security Top 10, secretos, autenticación, autorización, validación de entrada, exposición de datos, errores, logging, rate limiting y mass assignment. Sin hallazgos Critical/High ni secretos confirmados en el alcance revisado. Resultado: PASS WITH WARNINGS. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación ni performance.
- SDD: la revisión no crea tasks, no reabre el cierre U2 y no autoriza implementación adicional. Resultado: PASS WITH WARNINGS.
