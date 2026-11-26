# Mapa de módulos del monolito modular

NV-01 1.3.0-u2 (T13). Constitución §6: cada módulo es dueño exclusivo de sus casos de uso y sus colecciones; los demás módulos solo lo usan por contratos (puertos de `Application/Abstractions`) o por un servicio público, sin ciclos. Este mapa lo verifica `TheHouse.IntegrationTests/ArchitectureTests.cs`: si se agrega un repositorio, una colección o un servicio sin asignarlo aquí y en la prueba, el CI falla.

## Módulos

| Módulo | Specs | Colecciones (dueño exclusivo) | Componentes de `Application/UseCases` |
| --- | --- | --- | --- |
| Identity | NV-04, NV-05, NV-06, NV-10 | `users`, `refresh_tokens`, `deleted_accounts` | `RegistrationService`, `LoginService`, `LogoutService`, `SessionService`, `AccountService`, `AccountDeletionService` |
| Profile | NV-11 | `user_profiles`, `user_preferences` | `ProfileService`, `PreferencesService`, `ProfileDeletionParticipant` |
| Catalog | NV-07, NV-08 | `universities`, `properties` | `UniversityQueryService`, `UniversitySearchService`, `UniversitySeedService`, `PropertyQueryService`, `PropertySeedService` |
| Listings | NV-12 | `landlord_profiles`, `buildings`, `rooms` | `LandlordService`, `BuildingService`, `RoomService`, `ListingsDeletionParticipant` |
| Media | NV-13 | `media_items`, `verification_documents` (y sus buckets de GridFS) | `MediaService`, `MediaPort` |
| Search | NV-14 | `places` | `SearchService`, `RoomCatalog`, `PlaceSeedService`, `DemoRoomSeedService` |
| Favorites | NV-15 | `favorites` | `FavoriteService`, `RoomMigrationService`, `FavoritesDeletionParticipant` |
| Messaging | NV-16 | `conversations`, `messages`, `rental_requests` | `ConversationService`, `RentalRequestService`, `MessagingDeletionParticipant` |
| Visits | NV-17 | `visits`, `visit_idempotency` | `VisitService`, `LandlordVisitService`, `VisitsDeletionParticipant` |
| Rentals | NV-18 | `agreements` | `AgreementService`, `RentalHistoryQuery`, `RentalsDeletionParticipant` |
| Reviews | NV-19 | `reviews` | `RoomReviewService`, `ReviewsDeletionParticipant` |
| Notifications | NV-20 | `notifications`, `push_devices` | `NotificationService`, `NotificationPublisher`, `PushDeviceService`, `PushDispatcher`, `NotificationsDeletionParticipant` |
| Moderation | NV-21 | `staff_roles`, `audit_events`, `verification_reviews`, `reports`, `user_suspensions`, `moderation_rejections` | `StaffService`, `ModerationService`, `ReportService`, `ReviewService`, `ReviewModerationService`, `ContentModerationService`, `ModerationRejectionService`, `AccountSuspensionQuery`, `ModerationDeletionParticipant` |
| Roommates | NV-22 | `roommate_profiles` | `RoommateService`, `RoommatesDeletionParticipant` |
| SmartMatch | NV-23, NV-24 | `smart_match_feedback`, `smart_match_dismissals`, `smart_match_daily_stats`, `smart_match_explanations` | `SmartMatchService`, `SmartMatchExplanationService`, `SmartMatchDeletionParticipant` |
| Analytics | NV-25 | `room_views` | `RoomViewService`, `LandlordStatsService`, `AnalyticsDeletionParticipant` |

NV-26 (script de base de datos) y NV-01 a NV-03 y NV-09 (base, infraestructura y CD) no tienen colecciones propias.

## Dependencias entre módulos

Un módulo puede **leer** datos de otro solo por los puertos de `Application/Abstractions` (por ejemplo, `IRoomRepository` o `IContentModerator`); esos puertos son el contrato público del módulo dueño y no crean ciclos de compilación. Las dependencias de **servicio a servicio** entre módulos son pocas y forman un grafo sin ciclos:

| Desde | Hacia | Por qué |
| --- | --- | --- |
| Media | Moderation | `MediaService` → `StaffService`: los revisores ven documentos privados |
| Moderation | Identity | `ModerationService` → `SessionService`: suspender una cuenta cierra sus sesiones |
| Moderation | Listings | `ReviewService` → `BuildingService`: aprobar o rechazar un edificio |
| Favorites | Search | `FavoriteService` y `RoomMigrationService` → `RoomCatalog`: solo cuartos publicados |
| Visits | Search | `VisitService` → `RoomCatalog` |
| Roommates | Messaging | `RoommateService` → `ConversationService`: contactar abre una conversación |
| SmartMatch | Search | `SmartMatchService` → `SearchService`: candidatos de la búsqueda |
| SmartMatch | Moderation | `SmartMatchService` → `StaffService`: métricas solo para `admin` |

Decisión de asignación: `ReviewModerationService` (ocultar o mostrar una reseña, NV-19 en NV-21) pertenece a **Moderation**: usa el puerto de Reviews (`IReviewRepository`) y el registro de staff, como dice su propio comentario. En Reviews formaría un ciclo Reviews ↔ Moderation.

## Reglas que verifica el CI (NV-01-R13)

| Regla | Qué falla |
| --- | --- |
| R1 | Una referencia entre proyectos distinta a la de la constitución §7 |
| R2 | `Domain` o `Application` con un paquete de infraestructura (MongoDB, Redis, ONNX Runtime, Azure, SkiaSharp, Firebase, ASP.NET Core) |
| R3 | Una colección pedida por repositorios de dos módulos, o una colección del esquema (NV-26) sin módulo dueño |
| R4 | Un ciclo en las dependencias de servicio a servicio entre módulos |
| R5 | Un tipo de `TheHouse.Http` que use tipos de `TheHouse.Infrastructure` (la composición vive en `TheHouse.Configuration`) |
| R6 | Un repositorio, servicio o colección nuevos sin módulo en este mapa |
