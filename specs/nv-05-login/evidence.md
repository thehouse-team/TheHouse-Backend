# NV-05 — Evidencia de Login U2

## Estado

**✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

Resultado de revisión:

**PASS WITH WARNINGS**

## Endpoint validado

`POST /api/v1/auth/login`

NV-05 implementa autenticación mediante correo electrónico y contraseña y entrega un access token JWT cuando las credenciales son válidas.

## Contrato

### Request

- `email`
- `password`

### Response 200

- `accessToken`
- `tokenType`
- `expiresIn`

No forma parte de la respuesta:

- contraseña;
- hash;
- salt;
- refresh token;
- roles internos;
- información interna de persistencia.

## Validación de credenciales

Se implementó y validó:

- normalización de correo;
- validación de formato y longitud;
- consulta por correo normalizado;
- verificación PBKDF2-HMAC-SHA256;
- comparación criptográfica en tiempo constante;
- ejecución de PBKDF2 dummy cuando el usuario no existe;
- respuesta genérica `401 Invalid credentials`;
- mismo contrato público para usuario inexistente y contraseña incorrecta;
- ausencia de contraseña, hash y access token en logs controlados.

### Advertencia sobre timing

La mitigación contra enumeración temporal está implementada mediante una verificación PBKDF2 dummy con costo equivalente al flujo normal.

NV-05 no contiene un benchmark estadístico dedicado que demuestre igualdad temporal absoluta entre usuario inexistente y contraseña incorrecta.

Por ello este punto queda documentado como una advertencia y no se sobredeclara evidencia inexistente.

## JWT

Configuración implementada:

- algoritmo: `HS256`;
- issuer: `thehouse-api`;
- audience: `thehouse-clients`;
- expiración: `900` segundos;
- clock skew: `30` segundos;
- `jti` generado con 128 bits aleatorios.

Claims requeridos:

- `iss`;
- `aud`;
- `sub`;
- `email`;
- `jti`;
- `iat`;
- `nbf`;
- `exp`.

Las pruebas de seguridad cubren rechazo de:

- token con firma incorrecta;
- `alg=none`;
- token expirado;
- token sin `jti`;
- algoritmos no autorizados mediante la política de validación.

El secreto JWT no se almacena en `appsettings.json`.

Se obtiene mediante:

`JWT_SIGNING_KEY`

La configuración inválida falla antes de servir solicitudes.

## Rate limiting

Se implementaron dos límites de Login:

### IP + email

`5 / 60 segundos`

### IP

`20 / 60 segundos`

El adaptador productivo utiliza Redis.

No existe fallback en memoria para producción.

Cuando Redis no está disponible:

`503 Service Unavailable`

Cuando se excede el límite:

`429 Too Many Requests`

con:

`Retry-After: 60`

## Fail-closed

NV-05 aplica comportamiento fail-closed ante:

- Redis no disponible;
- MongoDB no disponible;
- configuración JWT inválida;
- secreto JWT ausente cuando JWT se encuentra habilitado.

## HTTP y Problem Details

Se validaron respuestas controladas correspondientes al contrato de Login, incluyendo:

- `200`;
- `400`;
- `401`;
- `413`;
- `415`;
- `429`;
- `503`.

Los errores utilizan Problem Details y `traceId`.

Las respuestas no deben revelar:

- stack trace;
- contraseña;
- hash;
- secreto JWT;
- detalles internos de MongoDB;
- detalles internos de Redis;
- rutas locales.

## Cache y headers

Login utiliza:

- `Cache-Control: no-store`;
- `Pragma: no-cache`.

## OpenAPI

`POST /api/v1/auth/login`

forma parte del contrato OpenAPI autorizado de U2.

No se habilitaron endpoints funcionales adicionales fuera del alcance aprobado.

## Pruebas unitarias

Resultado:

**PASS**

Cubren, entre otros:

- usuario inexistente;
- ejecución de hash dummy;
- contraseña incorrecta;
- credenciales válidas;
- normalización;
- emisión del contrato Login.

## Pruebas de integración

Resultado:

**PASS**

Cubren, entre otros:

- Login válido;
- respuesta JWT;
- credenciales incorrectas;
- usuario inexistente;
- entrada inválida;
- propiedades JSON desconocidas;
- content type inválido;
- rate limit IP + email;
- rate limit IP;
- fallo de Redis;
- fallo de MongoDB;
- headers de caché;
- sanitización de errores;
- OpenAPI.

## Pruebas de seguridad

Resultado:

**PASS**

El workflow Linux confirmó la ejecución satisfactoria del proyecto de pruebas de seguridad.

Entre los controles añadidos para NV-05 están:

- HS256;
- firma válida;
- firma incorrecta;
- `alg=none`;
- expiración;
- claims requeridos;
- `jti`.

## GitHub Actions

Workflow:

`U2 Linux Validation`

Branch:

`feature/u2-entrega`

Run:

`41746514844`

Job:

`126300775630`

Resultado:

**SUCCESS**

Etapas completadas:

- Checkout repository;
- Setup .NET 10;
- Show .NET environment;
- Restore;
- Build;
- Run tests;
- Upload test results.

Artifact:

`u2-test-results`

## Commits técnicos

`8ccfd82` — `test: validate nv05 login on linux`

`923bc9d` — `test: fix nv05 jwt configuration validation`

## Matriz de evidencia

| Requisito | Evidencia | Resultado |
| --- | --- | --- |
| R01 | DTO, endpoint, contrato HTTP, OpenAPI e integración | PASS |
| R02 | LoginService, PBKDF2, dummy verification y pruebas unitarias | PASS WITH WARNING |
| R03 | JwtTokenService y pruebas de seguridad | PASS |
| R04 | Rate limiting IP/email e IP, Redis fail-closed | PASS |
| R05 | Problem Details y pruebas HTTP | PASS |
| R06 | no-store, no-cache, logs sanitizados y JWT externo | PASS |
| R07 | unit, integration, security tests y CI Linux | PASS |

## Advertencias y límites

### W01 — Servicios cloud reales

Los tests de integración utilizan dependencias controladas para verificar la lógica HTTP y fail-closed.

Esta evidencia no declara que se haya validado todavía conectividad real contra:

- MongoDB cloud;
- Redis cloud.

Esa comprobación corresponde a la fase de infraestructura/cloud y NV-03.

### W02 — Timing estadístico

Se encuentra implementado el mecanismo de dummy PBKDF2 requerido para reducir diferencias observables entre usuarios existentes e inexistentes.

No se realizó un benchmark estadístico específico de timing dentro de NV-05.

### W03 — Logout

NV-05 no implementa:

- Logout;
- revocación;
- refresh token;
- blacklist distribuida;
- cierre de sesión.

Ese alcance corresponde a **NV-06**.

## Decisión final

**PASS WITH WARNINGS**

NV-05 cumple el alcance funcional y de seguridad requerido para Login U2 y cuenta con validación automatizada satisfactoria en Linux.

Las advertencias registradas no bloquean el cierre de NV-05 porque corresponden a evidencia de infraestructura posterior o a una validación adicional declarada explícitamente.

**NV-05 queda EJECUTADA, VALIDADA Y CERRADA PARA U2.**
