# NV-14 — Evidencia: búsqueda, filtros y mapa

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-14 1.1.0, decisiones de la §13 y tasks T01–T13 (M-BE-066). Contrato §6.1 validado antes por el responsable de Mobile |
| Rama | `feat/nv-14-search` → PR #27, mezclado en `main` (`f39aa59`); spec y aprobación en PR #26 (`0301f91`) |
| Resultado | **✅ PASS WITH WARNINGS**: 13/13 tasks completas; desplegada en Azure, seeds de colonias y de demostración aplicados y prueba HTTP real 19/19 con p95 de 300 ms. Advertencia: adaptadores Mongo sin cobertura automatizada (verificados en Azure) |

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

## 7. Despliegue, seeds y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#26](https://github.com/thehouse-team/TheHouse-Backend/pull/26) (spec y aprobación) y [#27](https://github.com/thehouse-team/TheHouse-Backend/pull/27) (código), mezclados el 2026-11-07 |
| CI de `main` (run 42407184856, `f39aa59`) | 7/7 checks en verde; 723/723 pruebas (unitarias 250, integración 418, seguridad 55); 49 casos de NV-14 |
| Cobertura de líneas (CI) | 69.2 % total. `MongoPlaceRepository` y las consultas geoespaciales de `MongoListingRepositories` sin cobertura automatizada, como los demás adaptadores Mongo: verificados con la prueba real |
| CD (run 42407348366) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--cf39aa59-25-1`, imagen `sha256:be6ac4f6…` |
| Seeds en Production (M-BE-043) | Ejecutados por Victor el 2026-11-07 en esa revisión con `env PlaceSeed__Enabled=true DemoRoomSeed__Enabled=true dotnet TheHouse.dll --seed-places --seed-demo-rooms --allow-production-seed` vía `az containerapp exec`. Log: «Seed autorizado: colonias=True, cuartos de demostración=True», «Place seed completed (60)» y «Demo room seed completed (12 created)» |

## 8. Prueba HTTP real en Azure

Contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-07), con una cuenta de prueba (`nv14-azure-4c1a37a2@example.com`) para el detalle. **19/19 PASS**:

| Bloque | Casos | Resultado |
| --- | --- | --- |
| Búsqueda | 11 cuartos `demo` a menos de 30 km de la UTTT con ETag y los 15 campos del contrato; sin datos privados; orden por distancia; `maxPrice=3000` con orden por precio (1 800 a 2 800); conteo 7 igual a la búsqueda; servicio fuera del catálogo 400 | 9/9 |
| Mapa | 8 marcadores en el área; área demasiado grande 400 | 2/2 |
| Detalle | sin sesión 401; con sesión 200 con `no-store` y edificio sin dirección ni dueño | 3/3 |
| Colonias | "centro" → Centro; "cuauhtemoc" → Colonia Cuauhtémoc | 2/2 |
| Rendimiento (T08) | 40 búsquedas, una por segundo, todas 200: **p50 230 ms, p95 300 ms**, máximo 753 ms, medidos desde el equipo de Victor (incluyen la red) | 2/2 |
| Compatibilidad | `/properties` sigue con 12 propiedades | 1/1 |

El duodécimo cuarto de demostración (Villa del Carbón) queda a más de 30 km de la UTTT, por eso la búsqueda desde la UTTT muestra 11. Queda en producción la cuenta de prueba de NV-14, sin datos personales y con su sesión cerrada.

## 9. Contrato entregable a Mobile

El contrato de `spec.md` §6.1 queda vigente sin cambios, con estos dos campos completos:

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        unitarias 250, integración 418, seguridad 55 en CI (723/723); prueba HTTP real en Azure 19/19, p95 300 ms (2026-11-07)
```
