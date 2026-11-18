# NV-10 — Evidencia: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `evidence.md` |
| Versión | 1.2.0 (1.1.0 completa, ver «Historial 1.1.0») |
| Fecha | 2026-11-17 |
| Autorización | 1.2.0: «apruebo y EJECUTA NV-10» — Victor Manuel Jose Jose, 2026-11-17: tasks T01–T09 (M-BE-077), con el contrato §6.1 validado antes por Mobile. 1.1.0: «Apruebo y EJECUTA», 2026-11-06 (M-BE-058) |
| Rama | 1.2.0: `feat/nv-10-account-deletion` → PR pendiente de revisión. 1.1.0: `docs/nv-10-refresh-me` (PR #10) |
| Resultado | **🟡 EN CURSO (1.2.0)**: T01–T05 completas y verificadas en local; T06–T09 esperan CI, despliegue y prueba HTTP real en Azure. 1.1.0: ✅ PASS WITH WARNINGS (13/13 en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. Ningún token, hash ni `sid` real aparece en esta evidencia.

## 1.2.0 — Eliminar cuenta, sesión siempre viva y aviso de privacidad

### 1.2.0 · Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/User.cs`, `Entities/Listings.cs` | `LegalAcceptance` en la cuenta; `BuildingStatus.Deleted` (nunca vuelve a publicarse) |
| Application | `Abstractions/Identity/IAccountDeletionParticipant.cs` (nuevo) | Puerto que cada módulo implementa (`CheckAsync` bloqueos y efectos; `EraseAsync` idempotente); Identity no depende de los demás módulos |
| Application | `Abstractions/Persistence/IUserAccountRepository.cs` (nuevo) | Aceptación legal y eliminación con marca en `deleted_accounts`, separado de `IUserRepository` |
| Application | `IProfileRepository`, `IListingRepositories`, `INotificationRepository`, `IRoomViewRepository`, `IAgreementRepository` | `AnonymizeAsync` (perfil y arrendador), `DeleteAsync` (preferencias), `DeleteAllAsync` (bandeja), `DeleteByOwnerAsync` (vistas), `ListOpenForUserAsync` (convenios) |
| Application | `UseCases/AccountDeletionServices.cs` (nuevo) | `AccountDeletionService` (vista previa, contraseña, `ELIMINAR`, bloqueos, módulos, sesiones y cuenta al final) y 11 participantes: Profile, Listings+Media, Favorites, Messaging, Visits, Rentals, Reviews, Notifications, Moderation, Roommates y Analytics |
| Application | `UseCases/AccountService.cs`, `Common/LegalOptions.cs`, `Contracts/Users/*` | Versiones legales, aceptación y `/users/me` con `legal` y `legalUpToDate` |
| Application | `UseCases/SessionService.cs`, `Common/RefreshTokenOptions.cs` | Sesión deslizante: cada refresh extiende 30 días; sin tope absoluto de 90 |
| Application | `UseCases/MessagingServices.cs` | `FirstName` conserva «Usuario eliminado» completo |
| Infrastructure | `MongoUserRepository`, `UserDocument`, `MongoProfileRepositories`, `MongoListingRepositories`, `MongoNotificationRepository`, `MongoRoomViewRepository`, `MongoAgreementRepository` y sus `Unavailable*` | Implementaciones de los métodos nuevos; `deleted_accounts` (solo id y fecha) |
| API | `Http/AccountEndpoint.cs`, `Configuration/AccountConfiguration.cs` (nuevos), `Program.cs`, `ProblemResponses.cs`, `UserResourceProblems.cs`, `RateLimitKeys.cs`, `UserRateLimitOptions.cs` | 4 rutas; códigos en español; `blockers` en el 409; 5 intentos por hora, 30/min y 60/min por IP en el aviso público |
| Pruebas | `UnitTests/AccountDeletionTests.cs`, `IntegrationTests/AccountDeletionHttpTests.cs`, `SecurityTests/AccountDeletionSecurityTests.cs`; ajustes en `SessionServiceTests`, `SessionHttpTests`, `OpenApiContractTests`, `LogoutHttpTests` y dobles en memoria | 10 unitarias nuevas (más la sesión deslizante), 6 de integración y 13 de seguridad |

### 1.2.0 · Comandos y resultados (local, 2026-11-17)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx --configuration Release` (dos corridas seguidas, como el CI) | 1010/1010 en ambas corridas: unitarias 389, seguridad 143, integración 478; 0 fallas |

### 1.2.0 · Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Vista previa y bloqueos | `Student_deletion_erases_cancels_anonymizes_and_closes_sessions` (campos y conteos), `Landlord_deletion_retires_buildings_and_cancels_visits_of_students`, `Active_rental_or_staff_role_block_deletion` |
| R02 Eliminación de cuenta | `Student_deletion_...` (favoritos, perfil, bandeja, solicitudes, conversaciones y visitas; avisos al arrendador; «Usuario eliminado» en su bandeja; 401 en `/users/me`, refresh y login), `Landlord_deletion_...` (edificio `deleted`, detalle 404, arrendador anonimizado, vistas borradas, aviso al estudiante), `Failure_midway_keeps_the_session_and_retry_completes`, unitarias de cada participante (idempotentes) |
| R03 Sesión siempre viva | `Session_stays_alive_while_used_and_expires_after_idle_window` (13 refresh en un año; vence tras 30 días sin uso), `Session_has_no_absolute_cap_but_expires_when_idle` |
| R04 Aviso de privacidad y términos | `Legal_versions_are_public_and_acceptance_is_recorded`, `Legal_options_are_validated`, campos de `/users/me` en `SessionHttpTests` |
| R05 Seguridad, privacidad y límites | 403 `password_mismatch`, 400 por campos desconocidos y confirmación inválida, `Deletion_attempts_are_limited_and_anonymous_is_rejected` (429 al sexto intento, 401 anónimo), `Account_responses_never_carry_secrets`, `Request_bodies_reject_unknown_fields`, `Codes_are_explained_in_spanish` |
| R06 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R07 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

### 1.2.0 · Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| «Usuario eliminado» | El perfil (NV-11) y el perfil de arrendador (NV-12) quedan solo con ese nombre, sin universidad, teléfono ni RFC; así conversaciones, convenios, reseñas y avisos lo muestran sin cambiar sus contratos |
| Solicitudes recibidas por un arrendador que se elimina | Pasan a `rejected` con motivo `account_deleted` (el estudiante recibe `request_rejected`); las enviadas por un estudiante pasan a `cancelled` (el arrendador recibe `request_cancelled`) |
| Cuartos | No cambian de estado: el edificio `deleted` ya los saca de búsqueda, mapa y detalle (404) |
| URLs legales | Opcionales en configuración (`Legal__TermsUrl`, `Legal__PrivacyUrl`, `Legal__AccountDeletionUrl`); mientras la landing page no las tenga, `GET /legal` las devuelve en `null` |

### 1.2.0 · Pendiente para cerrar

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| URLs de la landing page en la Container App (`Legal__*`, no son secretos) | Victor / Mobile | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv10_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |

# Historial 1.1.0

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
