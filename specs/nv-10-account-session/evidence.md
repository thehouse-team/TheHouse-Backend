# NV-10 — Evidencia: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-06: NV-10 1.1.0, tasks T01–T11, rutas de los siete proyectos y `specs/nv-10-account-session/` (M-BE-058) |
| Rama | `docs/nv-10-refresh-me` (PR #10) |
| Resultado | **🟡 PASS WITH WARNINGS (local)**: T01–T10 completas con pruebas; T11 abierta hasta la prueba HTTP real en Azure después del despliegue por el CD |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. Ningún token, hash ni `sid` real aparece en esta evidencia.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/RefreshToken.cs` | Entidad con hash, sesión, vencimientos, rotación y revocación; reglas `IsActive` e `IsReuse` |
| Application | `Abstractions/Security/IRefreshTokenStore.cs`, `ISessionRevocationStore.cs` | Puertos de persistencia (rotación atómica) y revocación de sesión |
| Application | `Abstractions/Security/IJwtTokenService.cs` | `CreateAccessToken(User, sessionId)` |
| Application | `Abstractions/Persistence/IUserRepository.cs` | `GetByIdAsync` |
| Application | `Common/SessionTokens.cs`, `RefreshTokenOptions.cs`, `RateLimitKeys.cs` | Generación (32 bytes, base64url), hash SHA-256, formato de `sid`, vigencias 30/90 días, rate limit de refresh |
| Application | `UseCases/SessionService.cs` | Inicio, refresh con rotación, detección de reuso y revocación de sesión |
| Application | `UseCases/LoginService.cs` | El login inicia una sesión; sin cambios en validaciones ni errores |
| Application | `UseCases/LogoutService.cs` | Revoca también la sesión (`sid`); rechaza tokens de sesiones revocadas; `ValidateActiveAsync` |
| Application | `UseCases/AccountService.cs`, `Contracts/Users/MeResponse.cs` | `/users/me` con `id`, `email`, `createdAt` |
| Application | `Contracts/Auth/LoginResponse.cs`, `RefreshRequest.cs` | `refreshToken` aditivo; entrada del refresh con campos desconocidos rechazados |
| Infrastructure | `Security/JwtTokenService.cs` | Claim `sid` (canónico); la validación no lo exige |
| Infrastructure | `Security/RedisTokenRevocationStore.cs` | `ISessionRevocationStore` con `SET NX` + TTL y fallo cerrado |
| Infrastructure | `Persistence/Mongo/MongoRefreshTokenStore.cs` (+ documento) | Colección `refresh_tokens`: índice único de hash, índice de sesión, TTL; rotación con `FindOneAndUpdate` condicional |
| Infrastructure | `Persistence/Mongo/MongoUserRepository.cs` | Búsqueda por id (id inválido → null) |
| API | `Http/RefreshEndpoint.cs`, `UsersMeEndpoint.cs`, `SessionProblems.cs` | Endpoints nuevos y Problem Details en español |
| API | `Http/TokenRevocationMiddleware.cs` | Deja el principal validado en `HttpContext.User` |
| API | `Configuration/SessionConfiguration.cs`, `LogoutConfiguration.cs`, `Program.cs` | Registro de servicios; una sola instancia Redis para `jti` y `sid` |

`TheHouse/Http/RegistrationEndpoint.cs`, `RegisterRequest` y `RegistrationService` no se modificaron.

## 2. Contratos verificados

| Contrato | Resultado |
| --- | --- |
| `POST /auth/register` | Sin cambios (`email`, `password` → 201); pruebas de `RegistrationHttpTests` en verde |
| `POST /auth/login` | 200 con `accessToken`, `tokenType`, `expiresIn` y `refreshToken` en ese orden (`LoginHttpTests`) |
| `POST /auth/logout` | 204 sin cambios; además revoca la sesión (`LogoutHttpTests`, `SessionHttpTests`) |
| `POST /auth/refresh` | 200 con la misma forma que el login; 400, 401 uniforme, 413, 415, 429 con `Retry-After: 60`, 503 |
| `GET /users/me` | 200 con exactamente `id`, `email`, `createdAt`; `Cache-Control: no-store`; 401 sin token, con token inválido, revocado o de usuario inexistente |
| OpenAPI | Agrega solo `/api/v1/auth/refresh` y `/api/v1/users/me` (pruebas de rutas autorizadas actualizadas) |

## 3. Validación local (2026-11-06)

```text
dotnet build TheHouse.slnx -c Release -warnaserror   → 0 advertencias, 0 errores
dotnet test TheHouse.slnx -c Release
  TheHouse.UnitTests         157/157
  TheHouse.SecurityTests      52/52
  TheHouse.IntegrationTests  271/271
  Total                      480/480 (antes de NV-10: 429)
```

Pruebas nuevas (51):

| Suite | Archivo | Casos |
| --- | --- | --- |
| Unit | `SessionServiceTests.cs` | Emisión con hash y `sid`; rotación; reuso revoca la sesión; concurrencia (≤ 1 emisión de 8 intentos); vencimiento de 30 días; límite absoluto de 90 días; 401 uniforme para desconocido y mal formado; 400 sin token; usuario eliminado; sesión revocada; fallo cerrado de MongoDB y Redis; formato de valores; validación de opciones; `/users/me` con campos reales |
| Unit | `LogoutSessionTests.cs` | Logout con y sin `sid`; rechazo de sesión revocada; principal validado; `sid` mal formado; fallo cerrado |
| Integración | `SessionHttpTests.cs` | Forma y rotación del refresh; reuso que invalida refresh y access tokens; `/users/me` (campos, 401, usuario eliminado); logout 204 que revoca la sesión; **revocación vista desde otra réplica**; validación de cuerpo (400/401/413/415); rate limit 429; Redis caído 503; login sin almacenamiento 503 sin tokens; refresh concurrente; ningún token, hash ni `sid` en logs |
| Seguridad | `JwtSecurityTests.cs` | `sid` presente y validado; `sid` no canónico rechazado; token anterior a NV-10 sin `sid` sigue válido |

## 4. Revisión de seguridad (AGENTS)

| Control | Resultado |
| --- | --- |
| Secretos | El refresh token solo se guarda como SHA-256; nunca en logs (prueba); generación con `RandomNumberGenerator` |
| Autenticación | Refresh con 401 uniforme; reuso revoca la sesión entera; vencimientos 30/90 días |
| Autorización por recurso | `/users/me` solo usa el `sub` del token validado; no acepta identificadores del cliente |
| Concurrencia | Rotación condicional atómica (MongoDB `FindOneAndUpdate`); prueba unitaria y HTTP |
| Stateless / réplicas | Revocación de sesión en Redis compartido; prueba con dos fábricas que comparten almacenes |
| Fallo cerrado | MongoDB o Redis caídos → 503 sin emitir tokens |
| Validación de entrada | Content type, tamaño (1 KB), JSON estricto sin campos desconocidos, formato del token |
| Rate limiting | 30/60 s por IP en Redis; 429 con `Retry-After` |
| Exposición de datos | `/users/me` sin `PasswordHash` ni `UpdatedAt`; Problem Details sin detalles internos |
| Compatibilidad | Cambios solo aditivos; tokens previos sin `sid` siguen funcionando |

Hallazgos: ninguno Critical, High ni Medium. Advertencia (Accepted Risk, decisión documentada en la spec): un reintento simultáneo del mismo refresh token se trata como reuso y obliga a iniciar sesión; Mobile debe serializar las renovaciones.

## 5. Pendiente para cerrar T11

1. CI del PR #10 en verde y merge.
2. Despliegue por el CD con aprobación en `production`.
3. Prueba HTTP real en Azure: login → `/users/me` → refresh → reuso (401) → logout → refresh (401).
4. Avisar a Mobile: "NV-10 desplegada".
