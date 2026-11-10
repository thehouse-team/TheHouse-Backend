# NV-17 — Evidencia: visitas v1

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `evidence.md` |
| Versión | 1.2.0 |
| Fecha | 2026-11-09 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-07: NV-17 1.2.0, tasks T01–T10 (M-BE-067); la migración `--migrate-to-rooms` en Production requiere autorización aparte |
| Rama | `feat/nv-15-nv-17-rooms` → PR #30 (specs en #29), mezclados en `main` (`14f4ff2`) |
| Resultado | **✅ PASS WITH WARNINGS**: 10/10 tasks completas; desplegada en Azure, migración autorizada ejecutada y prueba HTTP real 38/38. Advertencias: W1 (llaves de idempotencia de 1.1.0) y adaptador Mongo sin cobertura automatizada (verificado en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa (1.2.0)

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Application | `Abstractions/Persistence/IVisitRepository.cs` | `MigrateLegacyAsync` |
| Application | `Contracts/Visits/VisitContracts.cs` | `CreateVisitRequest` con `roomId` o (transición) `propertyId`, exactamente uno; `VisitResponse` con `room { id, buildingId, title, city }` y `property` o `null` |
| Application | `UseCases/VisitService.cs` | Traduce el id al cuarto antes del hash de idempotencia (`{roomId}\n{ticks}\n{message}`); la repetición se resuelve antes de revisar si sigue publicado; conflictos por cuarto con el código `active_visit_for_property` de 1.1.0 |
| Infrastructure | `Persistence/Mongo/MongoVisitRepository.cs`, `Documents/FavoriteVisitDocuments.cs` | `roomId` y `propertyId` opcionales (`BsonIgnoreIfNull`); migración `$set roomId` / `$unset propertyId`. Los índices de visitas no dependen de `propertyId` y no cambian |
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
| R01 | `Create_returns_201_with_contract_shape_in_utc`, `Create_rejects_bad_input_with_400` (ambos ids o ninguno), `Create_rejects_unsupported_media_type_and_missing_room`, `Unpublished_rooms_reject_new_visits_but_keep_existing_ones_readable`, `Visit_create_validates_input_before_side_effects` |
| R02 | `Create_returns_201_with_contract_shape_in_utc`, `Unmigrated_visits_are_read_with_their_room_and_migrated_by_the_command`, `Visits_of_unpublished_rooms_can_still_be_read_and_cancelled_but_not_by_others` |
| R03 | `Schedule_rules_return_422_with_code`, `Same_key_and_body_replays_and_different_body_conflicts`, `Conflicts_return_409_with_specific_codes`, `Concurrent_requests_with_different_keys_create_only_one_visit`, `Cancel_is_idempotent_and_list_filters_by_status`, `Visit_create_enforces_conflicts_per_room_and_active_limit`, `Visit_cancel_after_the_scheduled_time_is_a_conflict` |
| R04 | `Legacy_property_id_creates_the_visit_on_its_room_and_replays_by_room`, `Visit_create_replays_same_key_with_room_or_legacy_id` |
| R05 | `Unmigrated_visits_are_read_with_their_room_and_migrated_by_the_command`, `Migration_converts_legacy_favorites_and_visits_and_removes_duplicates`, `Migration_requires_configuration_and_explicit_production_flag` (seguridad) |
| R06 | `Other_users_visit_is_404_for_get_and_cancel`, `Daily_create_limit_returns_429_with_retry_after`, `Fails_closed_when_lock_or_storage_are_down`, `Requires_a_valid_bearer_token`, `Message_is_not_written_to_logs`, `Visit_create_fails_when_the_user_lock_is_held` |
| R07 | Migración autorizada en Production y prueba HTTP real en Azure 38/38 (§5 y §6); contrato entregado (§7) |
| R08 | CI de `main`: 738/738 y cobertura del §5; TRX de `TheHouse.SecurityTests` 61/61 |

## 4. Seguridad y coherencia (1.2.0)

- El usuario sale del `sub`; `userId` o `status` en el cuerpo → 400 (asignación masiva). Las visitas de otro usuario responden 404.
- Un cuarto no publicado no admite visitas nuevas (404); las visitas existentes se pueden leer y cancelar.
- El mensaje no se escribe en logs; la respuesta no incluye dirección ni dueño.
- Lock de Redis o MongoDB caídos → 503.
- **Advertencia W1:** el hash de idempotencia usa ahora el `roomId`. Durante las 24 h de vida de una llave, repetir después de la migración una petición de 1.1.0 con la misma llave responde 409 `idempotency_key_reused` en lugar de la visita original. El riesgo es bajo (ventana de 24 h, solo reintentos en ese momento) y se documenta para Mobile.

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#29](https://github.com/thehouse-team/TheHouse-Backend/pull/29) (specs 1.2.0) y [#30](https://github.com/thehouse-team/TheHouse-Backend/pull/30) (implementación), aprobados por `IsaiAxel` y mezclados el 2026-11-09 (merge `14f4ff2`). El conflicto de `specs/README.md` con #28 se resolvió regenerando el índice |
| CI de `main` (`14f4ff2`, run 42680187690) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 738/738: unitarias 251, integración 426, seguridad 61 |
| Cobertura de líneas (CI) | 68.9 % total. `RoomCatalog` 100 %, `FavoriteService` 97.3 %, `VisitService` 99.2 %, `FavoritesEndpoint` 100 %, `VisitsEndpoint` 100 %, `PropertyEndpoint` 99.3 %. `MongoFavoriteRepository` y `MongoVisitRepository` en 0 %, como los demás adaptadores Mongo: se verifican en Azure |
| CD (run 42680553139) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c14f4ff2-29-1` |
| Migración en Production (M-BE-043) | Autorizada por Victor el 2026-11-09 («autoriso la migracion»). Una ejecución con `env RoomMigration__Enabled=true dotnet TheHouse.dll --migrate-to-rooms --allow-production-migration` vía `az containerapp exec` en esa revisión. Log: «favoritos migrados=1, duplicados eliminados=0, sin mapeo=0; visitas migradas=2, sin mapeo=0» y «Room migration completed». Los índices de favoritos se cambiaron en esa misma ejecución |

## 6. Prueba HTTP real en Azure

`.azure-check/nv15_17_v12_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-09 después de la migración, con dos cuentas `@example.com` nuevas; no se imprimen tokens ni contraseñas. Resultado: **38/38 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Obsolescencia de `/properties` | `Deprecation: true` y `Link` al sucesor | 2/2 |
| Favoritos | Agregar por `roomId` e idempotente; listado con los campos del §6.1 y sin datos privados; mismo cuarto por su `propertyId` sin duplicar; `ids` con `roomIds`/`propertyIds`; agregar y quitar por `propertyId` de NV-07; otro usuario no ve nada; 404 inexistente, 400 id inválido, 401 sin token | 16/16 |
| Visitas | Crear por `roomId` (201, `room` con `id, buildingId, title, city`); repetir con la misma llave por `roomId` y por `propertyId` (200, mismo id); 409 `idempotency_key_reused` y `active_visit_for_property`; 422 `outside_hours`; 400 con ambos ids, sin ids y con `userId`; 404 cuarto inexistente; 429 en el intento 11 del día con `Retry-After`; crear por `propertyId` (201, `property.id` coincide); BOLA 404 en consultar y cancelar; cancelar; 401 sin token | 18/18 |
| Compatibilidad | `/users/me` y búsqueda de cuartos sin cambios | 2/2 |

Primera corrida: 35/37. Las dos fallas venían del script: la cuenta de prueba ya había hecho 10 intentos de crear visita y el límite diario de 10 de NV-17 1.1.0 respondió 429, como debe; la visita de transición no se creó y no había nada que cancelar. Se corrigió el script (caso V11a que espera el 429 y la visita de transición creada por otra cuenta); el API no cambió.

## 7. Contrato entregable a Mobile

Entregado el 2026-11-09 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` con la URL y las pruebas reales:

```text
ENDPOINT     /api/v1/visits
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      Idempotency-Key: <UUID> (obligatorio); Content-Type: application/json
REQUEST      { "roomId": "<24 hex>", "scheduledAt": "2026-11-20T17:00:00-06:00", "message": "opcional, <= 300" } (transición: propertyId en lugar de roomId)
RESPONSE     201 { id, room: { id, buildingId, title, city }, property: { id, title, city } | null (transición), scheduledAt, timeZone, status, message, createdAt, updatedAt, cancelledAt } | 200 con Idempotent-Replayed: true
STATUS CODES 201, 200, 400, 401, 404 cuarto, 409 code=..., 422 code=..., 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/visits?status=requested&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      status (requested|cancelled), page, pageSize
RESPONSE     200 { items: [visita], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: sobre de NV-01; orden scheduledAt asc, id asc
```

```text
ENDPOINT     /api/v1/visits/{id} | /api/v1/visits/{id}/cancel
METHOD       GET | POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      id en la ruta; cancel: { reason? }
RESPONSE     200 visita
STATUS CODES 200, 400, 401, 404, 409 code=visit_in_past, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 42680187690): 738/738 (unitarias 251, integración 426, seguridad 61); prueba HTTP real en Azure 38/38 (2026-11-09)
```

## Historial — evidencia de 1.1.0

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «te apluebo o ejecuta para hacer el desarrollo de lo que nos pidió» — Victor Manuel Jose Jose, 2026-11-07: NV-17 1.1.0, tasks T01–T12 (M-BE-060) |
| Rama | `feat/nv-15-nv-17` → PR #15, mezclado en `main` (`32f9ea0`) |
| Resultado | **✅ PASS WITH WARNINGS**: 12/12 tasks completas; desplegada en Azure y verificada con prueba HTTP real (26/26). Ajustes A1–A4 aprobados (M-BE-061). Advertencia: adaptadores Mongo/Redis sin cobertura automatizada (verificados en Azure) |

### 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Visit.cs` | Estados (`requested`, `cancelled`; `confirmed`, `rejected`, `rescheduled`, `completed` reservados), `history`, `IsActive`, `Cancel` idempotente |
| Domain | `Exceptions/ConflictException.cs`, `BusinessRuleException.cs` | 409 y 422 con código estable (`Reason` / `Rule`) |
| Application | `Abstractions/Persistence/IVisitRepository.cs`, `Abstractions/Security/IUserLock.cs` | Puertos de visitas, idempotencia y lock por usuario |
| Application | `Abstractions/Persistence/IPropertyRepository.cs` | `GetByIdsAsync` (lectura por lotes, sin N+1) |
| Application | `Common/VisitSchedulePolicy.cs` | Desplazamiento obligatorio (400); 2 h–60 días, 08:00–20:00 `America/Mexico_City`, múltiplos de 15 min (422) |
| Application | `Common/RateLimitKeys.cs`, `UserRateLimitOptions.cs` | Llaves por hash del `sub`: 60/min por usuario, 10 creaciones por día, lock |
| Application | `Contracts/Visits/VisitContracts.cs` | Entrada estricta (`JsonUnmappedMemberHandling.Disallow`); salida con `property { id, title, city }` |
| Application | `UseCases/VisitService.cs` | Crear (lock + idempotencia + conflictos), consultar, listar, cancelar; ajena → 404 |
| Infrastructure | `Persistence/Mongo/MongoVisitRepository.cs` | `visits` (índices `{userId, scheduledAt, _id}` y `{userId, status, scheduledAt}`), `visit_idempotency` (único `{userId, key}`, TTL 24 h) |
| Infrastructure | `Security/RedisUserLock.cs` | `SET NX PX` con token; liberación por script Lua comparar-y-borrar; fallo cerrado |
| Infrastructure | `Persistence/Mongo/UnavailableFavoriteVisitRepositories.cs` | Sin MongoDB → 503 |
| API | `Http/VisitsEndpoint.cs`, `Http/UserEndpointSupport.cs`, `Http/UserResourceProblems.cs` | 4 endpoints; `Idempotency-Key`, `Idempotent-Replayed`, `no-store`, Problem Details en español con `code` |
| API | `Http/ProblemResponses.cs` | Título 422 «Solicitud no procesable»; normalización de favoritos y visitas |
| API | `Configuration/FavoriteVisitConfiguration.cs`, `Program.cs` | Registro y mapeo |

### 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas seguidas) | 553/553: unitarias 190, seguridad 52, integración 311 |

Pruebas nuevas de NV-17: `TheHouse.UnitTests/FavoriteVisitTests.cs` (política de horario con límites exactos, `Visit.Cancel`, `VisitService`) y `TheHouse.IntegrationTests/VisitsHttpTests.cs` (HTTP de punta a punta con dos usuarios). Los dobles en memoria (`FavoriteVisitTestDoubles.cs`) se comparten entre ambos proyectos.

### 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 | `Create_returns_201_with_contract_shape_in_utc`, `Create_rejects_bad_input_with_400`, `Create_rejects_unsupported_media_type_and_missing_property` |
| R02 | `EnsureAllowed_applies_lead_hours_and_quarter_rules`, `ParseScheduledAt_*`, `Schedule_rules_return_422_with_code` |
| R03 | `Cancel_moves_requested_to_cancelled_and_records_history`, `Cancel_rejects_past_visits_and_reserved_states` |
| R04 | `Cancel_is_idempotent_and_list_filters_by_status` (orden `scheduledAt` asc, filtro, 400), `Visit_list_validates_status_filter` |
| R05 | `Cancel_is_idempotent_and_list_filters_by_status`, `Visit_cancel_after_the_scheduled_time_is_a_conflict` |
| R06 | `Same_key_and_body_replays_and_different_body_conflicts`, `Visit_create_replays_same_key_and_rejects_reused_key` |
| R07 | `Conflicts_return_409_with_specific_codes`, `Visit_create_enforces_conflicts_and_active_limit`, `Concurrent_requests_with_different_keys_create_only_one_visit`, `Visit_create_fails_when_the_user_lock_is_held` |
| R08 | `Other_users_visit_is_404_for_get_and_cancel`, `Create_rejects_bad_input_with_400` (`userId`/`status` en el cuerpo → 400), `Daily_create_limit_returns_429_with_retry_after`, `Fails_closed_when_lock_or_storage_are_down`, `Requires_a_valid_bearer_token`, `Message_is_not_written_to_logs` |

### 4. Ajustes de implementación (aprobados: «Apruebo A1–A4» — Victor Manuel Jose Jose, 2026-11-07 (M-BE-061))

| # | Spec dice | Implementación | Motivo |
| --- | --- | --- | --- |
| A1 | §8/§9.4: índice único parcial `{userId, propertyId}` cuando `status = requested` | Lock por usuario en Redis (5 s) + verificación de visitas activas | Una visita `requested` con horario ya pasado seguiría ocupando el índice y bloquearía para siempre nuevas visitas a esa propiedad. El lock serializa la creación por usuario en todas las réplicas; la prueba concurrente lo demuestra. |
| A2 | §6: fechas devueltas como `...Z` | `...+00:00` (ISO 8601 UTC) | Igual que `createdAt` del resto de la API (registro, `/users/me`, propiedades); Mobile ya lo interpreta. |
| A3 | §11: `type`, `title`, `status`, `detail`, `instance`, `traceId` | Se agrega `code` en 409 y 422 (`schedule_overlap`, `too_soon`, …) | Mobile pidió errores consistentes; con `code` distingue el motivo sin analizar el texto. Es aditivo. |
| A4 | T11: evidencia en `TheHouse.SecurityTests` | Casos de seguridad en `TheHouse.IntegrationTests` | Necesitan el host HTTP completo; mismo criterio que `PropertySecurityTests` y `UniversitySecurityTests`. |

### 5. Seguridad y coherencia

- El usuario sale del `sub` validado por `TokenRevocationMiddleware`; ninguna ruta, query ni cuerpo acepta `userId`.
- Una visita ajena responde 404, igual que una inexistente.
- La entrada es estricta: campos desconocidos → 400. Las respuestas no incluyen la descripción ni la dirección de la propiedad.
- Rate limit, lock y repositorios fallan cerrados con 503.
- `Idempotency-Key` se guarda con el hash SHA-256 del cuerpo, nunca con el cuerpo.
- Sin logs nuevos; la prueba de logs confirma que el mensaje no aparece.
- Costo: sin servicios nuevos. Usa dos colecciones en Atlas M0 y llaves con TTL en Upstash.
- Coherencia: constitution §6 (módulo dueño de sus colecciones), §14 (sin estado local; lock distribuido), AGENTS (TypeScript no aplica; estilo .NET existente).

### 6. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PR | [#15](https://github.com/thehouse-team/TheHouse-Backend/pull/15), mezclado el 2026-11-07 (commit `32f9ea0`); aprobación registrada por la cuenta `thehouserent26` |
| CI del PR (`33c8126`) y de `main` (`32f9ea0`) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI (`main`, run 42354786972) | 553/553: unitarias 190, integración 311, seguridad 52; 34 pruebas de NV-15/NV-17 en integración y 33 en unitarias |
| Cobertura de líneas (CI) | 70.3 % total. Los adaptadores `MongoFavoriteRepository`, `MongoVisitRepository` y `RedisUserLock` quedan en 0 %, como los adaptadores Mongo/Redis existentes: se verifican con la prueba real en Azure |
| CD (run 42354881476) | resolve, publish, verify-ephemeral (Mongo 7.0.45, Redis 7.4.12, smoke, ZAP), deploy aprobado por Victor, DAST: todo en verde |
| Azure | revisión `ca-thehouse-u2--c32f9ea0-13-1`, imagen `sha256:27b71e88…` |

### 7. Prueba HTTP real en Azure

Prueba HTTP real contra `https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io` (2026-11-07), con dos cuentas nuevas de dominio reservado (`nv15-17-azure-a-3a9876f4@example.com` y `nv15-17-azure-b-cabd2770@example.com`). El script (`nv15_17_azure_check.py`, fuera del repo) solo imprime códigos y nombres de campos; nunca tokens ni contraseñas. **26/26 PASS**: favoritos 10, visitas 14, compatibilidad 2 (`/users/me` y detalle de propiedad sin cambios).

Una corrida anterior dio 25/26: el script usaba como id inexistente `000000000000000000000001`, que es la propiedad 1 del seed regional, así que el 204 era correcto. Se corrigió el script para usar un id que antes comprueba con `GET /properties/{id}` → 404. Esa corrida dejó un favorito de la propiedad 1 en la cuenta de prueba `nv15-17-azure-a-ccbfd189@example.com`.

Quedan en producción cuatro cuentas de prueba con prefijo `nv15-17-azure-` y dominio `example.com`, sin datos personales; sus sesiones se cerraron con logout. Se eliminarán cuando exista la eliminación de cuenta.

| Caso | Resultado |
| --- | --- |
| V1 crear | 201; campos del contrato; `status=requested`; `scheduledAt` en UTC; `no-store` |
| V2 repetir misma llave y cuerpo | 200, `Idempotent-Replayed: true`, mismo id |
| V3 misma llave, otro cuerpo | 409 `idempotency_key_reused` |
| V4 otra visita a la misma propiedad | 409 `active_visit_for_property` |
| V5 fuera de horario | 422 `outside_hours`, título «Solicitud no procesable» |
| V6 `userId` en el cuerpo | 400 |
| V7 sin `Idempotency-Key` | 400 |
| V8–V9 listar y consultar propias | 200 |
| V10–V11 otro usuario consulta o cancela | 404 (BOLA) |
| V12–V13 cancelar y repetir | 200, `cancelled`, `cancelledAt` presente |
| V14 sin token | 401 |

### 8. Contrato entregable a Mobile

El contrato de `spec.md` §6.1 queda vigente sin cambios de forma, con estos dos campos completos:

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        unitarias 190, integración 311, seguridad 52 en CI (553/553); prueba HTTP real en Azure 26/26 (2026-11-07)
```
