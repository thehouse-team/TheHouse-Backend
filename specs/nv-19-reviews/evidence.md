# NV-19 — Evidencia: reseñas y calificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-13 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-13: NV-19 1.1.0, tasks T01–T10 (M-BE-072) |
| Rama | `feat/nv-19-reviews` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T06 completas y verificadas en local; T07–T10 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Review.cs` (nuevo) | `Review` con 9 criterios (`LandlordScores`, `PropertyScores` y sus promedios a un decimal), estados `published`/`hidden`, `Edit` (30 días), `Hide`, `Unhide`; `ReviewAverages` |
| Application | `Abstractions/Persistence/IReviewRepository.cs` (nuevo) | Alta con único por autor y cuarto, reemplazo condicional, listados y promedios por cuarto y por arrendador |
| Application | `Contracts/Reviews/ReviewContracts.cs` (nuevo) | Entradas con `JsonUnmappedMemberHandling.Disallow`; respuestas del §6.1 |
| Application | `UseCases/ReviewServices.cs` (nuevo) | `ReviewRules` (criterios, comentario, «visitó»), `RoomReviewService` (elegibilidad, crear, editar, listar con resumen y mis reseñas) y `ReviewModerationService` (ocultar y mostrar con rol `admin` y bitácora) |
| Application | `UseCases/ModerationServices.cs` (NV-21) | Objetivo de reporte `review`; acción `hide_review` al resolver; `suspend_user` sobre una reseña suspende a su autor; parámetros opcionales al final del constructor de `ReportService` |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | 60/min por usuario y 10 reseñas nuevas al día |
| Infrastructure | `Persistence/Mongo/MongoReviewRepository.cs` (nuevo) | Colección `reviews`; índices `ux_reviews_author_room`, `ix_reviews_room_status_created`, `ix_reviews_owner_status`, `ix_reviews_author_created`; promedios con agregación |
| API | `Http/ReviewsEndpoint.cs` (nuevo), `Configuration/ReviewsConfiguration.cs` (nuevo), `Program.cs` | 7 endpoints; `AddReviews()` y `MapReviews()` |
| API | `Http/ProblemResponses.cs`, `Http/UserResourceProblems.cs` | Mensajes de reseñas; códigos `no_stay`, `review_exists`, `review_locked`, `review_state`; el texto de `own_room` ahora cubre solicitudes y reseñas (mismo código) |
| Pruebas | `UnitTests/ReviewTests.cs`, `IntegrationTests/ReviewsHttpTests.cs`, `SecurityTests/ReviewSecurityTests.cs`, `IntegrationTests/ReviewTestDoubles.cs` | 10 unitarias, 4 de integración y 9 de seguridad; doble en memoria; las listas de rutas OpenAPI agregan las 7 operaciones |

## 2. Comandos y resultados (local, 2026-11-13)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 916/916 en ambas: unitarias 347, seguridad 107, integración 462 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Elegibilidad verificada | `Eligibility_requires_a_past_confirmed_visit_or_a_rental`, `Create_validates_criteria_and_allows_one_review_per_room`, `Only_students_who_stayed_can_review_and_only_the_author_edits` |
| R02 Crear y editar | `Averages_round_to_one_decimal_and_edits_close_after_30_days`, `Scores_must_be_1_to_5` (3 casos), `Create_validates_criteria_and_allows_one_review_per_room`, `Only_the_author_edits_within_30_days`, `Student_who_visited_reviews_edits_and_sees_the_summary` |
| R03 Listado y resumen | `List_shows_published_reviews_with_room_and_landlord_averages`, `Student_who_visited_reviews_edits_and_sees_the_summary` |
| R04 Mis reseñas | `My_reviews_include_hidden_ones_with_their_status`, `Reviews_are_reported_and_hidden_by_an_admin` |
| R05 Moderación | `Hide_and_unhide_follow_the_state`, `Reviews_are_reported_and_hidden_by_an_admin`, `Authors_appear_only_by_first_name_and_reviews_can_be_reported` |
| R06 Privacidad, autorización y límites | `Only_students_who_stayed_can_review_and_only_the_author_edits`, `Dependencies_fail_closed`, `Review_inputs_reject_unknown_fields`, `Review_codes_have_a_spanish_detail`, `Authors_appear_only_by_first_name_and_reviews_can_be_reported` |
| R07 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R08 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **Elegibilidad en el servidor:** «visitó» = visita `confirmed` del token a ese cuarto con horario pasado; «rentó» = convenio `active` o `ended` (`IRentalHistoryQuery`). El cliente no manda nada de eso.
- **BOLA:** solo el autor edita (lo ajeno 404); el arrendador no califica sus cuartos (403 `own_room`); ocultar y mostrar exigen rol `admin` (403 `role_required`).
- **Privacidad:** el autor aparece solo con su primer nombre o «Estudiante»; las respuestas no traen ids de autor ni de arrendador, correo ni teléfono; los comentarios no se registran en logs.
- **Moderación:** el reporte con objetivo `review` y la acción `hide_review` pasan por la bitácora de NV-21; una reseña oculta deja de contar en los promedios y el autor la ve como `hidden`.
- **Asignación masiva:** las tres entradas rechazan campos desconocidos (`authorId`, `status` → 400).
- **Fallo cerrado:** MongoDB o Redis caídos → 503.
- **Coherencia:** las rutas de NV-12 a NV-20 no cambian; el cambio en NV-21 es aditivo (un objetivo y una acción nuevos, rutas nuevas bajo `/admin/reviews`).

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Nombre de la clase | `RoomReviewService`, porque `ReviewService` ya existe en NV-21 (revisión de edificios) |
| Criterios del resumen | `summary.criteria` son los promedios del cuarto; `summary.landlord` usa todas las reseñas publicadas del arrendador |
| «Visitó» con muchas visitas | Se revisan las visitas confirmadas del estudiante en páginas de 50 (hasta 1000) |
| Reporte de reseña | `suspend_user` sobre una reseña suspende a su autor; `suspend_building` no aplica a reseñas (400) |
| Texto de `own_room` | «No puedes enviar una solicitud ni calificar un cuarto propio.» (el código no cambia; ninguna prueba dependía del texto) |

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv19_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
