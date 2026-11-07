# NV-14 — Evidencia: búsqueda, filtros y mapa

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-14 1.1.0, decisiones de la §13 y tasks T01–T13 (M-BE-066). Contrato §6.1 validado antes por el responsable de Mobile |
| Rama | `feat/nv-14-search` (sobre `docs/nv-14-1.1.0`, PR #26) |
| Resultado | **🟡 EN CURSO**: T01–T07 y T09 completas con pruebas locales; T08 (p95 en Azure) y T10–T13 esperan CI, CD, seeds autorizados en Production y prueba HTTP real |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Listings.cs`, `Entities/Place.cs` | `Building.Demo` y `DemoSourceId`; `Place` |
| Application | `UseCases/SearchService.cs` | `ApproximateLocation` (celda de 0.003°), `SearchService` (búsqueda, mapa, conteo, detalle y colonias en dos pasos), `PlaceSeedService`, `DemoRoomSeedService` |
| Application | `Common/PlaceSeedData.cs` | 60 colonias y localidades de OpenStreetMap con su id de nodo (§4) |
| Application | `Abstractions/Persistence/IListingRepositories.cs` | `ListVerifiedNearAsync`, `ListDemoAsync`, `ListAvailableByBuildingsAsync`, `RoomFilter`, `IPlaceRepository` |
| Application | `Contracts/Search/SearchContracts.cs`, `Common/RateLimitKeys.cs`, `UserRateLimitOptions.cs` | Respuestas públicas sin datos privados; 60/min por IP y detalle 60/min por usuario |
| Infrastructure | `MongoListingRepositories.cs`, `Documents/ListingDocuments.cs` | `$nearSphere` sobre edificios `verified`; cuartos `available` por lote de edificios con filtros; campos `demo` y `demoSourceId` |
| Infrastructure | `MongoPlaceRepository.cs` | `places` con único `osmNodeId`, `2dsphere` y búsqueda parcial escapada |
| API | `Http/SearchEndpoint.cs`, `ProblemResponses.cs` | 5 endpoints; caché pública con ETag en los públicos; `no-store` en detalle y errores |
| API | `Configuration/SearchConfiguration.cs`, `Program.cs` | `--seed-places` y `--seed-demo-rooms` con `PlaceSeed__Enabled`/`DemoRoomSeed__Enabled` y `--allow-production-seed` |

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas) | 723/723: unitarias 250, seguridad 55, integración 418 |

Pruebas nuevas: `TheHouse.UnitTests/SearchTests.cs` (7), `TheHouse.IntegrationTests/SearchHttpTests.cs` (19) y `TheHouse.SecurityTests/SearchSeedPolicyTests.cs` (1).

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Search_returns_published_rooms_with_contract_shape_and_no_private_data`, `Filters_sort_and_pagination_apply_and_count_matches`, `Invalid_filters_return_400` (9 casos), `Unknown_university_returns_404` |
| R02 | `Map_returns_markers_inside_a_valid_bbox` |
| R03 | `Filters_sort_and_pagination_apply_and_count_matches` (conteo = `totalItems`) |
| R04 | `Room_detail_requires_session_and_hides_private_data` |
| R05 | `Places_are_found_without_accents_and_limited_to_ten`, `Place_seed_has_unique_osm_ids_and_regional_coordinates` |
| R06 | `Approximate_location_hides_the_real_point_and_distances_use_the_cell`, `Cell_is_stable_and_close_to_the_real_point` (4 casos), `Points_in_the_same_cell_share_the_same_approximate_location`, `Distances_from_several_universities_do_not_reveal_more_than_the_cell` |
| R07 | `Demo_seed_is_idempotent_and_keeps_the_property_mapping` |
| R09 | `Only_available_rooms_of_verified_buildings_are_public`, `Search_limits_and_dependencies_fail_closed`, `Seeds_require_configuration_and_the_production_flag`, `Map_returns_markers_inside_a_valid_bbox` (bbox grande 400) |

## 4. Colonias (OpenStreetMap, ODbL)

Consulta Overpass del 2026-11-07 sobre nodos `place` (city, town, village, suburb, neighbourhood, quarter) en el rectángulo 19.65–20.30 N, 99.70–99.10 O, filtrados por distancia a la universidad más cercana (localidades hasta 4 km, colonias hasta 5–6 km, cabeceras hasta 15 km): 60 lugares, cada uno con su id de nodo en `PlaceSeedData.cs`. Atribución: © colaboradores de OpenStreetMap.

## 5. Notas de implementación

- `allowedTenants` como filtro: `women` incluye edificios `women` y `any`; `men` incluye `men` y `any`; `any` solo edificios `any`.
- El campo `demo` vive en el edificio (los cuartos lo heredan en la respuesta); la spec mencionaba ambos, pero basta con el edificio.
- El seed de demostración usa un arrendador de sistema `system-demo` sin cuenta en `users`: no puede iniciar sesión ni aparece en respuestas públicas. Cada edificio guarda `demoSourceId` (id de la propiedad de NV-07), que es el mapeo para la migración de NV-15/NV-17.

## 6. Seguridad y coherencia

- Las respuestas públicas no incluyen dirección, coordenadas reales, dueño ni teléfono; la prueba lo verifica sobre el cuerpo completo.
- La ubicación aproximada es el centro de una celda de 330 m y todas las distancias se calculan desde ese centro: dos edificios de la misma celda dan las mismas distancias a cualquier universidad (prueba de no triangulación).
- Límites anti-extracción: `pageSize` ≤ 50, `bbox` ≤ 0.3° por lado, 300 marcadores, 60/min por IP; Redis o Mongo caídos → 503.
- `/properties`, favoritos y visitas no cambian (M-BE-066).
- Costo: sin servicios nuevos; una colección de 60 documentos y lecturas con índices existentes.

## 7. Pendiente

- T08: medir p95 en Azure con los datos de demostración.
- T10: CI, merge, CD; seeds de colonias y de demostración en Production con autorización; prueba HTTP real; contrato para Mobile.
- T11–T13: TRX y cobertura del run de CI y cierre de esta evidencia.
