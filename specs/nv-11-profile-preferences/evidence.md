# NV-11 — Evidencia: perfil, universidad y preferencias

| Campo | Valor |
| --- | --- |
| ID | NV-11 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-11 1.1.0, decisiones de la §13 y tasks T01–T11 (M-BE-063). Contrato §6.1 validado antes por el responsable de Mobile |
| Rama | `feat/nv-11-profile-preferences` → PR #18, mezclado en `main` (`88599c2`) |
| Resultado | **✅ PASS WITH WARNINGS**: 11/11 tasks completas; desplegada en Azure, seed autorizado de coordenadas aplicado y prueba HTTP real 24/24. Advertencias: 3 de 10 coordenadas aproximadas al centro de la localidad (§4); adaptadores Mongo sin cobertura automatizada (verificados en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. No se registran nombres, preferencias ni coordenadas en logs.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/UserProfile.cs`, `Entities/UserPreferences.cs` | Perfil y preferencias; regla de configuración rápida (RF-72) |
| Domain | `Entities/University.cs` | `Latitude` y `Longitude` del campus (aditivo) |
| Application | `Common/SearchText.cs` | Normalización sin acentos, mayúsculas ni separadores; genera `searchKey` |
| Application | `Common/PreferenceCatalogs.cs` | Catálogos cerrados validados con Mobile |
| Application | `Contracts/Profile/ProfileContracts.cs` | Entradas estrictas (`quickSetupCompleted`, `version` y `userId` del cliente → 400) |
| Application | `Abstractions/Persistence/IProfileRepository.cs`, `IUniversityRepository.cs` | Puertos de perfil y preferencias; búsqueda por texto y por cercanía |
| Application | `UseCases/ProfileService.cs`, `PreferencesService.cs`, `UniversitySearchService.cs` | Casos de uso; distancia haversine redondeada a un decimal |
| Application | `Common/UniversitySeedData.cs` | Coordenadas de los 10 campus (§4) |
| Application | `Common/RateLimitKeys.cs`, `UserRateLimitOptions.cs` | 60/min por usuario (perfil) y 60/min por IP (búsqueda) |
| Infrastructure | `Persistence/Mongo/MongoProfileRepositories.cs`, `Documents/ProfileDocuments.cs` | `user_profiles` y `user_preferences` con índice único `{userId}`; upserts atómicos; `version` con `$inc` |
| Infrastructure | `Persistence/Mongo/MongoUniversityRepository.cs`, `Documents/UniversityDocument.cs` | `location` (2dsphere) y `searchKey`; búsqueda por texto escapado y `$nearSphere`; el seed escribe ambos campos |
| API | `Http/ProfileEndpoint.cs`, `Http/UniversitySearchEndpoint.cs` | 5 endpoints autenticados y la búsqueda pública |
| API | `Http/UserResourceProblems.cs`, `Http/ProblemResponses.cs` | Mensajes en español para perfil y preferencias |
| API | `Configuration/ProfileConfiguration.cs`, `Program.cs` | Registro y mapeo |

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas seguidas) | 624/624: unitarias 211, seguridad 52, integración 361 |

Pruebas nuevas: `TheHouse.UnitTests/ProfilePreferencesTests.cs`, `TheHouse.IntegrationTests/ProfileHttpTests.cs` y `TheHouse.IntegrationTests/UniversitySearchHttpTests.cs`. Se ajustó `UniversityTests.Seed_has_no_personal_or_contact_data` para admitir las coordenadas públicas del campus.

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Empty_profile_and_default_preferences_have_contract_shape`, `Name_is_updated_and_validated`, `Profile_service_validates_name_and_university` |
| R02 | `University_is_saved_idempotently_and_shown_in_profile` |
| R03 | `Preferences_replace_increments_version_and_derives_quick_setup`, `Preferences_reject_invalid_or_server_owned_fields` (16 casos), `Validate_*`, `Preferences_service_returns_defaults_and_increments_version` |
| R04 | `Text_search_is_partial_and_ignores_accents_and_case` (6 casos), `Text_search_matches_all_three_uaeh_schools`, `Invalid_queries_return_400`, `Long_query_returns_400`, `SearchText_normalizes_accents_case_and_separators` |
| R05 | `Near_search_orders_by_distance_and_is_not_cached`, `University_search_by_proximity_returns_sorted_distances`, `Haversine_distance_matches_known_value`, `Seed_universities_all_have_regional_coordinates` |
| R06 | `Existing_university_endpoints_do_not_expose_coordinates` (NV-08 sin cambios de contrato); índices únicos y 2dsphere en los repositorios Mongo |
| R07 | `Users_only_see_their_own_profile_and_preferences`, `Requires_a_valid_bearer_token` (5 casos), `Rate_limit_returns_429_and_dependencies_fail_closed`, `Rate_limit_by_ip_and_fail_closed`, `Name_is_not_written_to_logs`, `Coordinates_are_not_written_to_logs` |

## 4. Coordenadas de las universidades (OpenStreetMap, ODbL)

Fuente: Overpass API sobre OpenStreetMap (© colaboradores de OpenStreetMap), consultado el 2026-11-07. El punto es el centro del elemento.

| Código | Elemento OSM | Latitud | Longitud | Precisión |
| --- | --- | --- | --- | --- |
| UTTT | way 386380501 | 20.00903 | -99.34471 | Campus |
| UAEH-ESTE | way 571163606 | 19.91703 | -99.36116 | Campus |
| UAEH-ESTL | node 459392250 | 20.12991 | -99.23041 | **Centro de Tlahuelilpan** (el campus no está en OSM) |
| UAEH-ESAT | way 689177952 | 20.01584 | -99.23601 | Campus ("UAEH Plantel Atotonilco de Tula") |
| ITSOEH | way 403660593 | 20.20551 | -99.22178 | Campus |
| TESJI | node 13173489446 | 19.91953 | -99.57719 | Campus |
| UMB-JILO | node 13206892316 | 19.91248 | -99.57849 | Campus ("Unidad de Estudios Superiores Jilotepec") |
| UMB-VDC | node 10900967466 | 19.72791 | -99.46312 | **Centro de Villa del Carbón** (el campus no está en OSM) |
| UCAN | node 459391798 | 20.05170 | -99.34507 | **Centro de Tula** (el campus no está en OSM) |
| UNE-TULA | way 458178077 | 20.03108 | -99.33629 | Campus ("Universidad del Nuevo México") |

Hallazgo: 3 de 10 son aproximadas al centro de la localidad (error esperado de pocos kilómetros). Se corrigen cuando el equipo confirme la ubicación del campus; basta con actualizar el seed y volver a correrlo.

## 5. Seguridad y coherencia

- El usuario sale del `sub`; no hay rutas ni campos con `userId` (enviarlo → 400).
- `quickSetupCompleted` y `version` los calcula el servidor; enviarlos → 400.
- El texto de búsqueda se escapa con `Regex.Escape` antes de usarse en Mongo (sin inyección de patrones ni ReDoS).
- Las coordenadas de búsqueda no se guardan ni se registran; la respuesta por cercanía lleva `no-store` para que ningún caché compartido guarde la URL. La búsqueda por texto usa el caché público de NV-08.
- `GET /universities` y `GET /universities/{id}` no cambian: las coordenadas no salen en sus respuestas.
- Rate limit y repositorios fallan cerrados con 503.
- Costo: sin servicios nuevos; dos colecciones pequeñas y un índice 2dsphere en Atlas M0.

## 6. Despliegue, seed y CI

| Paso | Resultado |
| --- | --- |
| PR | [#18](https://github.com/thehouse-team/TheHouse-Backend/pull/18), mezclado el 2026-11-07 (`88599c2`); aprobación registrada por la cuenta `thehouserent26` |
| CI de `main` (run 42393690681) | 7/7 checks en verde; 624/624 pruebas (unitarias 211, integración 361, seguridad 52); 71 pruebas de NV-11 |
| Cobertura de líneas (CI) | 69.6 % total. `MongoProfileRepositories` y `ProfileDocuments` en 0 %, como los demás adaptadores Mongo: verificados con la prueba real en Azure |
| CD (run 42393781540) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c88599c2-16-1` (100 % del tráfico, sana), imagen `sha256:660118cf…` |
| Seed de coordenadas (T05) | Autorizado por Victor el 2026-11-07 («Autorizo el seed», M-BE-043). Una ejecución con `env UniversitySeed__Enabled=true dotnet TheHouse.dll --seed-universities --allow-production-seed` vía `az containerapp exec` en esa revisión. Log: «universidades=True, propiedades=False» y «University seed completed». Solo se actualizaron las 10 universidades por `code` |

## 7. Prueba HTTP real en Azure

Contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-07), con dos cuentas nuevas de dominio reservado (`nv11-azure-a-3318c15b@example.com` y `nv11-azure-b-3ce6dbfe@example.com`). El script (`nv11_azure_check.py`, fuera del repo) solo imprime códigos y nombres de campos. **24/24 PASS**:

| Bloque | Casos | Resultado |
| --- | --- | --- |
| Búsqueda pública | `TÉPEJI` → UTTT y UAEH-ESTE con caché público; `uaeh` → 3 escuelas; cercanía desde la UTTT → UTTT a 0 km, 10 resultados ordenados y `no-store`; `q` corta y modos combinados → 400 | 7/7 |
| Perfil y universidad | perfil vacío con `no-store`; nombre; `userId` en el cuerpo → 400; universidad 204; inexistente 404; perfil con `UTTT` | 6/6 |
| Preferencias | valores por defecto (`version` 0); guardar (`version` 1, `quickSetupCompleted` true); campo del servidor → 400; catálogo → 400; 415; otro usuario con `version` 0; sin token 401 | 7/7 |
| Compatibilidad | `/users/me`; `/universities` con sus 6 campos y sin ubicación; `/properties` | 4/4 |

Quedan en producción dos cuentas de prueba `nv11-azure-*@example.com`, sin datos personales y con sesión cerrada.

## 8. Contrato entregable a Mobile

El contrato de `spec.md` §6.1 queda vigente sin cambios, con estos dos campos completos:

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        unitarias 211, integración 361, seguridad 52 en CI (624/624); prueba HTTP real en Azure 24/24 (2026-11-07)
```
