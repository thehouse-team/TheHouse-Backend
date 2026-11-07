# NV-12 — Evidencia: arrendador, edificios y cuartos

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-12 1.1.0, decisiones de la §13 y tasks T01–T13 (M-BE-064). Contrato §6.1 validado antes por el responsable de Mobile |
| Rama | `feat/nv-12-landlord` (sobre `docs/nv-12-1.1.0`, PR #20) |
| Resultado | **🟡 EN CURSO**: T01–T09 completas con pruebas locales; T10–T13 esperan CI, CD y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. Nombre, teléfono, RFC y dirección no aparecen en logs.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Listings.cs` | `LandlordProfile`, `Building` y `Room` con sus estados; transiciones `SubmitForReview`, `Pause`, `Resume`, `WithDerivedStatus` (`setup` ↔ `ready`) y `EnsureEditable` |
| Domain | `Exceptions/ForbiddenException.cs`, `ConflictException.cs` | 403 con código; 409 con detalle opcional (pasos pendientes) |
| Application | `Common/ListingRules.cs` | Catálogos cerrados, límites, RFC del SAT con dígito verificador, teléfono E.164 de México, distancia haversine y minutos a pie (5 km/h) |
| Application | `Contracts/Landlord/LandlordContracts.cs` | Entradas estrictas (sin `ownerId`, `status` ni campos calculados) y respuestas del §6.1 |
| Application | `Abstractions/Persistence/IListingRepositories.cs` | Puertos con reemplazo y borrado condicionados a `version` |
| Application | `UseCases/LandlordService.cs`, `BuildingService.cs`, `RoomService.cs` | Alta de arrendador (rol = perfil), edificios, cuartos, publicación y tablero |
| Application | `Common/RateLimitKeys.cs`, `UserRateLimitOptions.cs` | 60 lecturas y 30 escrituras por minuto; generar cuartos 10 por hora |
| Infrastructure | `Persistence/Mongo/MongoListingRepositories.cs`, `Documents/ListingDocuments.cs` | `landlord_profiles` (único `userId`), `buildings` (2dsphere, `{ownerId, createdAt}`), `rooms` (único `{buildingId, number}`, `{ownerId, status}`, `{status, price}`); generación concurrente con número repetido retira lo insertado y responde 409 |
| API | `Http/LandlordEndpoint.cs` | 17 endpoints de la §6.1 |
| API | `Http/UserEndpointSupport.cs`, `UserResourceProblems.cs`, `ProblemResponses.cs` | 403 con `code`; detalle específico en 409; cuerpos de hasta 8 KB; mensajes en español |
| API | `Configuration/LandlordConfiguration.cs`, `Program.cs` | Registro y mapeo; versión de términos en `LandlordTerms:Version` (por defecto `2026-11`) |

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas) | 671/671: unitarias 230, seguridad 52, integración 389 |

Pruebas nuevas: `TheHouse.UnitTests/ListingTests.cs` (19) y `TheHouse.IntegrationTests/LandlordHttpTests.cs` (28). Los dobles en memoria (`ListingTestDoubles.cs`) se comparten entre ambos proyectos.

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Landlord_profile_is_created_once_and_validated`, `Landlord_profile_rejects_invalid_data` (6 casos), `Agency_with_valid_rfc_is_accepted`, `Rfc_validation_checks_format_and_check_digit`, `Phone_must_be_mexican_e164`, `Landlord_service_requires_profile_and_rejects_duplicates` |
| R02 | `Building_is_created_in_draft_with_computed_distance`, `Building_rejects_invalid_or_server_owned_fields` (6 casos), `Unknown_university_returns_404`, `Building_update_recomputes_distance_and_checks_version` |
| R03 | `Rooms_are_generated_by_floor_without_duplicates_or_excess`, `Room_service_numbers_rooms_by_floor_and_enforces_limits` |
| R04 | `Room_edits_require_current_version_and_derive_status`, `Duplicate_copy_pause_and_resume_follow_the_state_machine`, `Copy_from_takes_data_from_a_room_of_the_same_building` |
| R05 | `Room_status_is_derived_from_its_data`, `Room_pause_and_resume_only_from_valid_states`, `Building_can_only_be_submitted_from_draft`, `Publication_lists_steps_and_publish_moves_to_review` |
| R06 | `Walking_distance_rounds_km_and_minutes_up`, `Building_update_recomputes_distance_and_checks_version` |
| R07 | `Concurrent_edits_with_the_same_version_let_only_one_win`, `Room_edits_require_current_version_and_derive_status` (versión vieja 409) |
| R08 | `Publication_lists_steps_and_publish_moves_to_review`, `Dashboard_summarizes_rooms_and_income_and_delete_respects_rented_rooms` |
| R09 | `Users_without_landlord_profile_get_403`, `Other_landlords_never_see_or_change_my_buildings_and_rooms`, `Room_service_hides_rooms_of_other_landlords`, `Limits_auth_and_dependencies_fail_closed`, `Personal_data_is_not_written_to_logs` |

## 4. Seguridad y coherencia

- El usuario sale del `sub`; ningún cuerpo acepta `ownerId`, `userId`, `status`, distancias ni campos calculados (400).
- Edificio o cuarto ajeno → 404; usuario sin perfil de arrendador → 403 `landlord_required`.
- Concurrencia optimista: cada escritura exige la versión leída y la incrementa; la prueba concurrente deja una sola edición ganadora.
- Datos personales (nombre, teléfono, RFC y dirección) solo en respuestas del dueño; la prueba de logs confirma que no aparecen.
- Rate limit y repositorios fallan cerrados con 503.
- NV-12 no toca `properties`, favoritos ni visitas (M-BE-064); la app sigue igual.
- Costo: sin servicios nuevos; tres colecciones pequeñas en Atlas M0.

## 5. Pendiente

- T10: CI verde, merge, CD aprobado y prueba HTTP real en Azure; entregar el contrato §6.1 con `DEPLOYED URL` y `TESTS`.
- T11/T12: TRX y cobertura del run de CI.
- T13: cierre de esta evidencia.
