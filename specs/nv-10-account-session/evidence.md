# NV-10 — Evidencia: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `evidence.md` |
| Versión | 1.2.0 (1.1.0 completa, ver «Historial 1.1.0») |
| Fecha | 2026-11-17 |
| Autorización | 1.2.0: «apruebo y EJECUTA NV-10» — Victor Manuel Jose Jose, 2026-11-17: tasks T01–T09 (M-BE-077), con el contrato §6.1 validado antes por Mobile. 1.1.0: «Apruebo y EJECUTA», 2026-11-06 (M-BE-058) |
| Rama | 1.2.0: `feat/nv-10-account-deletion` → PR #63, merge `a368c0a`. 1.1.0: `docs/nv-10-refresh-me` (PR #10) |
| Resultado | **✅ PASS WITH WARNINGS (1.2.0)** — T01–T09 completas: CI 1010/1010, desplegado en `ca-thehouse-u2--ca368c0a-58-1`, prueba HTTP real en Azure 27/27. 1.1.0: ✅ PASS WITH WARNINGS (13/13 en Azure) |

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
| R06 Contrato para Mobile y despliegue | CD run 44250910491; prueba HTTP real en Azure 27/27; contrato entregado |
| R07 Pruebas, seguridad y evidencia | TRX del CI run 44250483818: 1010/1010; `TheHouse.SecurityTests` 143/143; cobertura en «Despliegue y CI»; esta evidencia |

### 1.2.0 · Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| «Usuario eliminado» | El perfil (NV-11) y el perfil de arrendador (NV-12) quedan solo con ese nombre, sin universidad, teléfono ni RFC; así conversaciones, convenios, reseñas y avisos lo muestran sin cambiar sus contratos |
| Solicitudes recibidas por un arrendador que se elimina | Pasan a `rejected` con motivo `account_deleted` (el estudiante recibe `request_rejected`); las enviadas por un estudiante pasan a `cancelled` (el arrendador recibe `request_cancelled`) |
| Cuartos | No cambian de estado: el edificio `deleted` ya los saca de búsqueda, mapa y detalle (404) |
| URLs legales | Opcionales en configuración (`Legal__TermsUrl`, `Legal__PrivacyUrl`, `Legal__AccountDeletionUrl`); mientras la landing page no las tenga, `GET /legal` las devuelve en `null` |

### 1.2.0 · Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#62](https://github.com/thehouse-team/TheHouse-Backend/pull/62) (borradores 1.2.0) y [#63](https://github.com/thehouse-team/TheHouse-Backend/pull/63) (aprobación e implementación), mezclados por `IsaiAxel` (merge `a368c0a`) |
| CI de `main` (`a368c0a`, run 44250483818) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 1010/1010: unitarias 389, integración 478, seguridad 143 |
| Cobertura de líneas (CI) | 68.8 % total (ramas 72.4 %). `AccountDeletionServices` 94.1 %, `AccountService` 96.9 %, `SessionService` 99.1 %, `AccountEndpoint` 85.7 %, `AccountConfiguration` 81.8 %, `LegalOptions` 100 %. `MongoUserRepository` 2.9 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 44250910491) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--ca368c0a-58-1`, Healthy, 100 % del tráfico |
| Rol admin | Asignado con «autorizo rol» de Victor a la cuenta de prueba `nv10-azure-admin-…@example.com` solo para aprobar el edificio de prueba y revisar su estado; retirado al terminar la corrida (2026-11-18) |

### 1.2.0 · Prueba HTTP real en Azure

`.azure-check/nv10_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-18 con tres cuentas `@example.com` nuevas (estudiante, arrendador y admin temporal); solo se imprimen códigos, campos y conteos, nunca tokens, contraseñas, documentos ni el texto de los mensajes. Resultado: **27/27 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Aviso legal y sesión | `GET /legal` público con `public, max-age=3600` y los campos del §6.1; versión vieja 409 `legal_version_outdated`; aceptar la vigente 200; `/users/me` con `legal` y `legalUpToDate`; refresh 200 | 5/5 |
| Preparación | Crear edificio 201; enviar a revisión 202; el admin aprueba 200 | 3/3 |
| Actividad del estudiante | Favorito 204; solicitud 201; visita 201 | 3/3 |
| Eliminación del estudiante | Vista previa con los campos del §6.1 y 1 favorito, 1 solicitud, 1 visita y 1 conversación; contraseña incorrecta 403 `password_mismatch`; confirmación inválida 400; eliminar 204; después access token, refresh y login 401 | 8/8 |
| Lo que ve el arrendador | Solicitud `cancelled`; conversación con «Usuario eliminado» sin el nombre real; avisos `request_cancelled` y `visit_cancelled` | 3/3 |
| Eliminación del arrendador | Vista previa con 1 edificio y 1 cuarto; eliminar 204; cuarto 404; edificio `deleted`; sesión 401 | 5/5 |

### 1.2.0 · Contrato entregable a Mobile

Entregado el 2026-11-18 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 4 rutas nuevas no cambian ninguna respuesta existente salvo dos campos al final de `/users/me` (`legal` y `legalUpToDate`).

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44250483818): 1010/1010 (unitarias 389, integración 478, seguridad 143); prueba HTTP real en Azure 27/27 (2026-11-18)
```

### 1.2.0 · Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | Las URLs legales (`Legal__TermsUrl`, `Legal__PrivacyUrl`, `Legal__AccountDeletionUrl`) todavía no están configuradas: `GET /legal` las devuelve en `null`. Google Play pide la URL de eliminación de cuenta | Media | Abierto: la landing page debe publicarlas y Victor cargarlas en la Container App (no son secretos) |
| H2 | Adaptadores Mongo sin cobertura en CI | Baja | Aceptado: se verificaron con la prueba real en Azure |
| H3 | Quien fue suspendido puede eliminar su cuenta y registrarse de nuevo con el mismo correo | Baja | Aceptado en el piloto (riesgo de la spec); la cuenta nueva sigue sujeta a reportes y moderación |
| H4 | Las cuentas de prueba del estudiante y del arrendador quedaron eliminadas; solo queda la cuenta admin de prueba (sin rol) y el edificio de prueba en `deleted` | Información | Intencional |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos; una colección pequeña `deleted_accounts` en MongoDB Atlas M0. Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 1.2.0 completa 9/9; las rutas existentes no cambiaron su contrato y los módulos no tienen dependencias circulares (`IAccountDeletionParticipant`).

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
