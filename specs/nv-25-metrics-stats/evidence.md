# NV-25 — Evidencia: estadísticas del arrendador

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-25 1.1.0, tasks T01–T09 (M-BE-075) |
| Rama | `feat/nv-25-landlord-stats` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T05 completas y verificadas en local; T06–T09 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Analytics.cs` (nuevo) | `RoomView` (una vista por persona, cuarto y día con `actorHash`) y `RoomViewCount` |
| Application | `Abstractions/Persistence/IRoomViewRepository.cs`, `Abstractions/Analytics/IAnalyticsPorts.cs` (nuevos) | Puerto de `room_views`, `IRoomViewSink` (el detalle avisa la vista) e `IViewSaltStore` (sal del día) |
| Application | `Abstractions/Persistence/IFavoriteRepository.cs` (NV-15) | `ListByRoomsAsync` para contar guardados por cuarto |
| Application | `Contracts/Analytics/LandlordStatsContracts.cs` (nuevo) | Respuestas del §6.1: resumen, por cuarto y sugerencias |
| Application | `UseCases/LandlordStatsServices.cs` (nuevo) | `LandlordStatsRules` (rango, conversión, mediana, hash, umbrales), `RoomViewService` (registro sin dueño ni staff) y `LandlordStatsService` (resumen, por cuarto y 6 sugerencias) |
| Application | `UseCases/SearchService.cs` (NV-14) | `DetailAsync` con `viewerId` registra la vista sin cambiar la respuesta |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | 30/min por usuario en estadísticas |
| Infrastructure | `Persistence/Mongo/MongoRoomViewRepository.cs` (nuevo), `MongoFavoriteRepository.cs` | `room_views` con `ux_room_views_room_day_actor`, `ix_room_views_owner_day` y `ttl_room_views_created` (400 días); favoritos por cuarto |
| Infrastructure | `Security/RedisViewSaltStore.cs` (nuevo) | Sal aleatoria del día con `SET NX` y 48 h |
| API | `Http/LandlordStatsEndpoint.cs`, `Configuration/AnalyticsConfiguration.cs` (nuevos), `Program.cs`, `Http/SearchEndpoint.cs` | 3 rutas; `LoggingRoomViewSink` absorbe cualquier fallo del registro; el detalle pasa el usuario del token |
| API | `Http/ProblemResponses.cs`, `UserResourceProblems.cs` | Mensajes en español de estadísticas |
| Pruebas | `UnitTests/LandlordStatsTests.cs`, `IntegrationTests/LandlordStatsHttpTests.cs`, `SecurityTests/LandlordStatsSecurityTests.cs`, `IntegrationTests/AnalyticsTestDoubles.cs` | 14 unitarias, 3 de integración y 7 de seguridad; listas OpenAPI con las 3 rutas |

## 2. Comandos y resultados (local, 2026-11-17)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx --configuration Release` (dos corridas seguidas, como el CI) | 981/981 en ambas corridas: unitarias 379, seguridad 130, integración 472; 0 fallas |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Registro de vistas | `One_view_per_person_and_day_without_owner_or_staff`, `Student_activity_appears_in_the_landlord_stats` (dos vistas de la misma persona cuentan una; el dueño no cuenta), `Views_never_break_the_room_detail_and_stats_fail_closed` |
| R02 Resumen del arrendador | `Summary_counts_activity_in_range_previous_period_and_daily_series`, `Range_defaults_to_30_days_and_accepts_up_to_90`, `Range_rejects_invalid_or_too_long` (5 casos), `Student_activity_appears_in_the_landlord_stats` |
| R03 Estadísticas por cuarto | `Rooms_are_ordered_by_views_and_paginated`, `Student_activity_appears_in_the_landlord_stats` (filtro por edificio) |
| R04 Sugerencias por reglas | `Recommendations_fire_only_when_their_rule_applies` (5 reglas se disparan; un cuarto completo y en la mediana no), `Pending_requests_are_flagged_after_48_hours` (47 h no, 49 h sí) |
| R05 Privacidad, autorización y límites | `Stats_are_only_for_landlords_and_their_buildings` (403 `landlord_required`, 404 ajeno, 400, 401), `Stats_responses_never_carry_people`, `Actor_hash_depends_on_the_daily_salt_and_hides_the_user`, `Stats_are_bounded_and_explained_in_spanish`, 503 en `Views_never_break...` |
| R06 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R07 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **BOLA:** todo se filtra por el dueño del token; un `buildingId` ajeno o inexistente responde 404.
- **RNF-11:** las respuestas solo tienen agregados; `actorHash` es HMAC-SHA256 con una sal aleatoria del día que vence en 48 h, así que no se puede revertir ni unir entre días.
- **Disponibilidad:** el registro de la vista nunca rompe el detalle del cuarto (Redis o MongoDB caídos → 200 sin vista y una advertencia sin datos del usuario); las estadísticas fallan cerrado con 503.
- **Dos réplicas:** sin tarea diaria; la llave única de `room_views` hace idempotente el registro y la sal del día se comparte en Redis.
- **Coherencia:** el detalle del cuarto y las demás rutas no cambian su contrato; `IFavoriteRepository` solo agrega un método.

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Vistas del edificio | `GET /properties/{id}` es el catálogo heredado de NV-07 (sin dueño de NV-12), así que no se registra; el detalle del cuarto (con sesión, RF-24) ya identifica el edificio |
| Persona | El detalle del cuarto exige sesión, así que la persona es siempre el usuario del token (no se usa la IP) |
| Solicitudes aceptadas | Se cuentan por la fecha de respuesta (`updatedAt` con estado `accepted`) |
| Favoritos | `favorites` son los guardados nuevos del rango que siguen guardados; `favoritesNow`, todos los actuales |

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv25_azure_check.py`, con rol admin temporal) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
