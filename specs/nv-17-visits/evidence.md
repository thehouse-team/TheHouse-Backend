# NV-17 — Evidencia: visitas v1

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-07 |
| Autorización | «te apluebo o ejecuta para hacer el desarrollo de lo que nos pidió» — Victor Manuel Jose Jose, 2026-11-07: NV-17 1.1.0, tasks T01–T12 (M-BE-060) |
| Rama | `feat/nv-15-nv-17` (junto con NV-15; base `main` 546f506) |
| Resultado | **🟡 EN CURSO**: T01–T08 completas con pruebas locales; T09–T12 esperan CI, despliegue por CD y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. No se registran mensajes de visitas, tokens ni ids de usuario en logs.

## 1. Cambios por capa

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

## 2. Comandos y resultados (local, 2026-11-07)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` (dos corridas seguidas) | 553/553: unitarias 190, seguridad 52, integración 311 |

Pruebas nuevas de NV-17: `TheHouse.UnitTests/FavoriteVisitTests.cs` (política de horario con límites exactos, `Visit.Cancel`, `VisitService`) y `TheHouse.IntegrationTests/VisitsHttpTests.cs` (HTTP de punta a punta con dos usuarios). Los dobles en memoria (`FavoriteVisitTestDoubles.cs`) se comparten entre ambos proyectos.

## 3. Trazabilidad requisito → prueba

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

## 4. Ajustes de implementación (requieren visto bueno)

| # | Spec dice | Implementación | Motivo |
| --- | --- | --- | --- |
| A1 | §8/§9.4: índice único parcial `{userId, propertyId}` cuando `status = requested` | Lock por usuario en Redis (5 s) + verificación de visitas activas | Una visita `requested` con horario ya pasado seguiría ocupando el índice y bloquearía para siempre nuevas visitas a esa propiedad. El lock serializa la creación por usuario en todas las réplicas; la prueba concurrente lo demuestra. |
| A2 | §6: fechas devueltas como `...Z` | `...+00:00` (ISO 8601 UTC) | Igual que `createdAt` del resto de la API (registro, `/users/me`, propiedades); Mobile ya lo interpreta. |
| A3 | §11: `type`, `title`, `status`, `detail`, `instance`, `traceId` | Se agrega `code` en 409 y 422 (`schedule_overlap`, `too_soon`, …) | Mobile pidió errores consistentes; con `code` distingue el motivo sin analizar el texto. Es aditivo. |
| A4 | T11: evidencia en `TheHouse.SecurityTests` | Casos de seguridad en `TheHouse.IntegrationTests` | Necesitan el host HTTP completo; mismo criterio que `PropertySecurityTests` y `UniversitySecurityTests`. |

## 5. Seguridad y coherencia

- El usuario sale del `sub` validado por `TokenRevocationMiddleware`; ninguna ruta, query ni cuerpo acepta `userId`.
- Una visita ajena responde 404, igual que una inexistente.
- La entrada es estricta: campos desconocidos → 400. Las respuestas no incluyen la descripción ni la dirección de la propiedad.
- Rate limit, lock y repositorios fallan cerrados con 503.
- `Idempotency-Key` se guarda con el hash SHA-256 del cuerpo, nunca con el cuerpo.
- Sin logs nuevos; la prueba de logs confirma que el mensaje no aparece.
- Costo: sin servicios nuevos. Usa dos colecciones en Atlas M0 y llaves con TTL en Upstash.
- Coherencia: constitution §6 (módulo dueño de sus colecciones), §14 (sin estado local; lock distribuido), AGENTS (TypeScript no aplica; estilo .NET existente).

## 6. Pendiente

- T09: CI verde, merge, CD aprobado y prueba HTTP real en Azure (crear, repetir con la misma llave, conflicto, listar, consultar, cancelar); entregar el contrato §6.1 con `DEPLOYED URL` y `TESTS`.
- T10/T11: TRX y cobertura del run de CI.
- T12: cierre de esta evidencia con commit del merge y resultado final.
