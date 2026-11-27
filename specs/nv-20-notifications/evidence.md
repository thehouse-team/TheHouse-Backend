# NV-20 — Evidencia: notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `evidence.md` |
| Versión | 1.3.0 (1.2.0 y 1.1.0 abajo) |
| Fecha | 2026-11-27 |
| Autorización | 1.3.0: «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-27: T09–T12, con el §6.1 de 1.3.0 validado antes por Mobile. 1.2.0: «Ya lo validó, apruebo y EJECUTA NV-20» — Victor Manuel Jose Jose, 2026-11-18: tasks T01–T08 (M-BE-078), con el contrato §6.1 validado antes por Mobile. 1.1.0: «Apruebo y EJECUTA», 2026-11-12 (M-BE-071) |
| Rama | 1.3.0: `feat/nv-20-avisos-1-3-0`. 1.2.0: `feat/nv-20-push-fcm` → PR #65, merge `7c24535`. 1.1.0: `feat/nv-20-notifications` (PR #46) |
| Resultado | **1.3.0: T09–T11 PASS en local (1,401/1,401); T12 pendiente del despliegue.** **🟡 PASS WITH WARNINGS (1.2.0), 7/8** — CI 1027/1027, desplegado en `ca-thehouse-u2--c7c24535-60-2` con el secreto de Firebase, prueba HTTP real de dispositivos 14/14; T05 espera el push en un teléfono real. 1.1.0: ✅ PASS WITH WARNINGS (25/25 en Azure) |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1.3.0 — Avisos nuevos (T09–T11)

Autorización: Mobile validó el §6.1 de 1.3.0 el 2026-11-27 (`targetKind=room` abre el detalle con `GET /rooms/{targetId}`, `none` abre la bandeja, tipo o destino desconocido → bandeja sin peticiones, el mismo resolver para bandeja y push) y Victor dio «Apruebo y EJECUTA» para T09–T12 el mismo día. Rama `feat/nv-20-avisos-1-3-0`. Sin endpoints, colecciones, índices ni paquetes nuevos; costo $0.

### Cambios por capa

| Capa | Archivos | Cambio |
| --- | --- | --- |
| Domain | `Entities/Notification.cs` | 6 tipos nuevos en el catálogo (22 en total) y destinos `room` y `none` |
| Application (puertos) | `INotificationSink.cs`, `INotificationRepository.cs`, `IFavoriteRepository.cs` | `NotificationEvent.Once`; `ExistsAsync` (aviso ya creado, leído o no); `ListUserIdsByRoomAsync` (quién tiene el cuarto en favoritos, con tope) |
| Application (casos de uso) | `NotificationServices.cs`, `PushServices.cs`, `FavoriteNotices.cs` (nuevo, estático) | Textos de §9.1; `Once` evita repetir el aviso; `agreement_proposed` con prioridad alta en Android; `FavoriteNotices` arma `favorite_unavailable` (tope 200, nunca a los excluidos, una vez por cuarto, usuario y día en hora de Ciudad de México) |
| Rentals | `RentalServices.cs` | `agreement_proposed` al crear el convenio, `agreement_cancelled` al cancelarlo (al estudiante) y `favorite_unavailable` al rentarse el cuarto (sin el arrendador ni el estudiante que lo rentó) |
| Reviews / Moderation | `ReviewServices.cs` | `review_received` al dueño al publicar (sin calificación ni texto); `review_hidden` al autor al ocultarla (sin motivo; volver a mostrarla no avisa) |
| Listings | `RoomService.cs` | `favorite_unavailable` al pausar o eliminar un cuarto (nunca al arrendador) |
| Moderation | `ModerationServices.cs` | `favorite_unavailable` por cada cuarto publicado al suspender un edificio, también cuando se suspende la cuenta del dueño (nunca al dueño ni al staff); `report_resolved` a quien reportó al resolver o descartar el reporte (sin la acción ni el motivo) |
| Infrastructure | `MongoNotificationRepository.cs`, `MongoFavoriteRepository.cs`, `UnavailableFavoriteVisitRepositories.cs` | Las dos consultas nuevas; las versiones «no disponible» fallan cerrado |
| Pruebas | `NotificationsV13HttpTests.cs` (nuevo), `NotificationTests.cs`, dobles de prueba | 6 pruebas de integración nuevas; el catálogo pasa de 16 a 22 |

### Pruebas (local, 2026-11-27)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx -c Release` | **1,401/1,401**: unitarias 459, integración 760 (incluye las 7 de arquitectura de NV-01-R13), seguridad 182 |

| Prueba nueva | Qué demuestra |
| --- | --- |
| `Agreement_rental_and_review_notify_the_other_party` | `agreement_proposed` al estudiante con destino `rental`, sin el código de cierre y con prioridad alta; al rentarse, `favorite_unavailable` solo a quien lo tenía en favoritos (no al estudiante que lo rentó ni al arrendador); `review_received` al dueño sin el texto; `review_hidden` al autor sin el motivo y sin aviso al volver a mostrarla |
| `Cancelled_agreement_notifies_the_student_once` | `agreement_cancelled` al estudiante; una segunda cancelación responde 409 y no repite el aviso |
| `Favorite_unavailable_on_pause_delete_and_suspension_once_per_day` | Pausa, eliminación y suspensión del edificio avisan; leer el aviso, reactivar y volver a pausar el mismo día no repite; el arrendador nunca lo recibe |
| `Resolved_report_notifies_the_reporter_without_the_sanction` | `report_resolved` con destino `none` y el id del reporte, sin la acción (`suspend_building`) ni el motivo |
| `Favorites_failure_does_not_break_the_action` | Si los favoritos no responden, pausar sigue en 200 y no se crea el aviso |
| `Catalog_has_the_new_types_and_targets` | Categorías y destinos de los 6 tipos; tope de 200 |

### Seguridad y coherencia

- **Destinatarios del servidor:** cada destinatario sale del recurso (convenio, reseña, reporte o favoritos), nunca del cliente. Nunca se avisa al actor: arrendador, estudiante que renta, autor que publica o staff que modera.
- **Sin datos de más (RNF-02):** sin calificación, texto, motivo de moderación ni sanción. Los nombres van solo de pila, y si el estudiante no tiene nombre en su perfil sale «Un estudiante».
- **Arquitectura (NV-01-R13):** los módulos que avisan usan solo puertos (`INotificationSink`, `IFavoriteRepository`, `IRoomRepository`). `FavoriteNotices` es estático, así que no es un componente con dependencias. La prueba R4 sigue con las mismas 8 aristas entre módulos.
- **Mejor esfuerzo:** el aviso no deshace la acción. El adaptador del sink ya atrapa sus errores, y si los favoritos fallan (`DependencyUnavailableException`) la acción sigue.
- **Carga acotada:** hasta 200 avisos por cuarto, y se crean en la misma petición. Al suspender un edificio grande es el caso más pesado: está limitado por el máximo de 200 cuartos por edificio, solo lo puede hacer el staff y es poco frecuente en el piloto.
- **Consultas sin índice nuevo:** `favorites` por `roomId` (como la consulta de NV-25) y `notifications` por `userId` y `dedupeKey`. Las dos colecciones son chicas en el piloto.

### Pendiente (T12)

1. Isai mezcla el PR.
2. El CD despliega y Victor aprueba.
3. Prueba HTTP real en Azure de los 6 avisos con cuentas de prueba.
4. Prueba conjunta con Mobile (bandeja y push). Con ella también se cierra T05.

## 1.2.0 — Notificaciones push con Firebase Cloud Messaging

### 1.2.0 · Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/PushDevice.cs` (nuevo) | Instalación de la app con su token de FCM |
| Application | `Abstractions/Notifications/IPushPorts.cs` (nuevo) | `IPushDeviceRepository`, `IPushSender`, `IPushQueue`, `PushMessage` y `PushResult` |
| Application | `Abstractions/Persistence/INotificationRepository.cs` | `UpsertAsync` devuelve la notificación guardada (en mensajes agrupados, la no leída que ya existía) para que el push lleve su id |
| Application | `UseCases/PushServices.cs` (nuevo) | `PushRules` (validación, prioridad alta en 4 tipos, `collapseKey` por conversación), `PushDeviceService` (registro idempotente, máximo 5, token que se mueve de cuenta, conflicto 409) y `PushDispatcher` (todos los dispositivos, token inválido → se borra, un reintento) |
| Application | `UseCases/NotificationServices.cs` | `NotificationPublisher` encola el push después de guardar; un hecho repetido sin agrupar no vuelve a avisar |
| Application | `UseCases/AccountDeletionServices.cs` (NV-10 1.2.0) | La eliminación de cuenta también borra los dispositivos |
| Infrastructure | `Persistence/Mongo/MongoPushDeviceRepository.cs` (nuevo), `MongoNotificationRepository.cs` | `push_devices` con `ux_push_devices_user_installation`, `ux_push_devices_token` y `ttl_push_devices_updated` (270 días); `FindOneAndUpdate` en la bandeja |
| Infrastructure | `Notifications/FcmPushSender.cs` (nuevo) | Cuenta de servicio (JSON o base64), JWT RS256 con `System.Security.Cryptography`, token de acceso en caché con renovación 5 min antes, API HTTP v1, 401 → renueva y reintenta; `DisabledPushSender` sin secreto |
| API | `Http/PushDevicesEndpoint.cs`, `Configuration/PushConfiguration.cs` (nuevos), `Program.cs`, `ProblemResponses.cs`, `UserResourceProblems.cs`, `RateLimitKeys.cs`, `UserRateLimitOptions.cs` | 3 rutas; cola acotada (1000) con `PushWorker`; aviso en el log si falta el secreto; mensajes en español; 30 registros por hora |
| Pruebas | `UnitTests/PushTests.cs`, `IntegrationTests/PushHttpTests.cs`, `IntegrationTests/FcmClientTests.cs`, `SecurityTests/PushSecurityTests.cs`, `IntegrationTests/PushTestDoubles.cs`; listas OpenAPI | 6 unitarias, 6 de integración (2 del cliente FCM con llave RSA generada al momento) y 5 de seguridad |

### 1.2.0 · Comandos y resultados (local, 2026-11-18)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx --configuration Release` (dos corridas seguidas, como el CI) | 1027/1027 en la segunda corrida (unitarias 395, seguridad 148, integración 484); en la primera falló 1 prueba ajena a NV-20 (HealthAndRateLimitTests de NV-01: el cliente de la prueba espera 5 s y la máquina estaba cargada), que pasó sola 7/7; las pruebas de NV-20 pasaron en todas las corridas |

### 1.2.0 · Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Registro de dispositivos | `Register_creates_then_updates_and_never_returns_the_token`, `Invalid_input_is_rejected`, `Sixth_device_replaces_the_oldest`, `Same_phone_moves_between_accounts_but_a_different_token_conflicts`, `Devices_are_registered_listed_and_deleted_without_exposing_tokens`, `Other_accounts_cannot_touch_my_installation`, `Invalid_tokens_are_removed_and_deleting_the_account_removes_devices` |
| R02 Envío push de la bandeja | `Notifications_are_pushed_to_the_recipient_and_never_to_the_actor` (solicitud real por HTTP → cola → push al arrendador con el id de la notificación guardada; nada al estudiante que la envió), `Push_uses_the_inbox_text_priority_and_collapse_key`, `Dispatcher_sends_to_every_device_drops_invalid_tokens_and_retries_once` |
| R03 Cliente de FCM | `Fcm_client_signs_the_assertion_caches_the_token_and_maps_errors` (firma RS256 verificada con la llave pública, alcance y audiencia, token en caché, formato HTTP v1, 404 → token inválido, 500 → falla, 401 → renueva), `Service_account_is_parsed_from_json_or_base64_and_only_for_google` |
| R04 Privacidad, seguridad y límites | `Device_responses_never_carry_the_token_or_the_user`, `Device_body_rejects_unknown_fields`, `Push_text_is_the_inbox_text_without_message_content`, `Credential_and_limits_are_bounded` (solo el endpoint OAuth de Google, sin SSRF por configuración) |
| R05 Contrato para Mobile y despliegue | CD run 44458017269; secreto cargado; dispositivos en Azure 14/14; contrato entregado; **push en teléfono real pendiente** |
| R06 Pruebas, seguridad y evidencia | TRX del CI run 44457497605: 1027/1027; `TheHouse.SecurityTests` 148/148; cobertura en «Despliegue y CI»; esta evidencia |

### 1.2.0 · Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Logout | La app borra su dispositivo con `DELETE` antes del logout (no se agregó encabezado a NV-06); si no lo hace, el siguiente usuario que registre el mismo token en ese teléfono se queda con él |
| Conflicto | 409 `installation_conflict` solo si la instalación está en otra cuenta **con otro token**; con el mismo token es el mismo teléfono y se mueve |
| Suspensión (NV-21) | Los dispositivos se conservan: la cuenta suspendida debe recibir el aviso `building_suspended` |
| Notificación repetida | Si ya había una no leída del mismo hecho y no se agrupa, no se vuelve a enviar push |

### 1.2.0 · Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PR | [#65](https://github.com/thehouse-team/TheHouse-Backend/pull/65), mezclado por `IsaiAxel` (merge `7c24535`) |
| CI de `main` (`7c24535`, run 44457497605) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 1027/1027: unitarias 395, integración 484, seguridad 148 |
| Cobertura de líneas (CI) | 68.9 % total (ramas 72.6 %). `PushServices` 100 %, `PushDevicesEndpoint` 100 %, `NotificationServices` 98.4 %, `FcmPushSender` 89.8 %, `PushConfiguration` 80.0 %. `MongoPushDeviceRepository` 3.2 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 44458017269) | Intento 1: `verify-ephemeral` falló porque ZAP no pudo levantar su proxy en el runner («Address already in use»; no escaneó, ningún hallazgo). Intento 2 (solo los jobs fallidos): resolve, publish, verify-ephemeral (smoke y ZAP), deploy aprobado por Victor, DAST y resumen en verde |
| Secreto | `firebase-service-account` cargado por Victor el 2026-11-19 desde la cuenta de servicio dedicada del proyecto de Firebase de la app; `FIREBASE_SERVICE_ACCOUNT` como `secretRef` (sin valor en texto plano); el archivo local se borró |
| Azure | revisión `ca-thehouse-u2--c7c24535-60-2`, Healthy, 100 % del tráfico; logs de la revisión sin «Push deshabilitado» ni errores (la cuenta de servicio se leyó) |

### 1.2.0 · Prueba HTTP real en Azure

`.azure-check/nv20_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-19 con dos cuentas `@example.com` nuevas y tokens de FCM generados al azar con el formato real (no de un teléfono); solo se imprimen códigos, campos y conteos. Resultado: **14/14 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Dispositivos | Registrar 201 con los campos del §6.1 y `no-store`; sin token en la respuesta; actualizar el token 200; lista sin tokens | 5/5 |
| BOLA, conflicto y validación | Otra cuenta no puede borrar mi instalación 404; otra cuenta con otro token 409 `installation_conflict`; plataforma no admitida 400; campo desconocido 400; `installationId` inválido 404; sin sesión 401 | 6/6 |
| Límite | Con 6 registros quedan 5 y sale el más antiguo | 1/1 |
| Limpieza | 5 dispositivos borrados; lista vacía | 2/2 |

### 1.2.0 · Contrato entregable a Mobile

Entregado el 2026-11-19 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 3 rutas son nuevas y las respuestas existentes no cambian.

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44457497605): 1027/1027 (unitarias 395, integración 484, seguridad 148); prueba HTTP real de dispositivos en Azure 14/14 (2026-11-19); push en teléfono real pendiente con Mobile
```

### 1.2.0 · Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | El envío real a FCM todavía no se probó con un teléfono (T05) | Media | Abierto: prueba con Mobile (solicitud, mensaje y visita deben llegar como push) |
| H2 | ZAP falló una vez al iniciar en el runner de GitHub (puerto ocupado) | Baja | Reintento manual del job; si se repite, se ajusta el paso del workflow |
| H3 | `HealthAndRateLimitTests.Slow_probe_has_bounded_readiness...` (NV-01) falló una vez en local con la máquina cargada (el cliente de la prueba espera 5 s); pasó en el CI y aislada | Baja | Observación; si se repite en el CI se amplía su tiempo |
| H4 | Adaptador Mongo sin cobertura en CI | Baja | Aceptado: se verificó con la prueba real en Azure |

Resultado parcial: **PASS WITH WARNINGS (7/8)**. Costo: $0 (FCM no cobra; una colección pequeña `push_devices` con TTL de 270 días en MongoDB Atlas M0). Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 7 de 8 con T05 pendiente.

# Historial 1.1.0

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
| R08 Contrato para Mobile y despliegue | CD run 43525078903; prueba HTTP real en Azure 25/25 (§6); contrato entregado (§7) |
| R09 Pruebas, seguridad y evidencia | TRX del CI run 43524705152: 893/893; `TheHouse.SecurityTests` 98/98; cobertura en §5; esta evidencia |

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

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#45](https://github.com/thehouse-team/TheHouse-Backend/pull/45) (spec 1.1.0) incluido en [#46](https://github.com/thehouse-team/TheHouse-Backend/pull/46) (aprobación e implementación), aprobado y mezclado por `IsaiAxel` (merge `ebb8deb`) |
| CI de `main` (`ebb8deb`, run 43524705152) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 893/893: unitarias 337, integración 458, seguridad 98 |
| Cobertura de líneas (CI) | 68.6 % total (ramas 72.3 %). `Notification` 100 %, `NotificationServices` 97.6 %, `NotificationsEndpoint` 100 %, `NotificationsConfiguration` 63.3 % (la rama de falla de `ClearAsync` no se ejercita). `MongoNotificationRepository` en 0 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 43525078903) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--cebb8deb-43-1`, Healthy |
| Rol admin temporal (M-BE-065) | Autorizado por Victor el 2026-11-13 («autorizo rol»). `--grant-role` a la cuenta admin de prueba antes de la corrida y `--revoke-role` justo después (usuario `6af7b4bc9801f121e98b5356`), vía `az containerapp exec` en esa revisión |

## 6. Prueba HTTP real en Azure

`.azure-check/nv20_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-13 con tres cuentas `@example.com` nuevas (estudiante, arrendador y admin); solo se imprimen códigos, tipos y conteos, nunca tokens, contraseñas, textos de avisos ni direcciones. Resultado: **25/25 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Preparación | Edificio de prueba creado (201) y aprobado por el admin (200) | 2/2 |
| Avisos por acciones reales | `building_approved` con los campos del §6.1; solicitud → `request_received`; aceptar → `request_accepted`; dos mensajes → un solo `message_received` con `count=2`; visita → `visit_requested`; confirmar → `visit_confirmed`; ningún aviso trae dirección, teléfono ni texto del chat | 12/12 |
| Contador y marcas | Contador 200 con `unread, requests, visits, system` y `private, no-cache`; 304 con `If-None-Match`; aviso ajeno 404; marcar uno 204; leer el chat marca el aviso de mensajes; marcar las de visitas y todas 204; contador en cero | 9/9 |
| Limpieza | Edificio suspendido (200) y aviso `building_suspended` | 2/2 |

## 7. Contrato entregable a Mobile

Entregado el 2026-11-13 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 4 rutas son nuevas y no cambia ninguna anterior.

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 43524705152): 893/893 (unitarias 337, integración 458, seguridad 98); prueba HTTP real en Azure 25/25 (2026-11-13)
```

## 8. Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | Sin outbox: si MongoDB falla justo después de una acción, ese aviso se pierde (queda una advertencia en el log) | Baja | Aceptado en M-BE-071; la información sigue en la pantalla de origen |
| H2 | Avisos de convenio (NV-18) y de suspensión cubiertos por la prueba en Azure y por código revisado, sin prueba unitaria propia | Baja | Aceptado: `building_suspended` verificado en Azure; `agreement_*` usa el mismo puerto ya probado |
| H3 | Adaptadores Mongo sin cobertura en CI | Baja | Aceptado: se verifican con la prueba real en Azure |
| H4 | El prototipo mostraba una cita del mensaje y un aviso de baja de precio | Información | Fuera de 1.1.0 por decisión (M-BE-071); Mobile muestra «te escribió» sin texto |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos; una colección `notifications` con TTL de 90 días y cuatro índices en MongoDB Atlas M0, dentro del plan gratuito. Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 1.1.0 completa 11/11; las rutas de NV-12 a NV-18 no cambiaron.
