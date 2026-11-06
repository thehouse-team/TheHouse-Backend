# NV-10 — Evidencia: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-06: NV-10 1.1.0, tasks T01–T11, rutas de los siete proyectos y `specs/nv-10-account-session/` (M-BE-058) |
| Rama | `docs/nv-10-refresh-me` (PR #10, mezclado por IsaiAxel el 2026-11-06) |
| Resultado | **✅ PASS WITH WARNINGS**: 11/11 tasks completas; desplegada en Azure y verificada con prueba HTTP real (13/13) |

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

## 5. Despliegue y prueba HTTP real en Azure (T11)

- CI del PR #10: run `42191624040`, los 7 checks obligatorios en verde.
- PR #10 aprobado y mezclado por `IsaiAxel` (commit `55feed2`).
- CD: run `42192269362` (https://github.com/thehouse-team/TheHouse-Backend/actions/runs/42192269362), aprobado por `vicmanujo`; `publish`, `verify-ephemeral`, `deploy`, `dast-baseline` y `cd-summary` en verde.
- Azure: revisión `ca-thehouse-u2--c55feed2-7-1`, imagen `sha256:4af4456d…`.

Prueba HTTP real contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-06), con una cuenta de prueba nueva de dominio reservado (`nv10-azure-…@example.com`). El script solo registró códigos y nombres de campos, nunca tokens:

| Paso | Esperado | Resultado |
| --- | --- | --- |
| 1. `POST /auth/register` (sin cambios) | 201 | ✅ 201 |
| 2. `POST /auth/login` | 200 con `accessToken`, `tokenType`, `expiresIn`, `refreshToken` | ✅ 200, campos exactos |
| 3. `GET /users/me` | 200 con `id`, `email`, `createdAt` | ✅ 200, campos exactos, email correcto |
| 4. `POST /auth/refresh` | 200 con token rotado | ✅ 200, token distinto |
| 5. `/users/me` con el access token renovado | 200 | ✅ 200 |
| 6. Reuso del refresh token viejo | 401 | ✅ 401 |
| 7. Access token de la sesión revocada por reuso | 401 | ✅ 401 |
| 8. Refresh token nuevo de esa sesión | 401 | ✅ 401 |
| 9. Login de una sesión nueva | 200 | ✅ 200 |
| 10. `POST /auth/logout` (sin cambios) | 204 | ✅ 204 |
| 11. Refresh después del logout | 401 | ✅ 401 |
| 12. `/users/me` después del logout | 401 | ✅ 401 |
| 13. `/users/me` sin token | 401 | ✅ 401 |

**13/13 PASS.** Encabezados verificados con una segunda cuenta de prueba: `cache-control: no-store` en login, `/users/me` (200 y 401) y refresh (401), y `pragma: no-cache` en `/users/me`; esa sesión se cerró con logout (204).

Quedan en producción dos cuentas de prueba con prefijo `nv10-azure-` y dominio `example.com`, sin datos personales; se eliminarán cuando exista la eliminación de cuenta (versión posterior de NV-10).

## 6. Resultado final

| Revisión | Resultado |
| --- | --- |
| SDD (spec → plan → task → evidencia, constitution 1.1.0, AGENTS 1.1.0) | PASS |
| Seguridad | PASS WITH WARNINGS — un refresh simultáneo con el mismo token se trata como reuso (Accepted Risk documentado; la app serializa las renovaciones) |
| Contratos existentes | Sin cambios incompatibles: register 201, login 200 (campo aditivo), logout 204 |

Mobile puede conectar `POST /auth/refresh` y `GET /users/me` ("NV-10 desplegada").
