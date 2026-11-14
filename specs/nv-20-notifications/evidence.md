# NV-20 — Evidencia: notificaciones

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-13 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-12: NV-20 1.1.0, tasks T01–T11 (M-BE-071) |
| Rama | `feat/nv-20-notifications` → PR #46, merge `ebb8deb` |
| Resultado | **✅ PASS WITH WARNINGS** — T01–T11 completas: CI 893/893, desplegado en `ca-thehouse-u2--cebb8deb-43-1`, prueba HTTP real en Azure 25/25 |

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
