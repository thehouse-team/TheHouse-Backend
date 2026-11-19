# NV-21 — Evidencia: verificación, reportes y moderación

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `evidence.md` |
| Versión | 1.2.0 (1.1.0 completa, ver «Historial 1.1.0») |
| Fecha | 2026-11-19 |
| Autorización | 1.2.0: «Ya lo validó, apruebo y EJECUTA NV-21» — Victor Manuel Jose Jose, 2026-11-19: tasks T01–T10 (M-BE-079), con el contrato §6.1 validado antes por Mobile; el `terraform apply` requiere «OK PARA APPLY». 1.1.0: «Apruebo y EJECUTA», 2026-11-07 (M-BE-065) |
| Rama | 1.2.0: `feat/nv-21-content-moderation` (PR #67), `fix/nv-21-moderation-tuning` (PR #68) y `fix/nv-21-threat-phrases` (PR #69); merge final `ea10a4c`. 1.1.0: `feat/nv-21-moderation` (PR #24) |
| Resultado | **✅ PASS WITH WARNINGS (1.2.0)** — T01–T10 completas: CI 1058/1058, desplegado en `ca-thehouse-u2--cea10a4c-64-1` con Azure AI Content Safety, prueba HTTP real en Azure 10/10 (tercera corrida). 1.1.0: ✅ PASS WITH WARNINGS (23/23 en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. Motivos y detalles de reportes, teléfonos y correos no aparecen en logs.

## 1.2.0 — Moderación automática antes de guardar

### 1.2.0 · Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Moderation.cs` | `ModerationRejection` (quién, dónde, categorías y severidad; **sin el contenido**) |
| Application | `Abstractions/Moderation/IContentModeration.cs` (nuevo) | Puerto compartido `IContentModerator` (como `INotificationSink`, sin dependencias entre módulos), `IContentSafetyClient`, `IModerationRejectionRepository`, `ContentRejectedException` (422) y `ModerationUnavailableException` (503) |
| Application | `UseCases/ContentModerationServices.cs` (nuevo) | `ContentModerationService` (lista de términos sin acentos ni mayúsculas, umbral 4, registro del rechazo, falla cerrada) y `ModerationRejectionService` (vista de revisor o admin) |
| Application | `ProfileService`, `LandlordService`, `BuildingService`, `MessagingServices` (solicitud, nota de rechazo y chat), `VisitService`, `LandlordVisitService` (notas), `ReviewServices`, `RoommateServices` (perfil y contacto), `ModerationServices` (detalles de reportes), `MediaServices` (fotos) | Revisión después de validar y antes de guardar; parámetro opcional `IContentModerator` (sin cambiar las demás dependencias) |
| Infrastructure | `Moderation/AzureContentSafetyClient.cs` (nuevo) | REST GA `2023-10-01` (`text:analyze` e `image:analyze`); token de la identidad administrada de Container Apps (`IDENTITY_ENDPOINT`/`IDENTITY_HEADER`, `client_id` de la identidad de la app); 3 s y un reintento en 5xx; 429 (cupo F0) → 503 sin reintentar; `DisabledContentSafetyClient` sin el recurso |
| Infrastructure | `Persistence/Mongo/MongoModerationRejectionRepository.cs` (nuevo) | `moderation_rejections` con `ix_moderation_rejections_user_created` y `ttl_moderation_rejections_created` (180 días) |
| API | `Configuration/ContentModerationConfiguration.cs` (nuevo), `Program.cs`, `appsettings.json`, `UserEndpointSupport.cs`, `UserResourceProblems.cs`, `AdminEndpoint.cs` | `ContentSafety__Endpoint` (solo `https://*.cognitiveservices.azure.com`), `ContentSafety__IdentityClientId`, umbral y lista corta de términos; 422 `content_rejected` con `field` y `categories`; 503 `moderation_unavailable` con `Retry-After: 30`; `GET /api/v1/admin/moderation/rejections` |
| Infra | `infra/terraform/content_safety.tf` (nuevo), `main.tf`, `variables.tf`, `outputs.tf` | `azurerm_cognitive_account` `ContentSafety` F0 con `local_auth_enabled = false`; identidad asignada por el usuario `id-thehouse-u2-app` en la Container App; rol `Cognitive Services User` solo sobre el recurso; salidas `content_safety_endpoint` y `content_safety_identity_client_id` |
| Pruebas | `UnitTests/ContentModerationTests.cs`, `IntegrationTests/ContentModerationHttpTests.cs`, `IntegrationTests/ContentSafetyClientTests.cs`, `SecurityTests/ContentModerationSecurityTests.cs`, `IntegrationTests/ContentModerationTestDoubles.cs`; listas OpenAPI | 7 unitarias, 11 de integración y 6 de seguridad |

### 1.2.0 · Comandos y resultados (local, 2026-11-19)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx --configuration Release` (dos corridas seguidas, como el CI) | 1051/1051 en la primera corrida (unitarias 402, seguridad 154, integración 495); en la segunda falló 1 prueba ajena a NV-21 (HealthAndRateLimitTests de NV-01, la misma de tiempo que en NV-20); las pruebas de NV-21 pasaron en las dos corridas |
| `terraform fmt -check` y `terraform validate` | Sin cambios de formato; configuración válida |
| `terraform plan` (state local, sin aplicar) | **Plan: 3 to add, 1 to change, 0 to destroy**: `azurerm_cognitive_account.content_safety` (`ContentSafety`, `F0`, `local_auth_enabled = false`, subdominio `cs-thehouse-u2`), `azurerm_user_assigned_identity.app`, `azurerm_role_assignment.app_content_safety` (`Cognitive Services User`) y `azurerm_container_app.this` en sitio (solo el bloque `identity`; imagen, secretos y variables en `ignore_changes`) |
| `az cognitiveservices account list-skus --kind ContentSafety --location eastus2` | F0 y S0 disponibles en la región del backend |
| `az provider register --namespace Microsoft.CognitiveServices --wait` (autorizado por Victor, 2026-11-19) | `Registered` (sin costo ni recursos) |
| `terraform apply` del plan guardado («OK PARA APPLY», Victor, 2026-11-19) | **Apply complete! Resources: 3 added, 1 changed, 0 destroyed** |
| Verificación posterior con `az` | `cs-thehouse-u2`: `ContentSafety`, `F0`, `eastus2`, `Succeeded`, `disableLocalAuth = true`; cupo F0 **30 000 llamadas cada 30 días** (`list-usage`, sin cobro); única asignación sobre el recurso: `Cognitive Services User` de `id-thehouse-u2-app`; Container App con identidad `UserAssigned` y la misma imagen, revisión nueva `ca-thehouse-u2--0000011` Healthy al 100 %; `terraform plan` posterior sin cambios en recursos (solo el output `container_app_fqdn`, H-04 de NV-09) |

### 1.2.0 · Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Cliente de Content Safety | `Client_sends_text_and_images_with_the_managed_identity_token`, `Quota_errors_and_missing_identity_fail_closed_and_server_errors_retry_once`, `Only_azure_ai_endpoints_are_accepted` |
| R02 Moderación de texto antes de guardar | `Offensive_text_is_rejected_before_saving_in_every_module` (perfil, edificio, reporte, solicitud y chat: 422 y nada guardado), `Blocklisted_words_are_rejected_even_when_the_cloud_says_it_is_clean`, `Severity_four_or_more_is_rejected` (0 y 2 pasan; 4 y 6 se rechazan), `Blocklist_matches_whole_words_without_accents_or_case` |
| R03 Moderación de imágenes antes de guardar | `Photos_are_reviewed_before_saving_but_documents_never_leave` (foto de cuarto y de edificio 422 sin guardar; la INE no llama a la nube; foto limpia 201) |
| R04 Registro de rechazos para el administrador | `Staff_see_rejections_without_content_and_others_cannot` (campos del §6.1 sin contenido, filtro inválido 400, estudiante 403), `Unavailable_cloud_fails_closed_and_a_failed_log_still_rejects` |
| R05 Recurso de Azure con identidad administrada | `terraform plan` revisado, apply con «OK PARA APPLY» y verificación con `az` (arriba); costo $0 (F0 se detiene al llegar al cupo) |
| R06 Seguridad, privacidad y límites | `Without_the_review_nothing_is_saved` (503 `moderation_unavailable` con `Retry-After`), `Rejections_never_carry_the_content`, `Rejected_exception_carries_only_field_and_categories`, `Codes_are_explained_in_spanish`, `Cloud_access_uses_the_managed_identity_and_the_ga_api` |
| R07 Contrato para Mobile y despliegue | CD run 44478006535; prueba HTTP real en Azure 10/10; contrato entregado |
| R08 Pruebas, seguridad y evidencia | TRX del CI run 44476873486: 1058/1058; `TheHouse.SecurityTests` 154/154; cobertura en «Despliegue y CI»; esta evidencia |

### 1.2.0 · Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Identidad | Asignada **por el usuario** (`id-thehouse-u2-app`) en lugar de la del sistema: con la del sistema Terraform no puede planear el rol en el mismo `apply` (su `principal_id` no existe todavía). Es exclusiva de la app y distinta de la identidad del CD |
| Despliegue seguro | Sin `ContentSafety__Endpoint` solo aplica la lista de términos (no llama a la nube): el código puede desplegarse antes del apply sin bloquear escrituras; la revisión en la nube se activa al cargar `ContentSafety__Endpoint` y `ContentSafety__IdentityClientId` (no son secretos) |
| Lista de términos | 12 términos en `appsettings.json` (`ContentSafety:Blocklist`), comparados por palabra completa sin acentos ni mayúsculas |
| Textos revisados | Perfil, nombre del arrendador, nombre del edificio, solicitud, nota de rechazo, chat (incluido el de compañero), mensaje y notas de visita, comentario de reseña, perfil y contacto de compañero, y detalles de reportes. Las direcciones y los datos estructurados (precios, tipos) no se envían |
| Rol para la vista | `reviewer` o `admin` (`admin` incluye a `reviewer`, NV-21 1.1.0) |

### 1.2.0 · Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#67](https://github.com/thehouse-team/TheHouse-Backend/pull/67) (implementación, merge `d08f8b1`), [#68](https://github.com/thehouse-team/TheHouse-Backend/pull/68) (token, plurales y umbral, `e1259c0`) y [#69](https://github.com/thehouse-team/TheHouse-Backend/pull/69) (frases de amenaza tras la calibración, `ea10a4c`), mezclados por `IsaiAxel` |
| CI de `main` (`ea10a4c`, run 44476873486) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 1058/1058: unitarias 408, integración 496, seguridad 154 |
| Cobertura de líneas (CI) | 68.9 % total (ramas 72.8 %). `ContentModerationServices` 97.6 %, `AzureContentSafetyClient` 84.9 %, `ContentModerationConfiguration` 48.1 % (la rama con el recurso real solo corre en Azure), `MongoModerationRejectionRepository` 0 %, como los demás adaptadores Mongo |
| CD (run 44478006535) | Todo en verde; el paso de build y push tardó 18 min en el runner de GitHub (normalmente ~1 min), sin errores |
| Azure | revisión `ca-thehouse-u2--cea10a4c-64-1`, Healthy, 100 % del tráfico; `ContentSafety__Endpoint` y `ContentSafety__IdentityClientId` cargadas por Victor (no son secretos) |

### 1.2.0 · Prueba HTTP real en Azure

`.azure-check/nv21_azure_check.py` (fuera del repositorio), con dos cuentas `@example.com` nuevas en cada corrida, que el propio script elimina al final (NV-10). Solo imprime códigos, campos y categorías.

| Corrida | Resultado | Hallazgo y corrección |
| --- | --- | --- |
| 1 (tras #67) | 5/9 | T1 503: el token de la identidad se pedía dentro de los 3 s del análisis (en frío tarda más) → límite propio de 10 s. F3: el plural «pendejos» pasaba → la lista acepta `-s`/`-es`. T3: una amenaza se guardó → umbral 2 en violencia y odio (decisión de Victor) |
| 2 (tras #68) | 7/9 | T3 seguía pasando con el umbral en 2 y la configuración leída bien (prueba nueva). La calibración con un rol temporal mostró **Violence=0 para amenazas directas en español y en inglés**, mientras odio (**Hate=4**) y sexual (**Sexual=6**) sí se detectan → la lista acepta frases y agrega amenazas comunes |
| 3 (tras #69) | **10/10 PASS** | Texto limpio 200 (la nube respondió); término de la lista 422 `blocklist`; odio 422 `hate` (Content Safety); amenaza 422 `blocklist` (frase); el perfil conserva el nombre limpio; edificio limpio 201; foto limpia 200 (imagen revisada en la nube); nombre de edificio con término 422; cuentas eliminadas 204 |

### 1.2.0 · Contrato entregable a Mobile

Entregado el 2026-11-19 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las respuestas de éxito no cambian y las rutas que guardan texto o fotos agregan 422 `content_rejected` (con `field` y `categories`) y 503 `moderation_unavailable` (con `Retry-After`).

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44476873486): 1058/1058 (unitarias 408, integración 496, seguridad 154); prueba HTTP real en Azure 10/10 con Azure AI Content Safety (2026-11-19)
```

### 1.2.0 · Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | Azure AI Content Safety no califica las amenazas directas (Violence=0 en español e inglés) | Media | Mitigado: frases de amenaza en la lista de términos; la lista es corta y no cubre todas las variantes |
| H2 | La lista de términos se mantiene a mano en `appsettings.json` | Baja | Aceptado; se amplía con los reportes (NV-21 1.1.0) y los rechazos registrados |
| H3 | El registro de rechazos (`moderation_rejections`) y su adaptador Mongo no tienen cobertura en CI | Baja | Aceptado: la ruta de administración se probó por HTTP con el doble en memoria |
| H4 | El build de la imagen tardó 18 min una vez en el runner de GitHub | Información | Observación; si se repite, se revisa la caché del build |

Resultado: **PASS WITH WARNINGS**. Costo: $0 (Content Safety F0, cupo de 30 000 llamadas cada 30 días que se detiene sin cobrar; identidad y rol sin costo; colección pequeña con TTL). Revisión de coherencia: spec, plan, tasks, README, `memoria.md` (M-BE-079 con su actualización y la calibración) y esta evidencia indican 1.2.0 completa 10/10.

# Historial 1.1.0

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Listings.cs` | Estados `rejected` y `suspended`; `Approve`, `RequestChanges`, `Reject`, `Suspend`, `Unsuspend`; `EnsureEditable` (`building_in_review` o `building_state`) |
| Domain | `Entities/Moderation.cs` | `StaffRoles` (`admin` incluye `reviewer`), `VerificationReview`, `Report`, `UserSuspension`, `AuditEvent` |
| Application | `Abstractions/Persistence/IModerationRepositories.cs`, `Security/IAccountSuspensionQuery.cs` | Puertos de moderación; la bitácora no tiene métodos de cambio ni borrado |
| Application | `Abstractions/Security/IRefreshTokenStore.cs` | `ListActiveSessionIdsAsync` para cortar todas las sesiones de una cuenta suspendida |
| Application | `UseCases/ModerationServices.cs` | `StaffService` (roles y bitácora), `ReviewService`, `ModerationService`, `ReportService`, `AccountSuspensionQuery` |
| Application | `UseCases/SessionService.cs` | Login (tras validar la contraseña) y refresh rechazan cuentas suspendidas con 403 `account_suspended`; el refresh revoca además esa sesión |
| Application | `UseCases/BuildingService.cs`, `RoomService.cs` | Usan `EnsureEditable`; un edificio `rejected` se puede borrar pero no editar |
| Infrastructure | `Persistence/Mongo/MongoModerationRepositories.cs` | `staff_roles`, `verification_reviews`, `reports` (único parcial de reporte abierto), `user_suspensions`, `audit_events` (solo inserción) |
| Infrastructure | `MongoListingRepositories.cs`, `MongoRefreshTokenStore.cs` | Cola por estado; sesiones activas por usuario |
| API | `Http/AdminEndpoint.cs` | 12 rutas `/admin/*` y `POST /reports` |
| API | `Http/LandlordEndpoint.cs` | `GET /landlord/buildings/{id}/verification` |
| API | `Http/LoginEndpoint.cs`, `RefreshEndpoint.cs`, `ProblemResponses.cs`, `UserResourceProblems.cs` | 403 `account_suspended`; `code` aplicado en cualquier ruta que lo indique |
| API | `Configuration/RoleCommand.cs`, `ModerationConfiguration.cs`, `Program.cs` | `--grant-role`/`--revoke-role` con `--allow-production-role-change`; registro de servicios |

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas) | 696/696: unitarias 243, seguridad 54, integración 399 |

Pruebas nuevas: `TheHouse.UnitTests/ModerationTests.cs` (13), `TheHouse.IntegrationTests/AdminHttpTests.cs` (10) y `TheHouse.SecurityTests/RoleCommandTests.cs` (2). Los dobles de moderación se registran en `FoundationFactory` porque el login ahora consulta suspensiones (sin ellos, el login falla cerrado con 503, que es el comportamiento correcto sin MongoDB).

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Admin_routes_require_the_right_role`, `Staff_service_requires_roles_and_audits_grants`, `Admin_role_includes_reviewer`, `Parse_reads_grant_and_revoke_with_email_and_role`, `Production_requires_the_explicit_flag` |
| R02 | `Approving_a_building_verifies_it_and_makes_rooms_available` (cola y detalle sin teléfono) |
| R03 | `Approving_a_building_verifies_it_and_makes_rooms_available`, `Request_changes_and_reject_require_a_reason_and_move_states`, `Reviewers_cannot_decide_on_their_own_buildings`, `Review_decisions_only_apply_to_buildings_in_review` |
| R04 | `Approving_a_building_verifies_it_and_makes_rooms_available` y `Request_changes_and_reject_require_a_reason_and_move_states` (verificación del dueño sin revisor) |
| R05 | `Reports_are_validated_deduplicated_and_resolved` |
| R06 | `Reports_are_validated_deduplicated_and_resolved`, `Suspending_a_user_blocks_login_refresh_and_current_sessions` |
| R07 | `Buildings_can_be_suspended_only_when_verified_and_admins_cannot_be_suspended`, `Suspension_is_only_between_verified_and_suspended` |
| R08 | `Suspending_a_user_blocks_login_refresh_and_current_sessions`, `Refresh_of_a_suspended_account_returns_403` |
| R09 | `Audit_log_is_listed_and_cannot_be_modified` (405 en `POST`/`DELETE`) |
| R10 | `Admin_routes_require_the_right_role`, `Admin_limits_and_dependencies_fail_closed`, `Owner_edits_depend_on_the_building_state`, `Reasons_have_5_to_500_characters`, prueba de logs en `Reports_are_validated_deduplicated_and_resolved` |

## 4. Ajuste respecto a la spec

La spec dice que, al suspender una cuenta, el access token vigente expira en 15 minutos como máximo. La implementación hace más: revoca en Redis cada sesión activa de la cuenta (`sid`), así que sus access tokens dejan de funcionar de inmediato. La prueba `Suspending_a_user_blocks_login_refresh_and_current_sessions` lo verifica (`/users/me` → 401 justo después de suspender).

## 5. Seguridad y coherencia

- Roles leídos de `staff_roles` en cada solicitud; sin rol → 403 `role_required`; un revisor no decide sobre sus propios edificios (403 `own_building`).
- Los roles no se asignan por la API: solo por `--grant-role` desde el contenedor, con bandera explícita en Production y registro en la bitácora (actor `cli`, sin correo en logs).
- El detalle de revisión no expone teléfono ni correo; la verificación del dueño no revela al revisor.
- Login revisa la suspensión solo después de validar la contraseña (no revela el estado de la cuenta a terceros).
- La bitácora no tiene métodos de actualización ni borrado; las rutas no admiten `POST` ni `DELETE`.
- Rate limit y repositorios fallan cerrados con 503.
- Costo: sin servicios nuevos; cinco colecciones pequeñas en Atlas M0.

## 6. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#23](https://github.com/thehouse-team/TheHouse-Backend/pull/23) (spec y aprobación) y [#24](https://github.com/thehouse-team/TheHouse-Backend/pull/24) (código), mezclados el 2026-11-07 |
| CI de `main` (run 42403322329, `7310ab0`) | 7/7 checks en verde; 696/696 pruebas (unitarias 243, integración 399, seguridad 54); 25 pruebas de NV-21 |
| Cobertura de líneas (CI) | 68.5 % total. `MongoModerationRepositories` en 0 %, como los demás adaptadores Mongo: verificado con la prueba real en Azure |
| CD (run 42403433110) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c7310ab0-22-1`, imagen `sha256:f37ff11f…` |

Una primera corrida del script expiró (timeout de 30 s en el registro) porque coincidió con el cambio de revisión del CD (revisión creada 18:55:58 UTC, despliegue terminado 18:57:00 UTC); al terminar, la API respondía en 0.25 s y la segunda corrida funcionó.

## 7. Roles en Production y prueba HTTP real

Autorizado por Victor el 2026-11-07 («Autorizo el rol»): `--grant-role` con `--allow-production-role-change` dio `admin` a la cuenta de prueba `nv21-azure-admin-f696c8aa@example.com` (usuario `6aef750e…`) para la prueba, y al terminar `--revoke-role` lo retiró. Ambos cambios quedaron en la bitácora con actor `cli`; los logs solo muestran el id del usuario. El archivo local con las credenciales de prueba se borró.

Prueba contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-07) con tres cuentas de prueba (`nv21-azure-landlord-2266f406`, `nv21-azure-reporter-9c0efd39` y `nv21-azure-admin-f696c8aa`, todas `@example.com`). **23/23 PASS**:

| Bloque | Casos | Resultado |
| --- | --- | --- |
| Roles | sin rol 403 `role_required`; cola con rol admin 200 | 2/2 |
| Edificio de prueba de NV-12 | "PRUEBA AUTOMATIZADA NV-12" rechazado (`rejected`) | 1/1 |
| Revisión | envío a revisión 202; detalle con `building, rooms, landlord, lastDecision` y arrendador sin teléfono; correcciones sin motivo 400; correcciones 200 (`draft`); el dueño ve `changes_requested`; aprobar 200 (`verified`, 2 disponibles); aprobar de nuevo 409 `building_state` | 8/8 |
| Reportes y suspensión | reporte 201; repetido 409 `report_exists`; resolver con suspensión del edificio; reactivar; suspender la cuenta; su sesión actual 401; login 403 `account_suspended`; reactivar; login 200 | 9/9 |
| Bitácora y compatibilidad | bitácora con las 9 acciones probadas; `DELETE` 405; `/properties` sin cambios | 3/3 |

Quedan en producción el edificio "PRUEBA AUTOMATIZADA NV-12" en `rejected` (su dueño de prueba puede borrarlo), el edificio "PRUEBA AUTOMATIZADA NV-21" en `suspended` (al suspender la cuenta de prueba se suspendieron sus edificios; reactivar la cuenta no los reactiva, según la regla) y las tres cuentas de prueba, la de admin ya sin rol. Sus sesiones se cerraron.

## 8. Contrato entregable a Mobile

El contrato de `spec.md` §6.1 queda vigente sin cambios, con estos dos campos completos:

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        unitarias 243, integración 399, seguridad 54 en CI (696/696); prueba HTTP real en Azure 23/23 (2026-11-07)
```
