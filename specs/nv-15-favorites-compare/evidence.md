# NV-15 — Evidencia: favoritos

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `evidence.md` |
| Versión | 1.2.0 |
| Fecha | 2026-11-09 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-15 1.2.0, tasks T01–T10 (M-BE-067); la migración `--migrate-to-rooms` en Production requiere autorización aparte |
| Rama | `feat/nv-15-nv-17-rooms` (commit de implementación `a0de2d4`) → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T06 completas y verificadas en local; T07–T10 esperan CI, la migración autorizada en Production y la prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa (1.2.0)

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Application | `Abstractions/Persistence/IFavoriteRepository.cs` | Agregar, quitar por cuarto o id de propiedad, existe, contar, listar todos, migrar |
| Application | `Contracts/Favorites/FavoriteContracts.cs` | Item con `roomId`, `buildingId`, `title`, `price`, `currency`, `roomType`, `bathroom`, `city`, `approxLocation`, `demo`, `favoritedAt` y, en transición, `propertyId`; `ids` con `roomIds` y `propertyIds` |
| Application | `UseCases/FavoriteService.cs` | Solo cuartos publicados (404 si no); idempotente; límite 200 (409 `favorites_limit`); listado e ids omiten los no publicados sin borrarlos y traducen los favoritos sin migrar |
| Infrastructure | `Persistence/Mongo/MongoFavoriteRepository.cs` | Al iniciar elimina `ux_favorites_user_property` e `ix_favorites_user_created`; crea `ux_favorites_user_room` (único parcial: solo si `roomId` es texto) e `ix_favorites_user_created_v2`; migración con borrado de duplicados |
| API | `Http/FavoritesEndpoint.cs` | `PUT`/`DELETE /users/me/favorites/{roomId}` (acepta id de propiedad en transición) |
| Domain | `Entities/Favorite.cs`, `Entities/Visit.cs` | `RoomId` y, solo en datos de 1.1.0 sin migrar, `LegacyPropertyId` |
| Application | `UseCases/RoomCatalog.cs` (nuevo) | Resuelve un cuarto publicado por id de cuarto o, en transición, por id de propiedad de NV-07 (`Building.DemoSourceId`); carga por lotes |
| Application | `Abstractions/Persistence/IListingRepositories.cs` | `GetManyAsync` de edificios y cuartos (lectura por lotes) |
| Application | `UseCases/FavoriteService.cs` | `RoomMigrationService` y `RoomMigrationOptions`: migración idempotente con conteos (migrados, duplicados eliminados, sin mapeo) |
| Infrastructure | `Persistence/Mongo/MongoListingRepositories.cs` | `GetManyAsync` con `$in` por `_id` |
| API | `Http/PropertyEndpoint.cs` | `/properties` responde `Deprecation: true` y `Link: </api/v1/rooms/search>; rel="successor-version"` |
| API | `Configuration/SearchConfiguration.cs`, `Program.cs` | Comando `--migrate-to-rooms`: requiere `RoomMigration__Enabled=true` y, en Production, `--allow-production-migration` (M-BE-043); la bandera de seeds no lo autoriza |

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 738/738 en ambas: unitarias 251, seguridad 61, integración 426 |

## 3. Trazabilidad requisito → prueba (1.2.0)

| Requisito | Pruebas |
| --- | --- |
| R01 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Add_rejects_invalid_or_missing_room`, `Unpublished_rooms_cannot_be_added_and_are_hidden_from_list_and_ids`, `Limit_of_200_favorites_returns_409_with_code`, `Favorites_add_is_idempotent_and_requires_a_published_room`, `Favorites_are_scoped_to_the_user_and_limited_to_200` |
| R02 | `Ids_are_newest_first_and_remove_is_idempotent` |
| R03 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Ids_are_newest_first_and_remove_is_idempotent`, `Unpublished_rooms_cannot_be_added_and_are_hidden_from_list_and_ids`, `Pagination_is_validated`, `Unpublished_rooms_are_hidden_but_not_deleted` |
| R04 | `Legacy_property_ids_are_accepted_during_the_transition`, `Legacy_property_catalog_announces_its_deprecation`, `Favorites_accept_legacy_property_ids_and_report_them` |
| R05 | `Unmigrated_favorites_are_listed_by_room_and_migrated_by_the_command`, `Migration_converts_legacy_favorites_and_visits_and_removes_duplicates`, `Migration_requires_configuration_and_explicit_production_flag` (seguridad). Los índices reales se verifican en Azure |
| R06 | `Users_never_see_each_others_favorites`, `Concurrent_double_tap_creates_a_single_favorite`, `Rate_limit_returns_429_with_retry_after`, `Fails_closed_when_redis_or_mongo_are_down`, `Requires_a_valid_bearer_token` |
| R07 | Pendiente: migración autorizada y prueba HTTP real en Azure |
| R08 | Pendiente: TRX y cobertura del CI del PR |

## 4. Seguridad y coherencia (1.2.0)

- El usuario sale del `sub`; ninguna entrada acepta `userId`. Quitar el favorito de otro usuario no es posible.
- El listado no expone dirección, coordenadas exactas ni el dueño: la ubicación es la celda aproximada de NV-14 y el dueño del demo (`system-demo`) no aparece.
- Un cuarto pausado, o de un edificio no verificado, no puede agregarse (404) y desaparece del listado y de `ids`; el favorito se conserva y reaparece si se vuelve a publicar.
- Redis, MongoDB o el catálogo de cuartos caídos → 503.
- La migración solo corre con configuración explícita y, en Production, con su propia bandera.

## 5. Pendiente para cerrar 1.2.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Migración `--migrate-to-rooms --allow-production-migration` con `RoomMigration__Enabled=true` | Victor (autorización explícita) | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv15_17_v12_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |

## Historial — evidencia de 1.1.0

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «te apluebo o ejecuta para hacer el desarrollo de lo que nos pidió» — Victor Manuel Jose Jose, 2026-11-07: NV-15 1.1.0, tasks T01–T10 (M-BE-060) |
| Rama | `feat/nv-15-nv-17` → PR #15, mezclado en `main` (`32f9ea0`) |
| Resultado | **✅ PASS WITH WARNINGS**: 10/10 tasks completas; desplegada en Azure y verificada con prueba HTTP real (26/26). Ajustes A2–A3 aprobados (M-BE-061). Advertencia: adaptador Mongo sin cobertura automatizada (verificado en Azure) |

### 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Favorite.cs` | `userId`, `propertyId`, `createdAt` |
| Application | `Abstractions/Persistence/IFavoriteRepository.cs` | Agregar (false si ya existe), quitar, quitar varios, contar, listar, ids |
| Application | `Abstractions/Persistence/IPropertyRepository.cs` | `GetByIdsAsync` (lectura por lotes) |
| Application | `Contracts/Favorites/FavoriteContracts.cs` | Item con datos públicos de la propiedad y `favoritedAt`; sobre de paginación NV-01 |
| Application | `UseCases/FavoriteService.cs` | Idempotencia, 404 de propiedad, límite 200 (409 `favorites_limit`), limpieza de favoritos de propiedades eliminadas |
| Infrastructure | `Persistence/Mongo/MongoFavoriteRepository.cs` | `favorites` con único `{userId, propertyId}` (doble toque → sin duplicado) e índice `{userId, createdAt desc, propertyId desc}` |
| API | `Http/FavoritesEndpoint.cs` | `PUT`/`DELETE /users/me/favorites/{propertyId}` (204), `GET /users/me/favorites`, `GET /users/me/favorites/ids`; 60/min por usuario; `no-store` |

Las piezas compartidas con NV-17 (`UserEndpointSupport`, `UserResourceProblems`, configuración y título 422) se describen en `specs/nv-17-visits/evidence.md`.

### 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas seguidas) | 553/553: unitarias 190, seguridad 52, integración 311 |

### 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Add_rejects_invalid_or_missing_property`, `Limit_of_200_favorites_returns_409_with_code`, `Favorites_add_is_idempotent_and_validates_property`, `Favorites_limit_is_200` |
| R02 | `Ids_are_newest_first_and_remove_is_idempotent` |
| R03 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Removed_properties_are_dropped_from_list_and_ids`, `Pagination_is_validated`, `Favorites_list_orders_newest_first_and_skips_orphans` |
| R04 | `Ids_are_newest_first_and_remove_is_idempotent` |
| R05 | `Concurrent_double_tap_creates_a_single_favorite` (doble con unicidad; el índice real se verifica en Azure), lectura por lotes en `FavoriteService` |
| R06 | `Users_never_see_each_others_favorites`, `Favorites_are_scoped_to_the_user`, `Rate_limit_returns_429_with_retry_after`, `Fails_closed_when_redis_or_mongo_are_down`, `Requires_a_valid_bearer_token` |

### 4. Seguridad y coherencia

- El usuario sale del `sub`; no hay `userId` en ninguna entrada. Borrar el favorito de otro usuario no es posible: la operación solo toca los del `sub`.
- El listado expone solo campos públicos de NV-07; no incluye la descripción.
- Redis o MongoDB caídos → 503.
- Ajustes A2 (`+00:00`) y A3 (`code` en 409) de `specs/nv-17-visits/evidence.md` §4 aplican igual aquí; aprobados en M-BE-061.

### 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PR | [#15](https://github.com/thehouse-team/TheHouse-Backend/pull/15), mezclado el 2026-11-07 (commit `32f9ea0`); aprobación registrada por la cuenta `thehouserent26` |
| CI del PR (`33c8126`) y de `main` (`32f9ea0`) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI (`main`, run 42354786972) | 553/553: unitarias 190, integración 311, seguridad 52; 34 pruebas de NV-15/NV-17 en integración y 33 en unitarias |
| Cobertura de líneas (CI) | 70.3 % total. Los adaptadores `MongoFavoriteRepository`, `MongoVisitRepository` y `RedisUserLock` quedan en 0 %, como los adaptadores Mongo/Redis existentes: se verifican con la prueba real en Azure |
| CD (run 42354881476) | resolve, publish, verify-ephemeral (Mongo 7.0.45, Redis 7.4.12, smoke, ZAP), deploy aprobado por Victor, DAST: todo en verde |
| Azure | revisión `ca-thehouse-u2--c32f9ea0-13-1`, imagen `sha256:27b71e88…` |

### 6. Prueba HTTP real en Azure

Prueba HTTP real contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-07), con dos cuentas nuevas de dominio reservado (`nv15-17-azure-a-3a9876f4@example.com` y `nv15-17-azure-b-cabd2770@example.com`). El script (`nv15_17_azure_check.py`, fuera del repo) solo imprime códigos y nombres de campos; nunca tokens ni contraseñas. **26/26 PASS**: favoritos 10, visitas 14, compatibilidad 2 (`/users/me` y detalle de propiedad sin cambios).

Una corrida anterior dio 25/26: el script usaba como id inexistente `000000000000000000000001`, que es la propiedad 1 del seed regional, así que el 204 era correcto. Se corrigió el script para usar un id que antes comprueba con `GET /properties/{id}` → 404. Esa corrida dejó un favorito de la propiedad 1 en la cuenta de prueba `nv15-17-azure-a-ccbfd189@example.com`.

Quedan en producción cuatro cuentas de prueba con prefijo `nv15-17-azure-` y dominio `example.com`, sin datos personales; sus sesiones se cerraron con logout. Se eliminarán cuando exista la eliminación de cuenta.

| Caso | Resultado |
| --- | --- |
| F1–F2 agregar y repetir | 204 y 204; `no-store` |
| F3 ids | 200; la propiedad aparece una vez |
| F4 listado | 200; sobre `items, page, pageSize, totalItems, totalPages`; item con los 10 campos del contrato |
| F5 otro usuario | 200 con `propertyIds` vacío |
| F6 propiedad inexistente | 404 |
| F7 id inválido | 400 |
| F8 sin token | 401 |
| F9–F10 quitar y repetir | 204 y 204 |

### 7. Contrato entregable a Mobile

El contrato de `spec.md` §6.1 queda vigente sin cambios de forma, con estos dos campos completos:

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        unitarias 190, integración 311, seguridad 52 en CI (553/553); prueba HTTP real en Azure 26/26 (2026-11-07)
```
