# NV-06 — Evidencia de Logout U2

## Estado

**✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

Resultado:

**PASS WITH WARNINGS**

## Endpoint

`POST /api/v1/auth/logout`

Contrato validado:

- Bearer obligatorio;
- sin body;
- primer logout válido: `204 No Content`;
- segundo logout con el mismo JWT: `401`;
- token ausente, inválido, expirado o revocado: `401`;
- Authorization mal formado o body inesperado: `400`;
- rate limit excedido: `429`;
- Redis no disponible: `503`.

## Revocación

La revocación utiliza el `jti` del JWT.

Clave:

`auth:revoked:jti:{jti}`

Características:

- valor de revocación almacenado en Redis;
- operación atómica;
- TTL limitado a la vida restante del JWT;
- no existe fallback en memoria;
- no existe caché local de revocación;
- la reutilización de un token revocado es rechazada.

## Guardia de revocación

Se implementó un mecanismo reutilizable formado por:

- `TokenRevocationMiddleware`;
- `RevocationGuard`;
- `RequireTokenNotRevoked()`.

Para endpoints marcados como protegidos:

- token válido/no revocado permite continuar;
- token revocado devuelve `401`;
- `jti` ausente devuelve `401`;
- Redis no disponible devuelve `503`;
- no existe fallback local.

La comprobación se realiza contra `ITokenRevocationStore`.

## Multi-réplica

Se validó mediante pruebas con store compartido:

1. una réplica observa un token inicialmente válido;
2. otra réplica registra su revocación;
3. la primera réplica vuelve a consultar el store compartido;
4. la reutilización es rechazada con `401`.

Esto demuestra la semántica requerida independientemente del proceso que atienda la solicitud.

## Rate limiting

Logout aplica:

`30 solicitudes / 60 segundos / IP`

Estado almacenado mediante Redis.

Cuando se supera:

`429 Too Many Requests`

con:

`Retry-After: 60`

## Fail-closed

Se validó comportamiento fail-closed ante:

- Redis no disponible;
- JWT inválido;
- JWT expirado;
- `jti` ausente;
- token previamente revocado.

No se permite continuar mediante almacenamiento local o fallback en memoria.

## Seguridad

Se validó:

- JWT no registrado en logs;
- `jti` no registrado en logs;
- Authorization no registrado en logs;
- token no aceptado por query string;
- `Cache-Control: no-store`;
- `Pragma: no-cache`;
- errores sin stack trace ni detalles internos de Redis.

Resultado de seguridad:

**PASS**

## Problem Details

Se validaron respuestas controladas para:

- `400`;
- `401`;
- `429`;
- `503`;
- `500`.

Los errores mantienen el contrato compartido de Problem Details y `traceId`.

## OpenAPI

Se incorporó únicamente:

`POST /api/v1/auth/logout`

al conjunto de operaciones funcionales autorizadas de U2.

No se agregó ningún endpoint productivo adicional.

## Pruebas locales

Comandos ejecutados:

`dotnet build TheHouse.slnx`

Resultado:

- 0 errores;
- 0 advertencias.

Comando:

`dotnet test TheHouse.slnx --no-build`

Resultado:

**253 / 253 PASS**

Distribución:

- Unit: 66;
- Security: 28;
- Integration: 159.

También se ejecutó:

`git diff --check`

sin errores de whitespace.

## Cobertura específica de revocación

Las pruebas incluyen:

- primer logout `204`;
- reutilización `401`;
- token expirado `401`;
- token ausente/inválido;
- body inesperado;
- Authorization mal formado;
- Redis fail-closed;
- rate limit;
- TTL;
- revocación compartida entre réplicas;
- guardia central de revocación;
- `jti` ausente;
- ausencia de JWT/jti/Authorization en logs.

## GitHub Actions

Workflow:

`U2 Linux Validation`

Branch:

`feature/u2-entrega`

Commit técnico:

`8cfad4a`

Run:

`41771006554`

Job:

`126380428996`

Resultado:

**SUCCESS**

Etapas:

- Checkout repository — PASS;
- Setup .NET 10 — PASS;
- Restore — PASS;
- Build — PASS;
- Run tests — PASS;
- Upload test results — PASS.

Artifact:

`u2-test-results`

## Matriz de evidencia

| Requisito | Evidencia | Resultado |
| --- | --- | --- |
| R01 | LogoutEndpoint, Bearer, body vacío, 204 | PASS |
| R02 | Revocation store, jti, TTL y 204→401 | PASS |
| R03 | Store compartido, guardia y fail-closed | PASS WITH WARNING |
| R04 | 204 inicial, 401 reutilización/expirado | PASS |
| R05 | Problem Details y pruebas HTTP | PASS |
| R06 | no-store, rate limit, logs sanitizados | PASS |
| R07 | Unit, Integration, Security, multi-réplica y CI | PASS |

## Advertencias

### W01 — Redis real

El comportamiento de `RedisTokenRevocationStore` se encuentra implementado y cubierto por pruebas automatizadas.

Esta evidencia no afirma todavía conectividad real contra la instancia Redis cloud.

La verificación desplegada corresponde a **NV-03**.

### W02 — Endpoints protegidos futuros

El mecanismo central de revocación está implementado mediante middleware y convención de endpoint.

Actualmente U2 no contiene otro endpoint productivo autenticado que requiera adjuntarlo.

Todo futuro endpoint protegido deberá utilizar la convención de revocación correspondiente.

### W03 — Logout concurrente

La semántica secuencial exigida:

`204 → 401`

está validada.

Dos solicitudes de logout exactamente simultáneas pueden consultar la revocación antes de que una de ellas complete el `SET NX`.

Esto no incrementa permisos ni revoca sesiones adicionales y queda registrado como consideración informativa.

## Fuera de alcance

NV-06 no implementa:

- refresh token;
- revocación por usuario;
- logout de todos los dispositivos;
- roles;
- administración de sesiones;
- NV-02;
- infraestructura cloud.

## Decisión final

**PASS WITH WARNINGS**

No existen hallazgos Critical o High abiertos.

NV-06 cumple su alcance funcional y de seguridad para U2.

La comprobación contra Redis real se realizará en NV-03.

**NV-06 queda EJECUTADA, VALIDADA Y CERRADA PARA U2.**

## Addendum — Correcciones de revisión U2 (2026-11-05)

Correcciones autorizadas por Victor Manuel Jose Jose el 2026-11-05 en la rama `fix/u2-review`. No se modifican requisitos, alcance ni tasks de NV-06.

- Problem Details de logout: títulos en español y consistentes (`No autorizado`, `Acceso denegado`, `Servicio no disponible`). El contrato omite `detail` e `instance` en este endpoint, sin datos internos ni tokens.
- Pruebas actualizadas: `LogoutHttpTests` y `LogoutSecurityTests` verifican los títulos en español y la semántica de revocación.
- La revocación distribuida mantiene fail-closed ante indisponibilidad de Redis; no se modificó su comportamiento.
- Codificación reparada en pruebas y documentos; sin BOM. `.editorconfig` fija UTF-8.
- Verificación ejecutada: `dotnet build --verbosity quiet` con 0 errores y 0 advertencias; `dotnet test --verbosity quiet` con 130 pruebas unitarias, 39 de seguridad y 227 de integración: 396 aprobadas y 0 fallidas.
- Nota de entorno: `TheHouse/appsettings.Development.json` es un archivo local ignorado por Git, no forma parte del repositorio y habilita JWT sin `JWT_SIGNING_KEY`; para ejecutar la suite se excluyó temporalmente y se restauró byte a byte sin incorporarlo al commit. Esta limitación se declara para no atribuir a la suite una ejecución que dependa de configuración no versionada.
- Seguridad: revisión de OWASP API Security Top 10, secretos, autenticación, autorización, validación de entrada, exposición de datos, errores, logging, rate limiting y mass assignment. Sin hallazgos Critical/High ni secretos confirmados en el alcance revisado. Resultado: PASS WITH WARNINGS. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación ni performance.
- SDD: la revisión no crea tasks, no reabre el cierre U2 y no autoriza implementación adicional. Resultado: PASS WITH WARNINGS.
