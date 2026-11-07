# NV-21 — Evidencia: verificación, reportes y moderación

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-21 1.1.0, decisiones de la §13 y tasks T01–T14 (M-BE-065). Contrato §6.1 validado antes por el responsable de Mobile |
| Rama | `feat/nv-21-moderation` → PR #24, mezclado en `main` (`7310ab0`); spec y aprobación en PR #23 (`c770b1c`) |
| Resultado | **✅ PASS WITH WARNINGS**: 14/14 tasks completas; desplegada en Azure y verificada con prueba HTTP real 23/23. Advertencia: adaptadores Mongo sin cobertura automatizada (verificados en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. Motivos y detalles de reportes, teléfonos y correos no aparecen en logs.

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
