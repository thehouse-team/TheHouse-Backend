# NV-25 — Evidencia: estadísticas del arrendador

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-25 1.1.0, tasks T01–T09 (M-BE-075) |
| Rama | `feat/nv-25-landlord-stats` → PR #59, merge `aeb40ef` |
| Resultado | **✅ PASS WITH WARNINGS** — T01–T09 completas: CI 981/981, desplegado en `ca-thehouse-u2--caeb40ef-54-1`, prueba HTTP real en Azure 26/27 (la falla es de la expectativa del script, H1) |

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
| R06 Contrato para Mobile y despliegue | CD run 44222853524; prueba HTTP real en Azure 26/27 (§6, H1); contrato entregado (§7) |
| R07 Pruebas, seguridad y evidencia | TRX del CI run 44222420020: 981/981; `TheHouse.SecurityTests` 130/130; cobertura en §5; esta evidencia |

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

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#58](https://github.com/thehouse-team/TheHouse-Backend/pull/58) (spec 1.1.0) y [#59](https://github.com/thehouse-team/TheHouse-Backend/pull/59) (aprobación e implementación), mezclados por `IsaiAxel` (merge `aeb40ef`) |
| CI de `main` (`aeb40ef`, run 44222420020) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 981/981: unitarias 379, integración 472, seguridad 130 |
| Cobertura de líneas (CI) | 68.5 % total (ramas 72.0 %). `LandlordStatsServices` 98.9 %, `LandlordStatsEndpoint` 100 %, `Analytics` 100 %, `SearchService` 97.9 %, `AnalyticsConfiguration` 81.0 %. `MongoRoomViewRepository` y `RedisViewSaltStore` en 0 %, como los demás adaptadores: se verifican en Azure |
| CD (run 44222853524) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--caeb40ef-54-1`, Healthy, 100 % del tráfico |
| Rol admin | Asignado con «autorizo rol» de Victor a la cuenta de prueba `nv25-azure-admin-…@example.com` solo para aprobar y suspender el edificio de prueba; retirado al terminar la corrida (2026-11-17) |

## 6. Prueba HTTP real en Azure

`.azure-check/nv25_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-17 con cuatro cuentas `@example.com` nuevas (dos estudiantes, un arrendador y un admin temporal); solo se imprimen códigos, campos y conteos, nunca tokens, contraseñas, documentos ni el texto de los mensajes. Resultado: **26/27 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Preparación | Crear edificio 201; enviar a revisión respondió **202** (el script esperaba 200, ver H1); el admin aprueba 200 | 2/3 |
| Acceso | Estudiante sin modo arrendador 403 `landlord_required`; resumen inicial 200 con 0 vistas y `no-store` | 2/2 |
| Actividad | Detalle del cuarto 200 visto por el estudiante (dos veces), otro estudiante y el propio arrendador; favorito 204; solicitud 201; visita 201 | 7/7 |
| Estadísticas | Resumen con los campos del §6.1; 2 vistas (sin repetidas ni la del dueño), 1 favorito, 1 solicitud, 1 visita; conversión 0.5 y serie de 30 días con 2 vistas hoy; `no-store`; por cuarto con los campos del §6.1 y 2/1/1; sugerencias `add_photos` y `missing_details`; sin datos de los estudiantes; edificio ajeno 404; rango de más de 90 días 400 | 11/11 |
| Limpieza | Cancelar solicitud 200 y visita 200; quitar favorito 204; suspender el edificio 200 | 4/4 |

## 7. Contrato entregable a Mobile

Entregado el 2026-11-17 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 3 rutas son nuevas y ninguna respuesta existente cambia (el detalle del cuarto solo registra la vista). Códigos de sugerencia: `pending_requests`, `pending_visits`, `add_photos` (prioridad `high`); `price_above_median`, `missing_details`, `views_without_requests` (prioridad `medium`).

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44222420020): 981/981 (unitarias 379, integración 472, seguridad 130); prueba HTTP real en Azure 26/27, la única falla es de la expectativa del script (2026-11-17)
```

## 8. Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | P2 de la prueba en Azure esperaba 200 al enviar a revisión, pero el contrato de NV-12 es 202 Accepted (lo fijan 4 pruebas de integración) y P3 aprobó el edificio, así que el envío funcionó | Información | Error del script, no del API: se corrigió la expectativa a 202 |
| H2 | Adaptadores Mongo y Redis sin cobertura en CI | Baja | Aceptado: se verifican con la prueba real en Azure (vistas únicas por persona y día, conteos y sal del día) |
| H3 | Las estadísticas juntan la actividad del arrendador en memoria (dentro de los límites de paginación de cada repositorio) | Baja | Aceptado para el volumen del piloto; si crece, se pasa a agregaciones en la base sin cambiar el contrato |
| H4 | Las cuentas y el edificio de prueba (suspendido) quedan en Production | Información | Intencional, como en specs anteriores: solicitud y visita canceladas, favorito quitado, cuentas `@example.com` y sin rol admin |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos; una colección `room_views` con tres índices (uno TTL de 400 días) en MongoDB Atlas M0 y una llave diaria en Upstash Redis, dentro de los planes gratuitos. Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 1.1.0 completa 9/9; las rutas existentes no cambiaron su contrato.
