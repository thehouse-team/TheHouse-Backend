# NV-20 — Evidencia: notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-12 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-12: NV-20 1.1.0, tasks T01–T11 (M-BE-071) |
| Rama | `feat/nv-20-notifications` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T07 completas y verificadas en local; T08–T11 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Notification.cs` (nuevo) | `Notification`, `NotificationCounts`, categorías, destinos y catálogo cerrado de 16 tipos |
| Application | `Abstractions/Notifications/INotificationSink.cs` (nuevo) | Puerto `NotifyAsync` / `ClearAsync` y `NotificationEvent` (destinatario, destino, `dedupeKey`, ids para el texto) |
| Application | `Abstractions/Persistence/INotificationRepository.cs` (nuevo) | Alta idempotente con agrupación, bandeja, contador por categoría y marcas de leído |
| Application | `Contracts/Notifications/NotificationContracts.cs` (nuevo) | Respuestas del §6.1; `ReadAllNotificationsRequest` con `JsonUnmappedMemberHandling.Disallow` |
| Application | `UseCases/NotificationServices.cs` (nuevo) | `NotificationTemplates` (textos en español, fecha en Ciudad de México), `NotificationPublisher` (arma y guarda) y `NotificationService` (bandeja, contador y marcas) |
| Application | `UseCases/MessagingServices.cs`, `VisitService.cs`, `LandlordVisitService.cs`, `RentalServices.cs`, `ModerationServices.cs` | Parámetro opcional `INotificationSink` al final del constructor; aviso a la otra parte después de guardar; leer el chat limpia el aviso de esa conversación |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | 60/min en bandeja y marcas, 30/min en el contador, por usuario y con hash |
| Infrastructure | `Persistence/Mongo/MongoNotificationRepository.cs` (nuevo) | Colección `notifications`; índices `ux_notifications_unread_user_key` (único parcial `unread: true`), `ix_notifications_user_updated`, `ix_notifications_user_unread_category` y `ttl_notifications_created` (90 días) |
| API | `Http/NotificationsEndpoint.cs` (nuevo), `Configuration/NotificationsConfiguration.cs` (nuevo), `Program.cs` | 4 endpoints; `SafeNotificationSink` atrapa y registra fallas solo con el tipo |
| API | `Http/ProblemResponses.cs`, `Http/UserResourceProblems.cs` | Mensajes en español de la bandeja |
| Pruebas | `UnitTests/NotificationTests.cs`, `IntegrationTests/NotificationsHttpTests.cs`, `SecurityTests/NotificationSecurityTests.cs`, `IntegrationTests/NotificationTestDoubles.cs` | 10 unitarias, 4 de integración y 3 de seguridad; doble en memoria y puerto que registra; las listas de rutas OpenAPI agregan las 4 operaciones |

## 2. Comandos y resultados (local, 2026-11-12)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 893/893 en ambas: unitarias 337, seguridad 98, integración 458 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Generación desde las acciones | `Actions_create_notifications_for_the_other_party` (revisión de edificio, solicitud, aceptación, chat y visita reales), `Messaging_notifies_the_other_party_and_never_the_actor`, `Landlord_visit_actions_notify_the_student`, `A_failing_store_does_not_break_the_action_and_the_inbox_fails_closed` |
| R02 Textos sin datos sensibles | `Every_template_renders_in_spanish_with_the_local_time`, `Publisher_uses_first_names_and_never_contact_or_address`, `Templates_only_use_the_given_name_and_place`, `Actions_create_notifications_for_the_other_party` |
| R03 Bandeja y filtros | `Inbox_filters_counts_and_marks_as_read`, `Inbox_validates_filters` (2 casos), `Notifications_are_private_and_inputs_are_strict` |
| R04 Marcar como leídas | `Inbox_filters_counts_and_marks_as_read`, `Counter_supports_etag_and_marks_change_it`, `Invalid_ids_and_categories_are_rejected`, `Actions_create_notifications_for_the_other_party` (leer el chat) |
| R05 Contador eficiente | `Counter_supports_etag_and_marks_change_it` |
| R06 Agrupación y retención | `Same_fact_is_not_duplicated_and_messages_are_grouped`, `Catalog_has_sixteen_types_in_three_categories`; TTL creado en `MongoNotificationRepository` (se verifica en Azure) |
| R07 Privacidad, autorización y límites | `Notifications_are_private_and_inputs_are_strict`, `Read_all_rejects_unknown_fields`, `Responses_never_expose_user_ids_or_dedupe_keys`, `A_failing_store_does_not_break_the_action_and_the_inbox_fails_closed` |
| R08 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R09 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **BOLA:** toda consulta y marca filtra por el `sub`; marcar una ajena responde 404.
- **RNF-02:** las plantillas solo reciben el primer nombre, el título del cuarto o el nombre del edificio y la fecha; nunca dirección, teléfono, correo, texto del chat ni notas. Las respuestas no traen `userId` ni `dedupeKey`.
- **Contador:** `ETag` débil con `Cache-Control: private, no-cache` y 304; ninguna caché compartida lo guarda (constitution §23).
- **Fallo aislado:** si guardar el aviso falla, la acción principal responde igual y se registra una advertencia solo con el tipo de aviso; la bandeja con MongoDB caído responde 503 y con Redis caído también.
- **Asignación masiva:** `read-all` rechaza campos desconocidos.
- **Coherencia:** las rutas y respuestas de NV-12 a NV-18 no cambian; los emisores reciben el puerto como parámetro opcional, así que las pruebas anteriores no cambiaron (suite completa en verde).

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Inyección del puerto | Parámetro opcional al final del constructor de cada emisor: la DI lo inyecta y las pruebas existentes siguen sin cambios |
| `ETag` del contador | Se calcula con los cuatro conteos; cambia en cuanto cambia cualquier no leído |
| Nombre del actor | Primer nombre del perfil (estudiante) o del perfil de arrendador; sin nombre → «Un estudiante» |
| `dedupeKey` | `tipo:id` del recurso; `visit_rescheduled` agrega la fecha del cambio y los avisos de edificio agregan la versión, para que cada hecho nuevo cuente |

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv20_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
