# NV-15 — Evidencia: favoritos

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «te apluebo o ejecuta para hacer el desarrollo de lo que nos pidió» — Victor Manuel Jose Jose, 2026-11-07: NV-15 1.1.0, tasks T01–T10 (M-BE-060) |
| Rama | `feat/nv-15-nv-17` (junto con NV-17; base `main` 546f506) |
| Resultado | **🟡 EN CURSO**: T01–T06 completas con pruebas locales; T07–T10 esperan CI, despliegue por CD y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

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

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas seguidas) | 553/553: unitarias 190, seguridad 52, integración 311 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Add_rejects_invalid_or_missing_property`, `Limit_of_200_favorites_returns_409_with_code`, `Favorites_add_is_idempotent_and_validates_property`, `Favorites_limit_is_200` |
| R02 | `Ids_are_newest_first_and_remove_is_idempotent` |
| R03 | `Add_is_idempotent_and_listing_returns_contract_shape`, `Removed_properties_are_dropped_from_list_and_ids`, `Pagination_is_validated`, `Favorites_list_orders_newest_first_and_skips_orphans` |
| R04 | `Ids_are_newest_first_and_remove_is_idempotent` |
| R05 | `Concurrent_double_tap_creates_a_single_favorite` (doble con unicidad; el índice real se verifica en Azure), lectura por lotes en `FavoriteService` |
| R06 | `Users_never_see_each_others_favorites`, `Favorites_are_scoped_to_the_user`, `Rate_limit_returns_429_with_retry_after`, `Fails_closed_when_redis_or_mongo_are_down`, `Requires_a_valid_bearer_token` |

## 4. Seguridad y coherencia

- El usuario sale del `sub`; no hay `userId` en ninguna entrada. Borrar el favorito de otro usuario no es posible: la operación solo toca los del `sub`.
- El listado expone solo campos públicos de NV-07; no incluye la descripción.
- Redis o MongoDB caídos → 503.
- Ajustes A2 (`+00:00`) y A3 (`code` en 409) de `specs/nv-17-visits/evidence.md` §4 aplican igual aquí.

## 5. Pendiente

- T07: CI verde, merge, CD aprobado y prueba HTTP real en Azure (agregar, repetir, listar, ids, quitar, repetir); entregar el contrato §6.1 con `DEPLOYED URL` y `TESTS`.
- T08/T09: TRX y cobertura del run de CI.
- T10: cierre de esta evidencia con commit del merge y resultado final.
