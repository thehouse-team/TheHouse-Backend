# NV-16 — Evidencia: contacto, solicitudes y chat

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-10 |
| Autorización | «Apruebo y EJECUTA NV-16, NV-23 y NV-24» — Victor Manuel Jose Jose, 2026-11-10: NV-16 1.1.0, tasks T01–T11 (M-BE-068) |
| Rama | `feat/nv-16-messaging` (implementación `014bd05`) → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T07 completas y verificadas en local; T08–T11 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Messaging.cs` | `RentalRequest` (estados `pending`, `accepted`, `rejected`, `cancelled`), `Conversation` (`open`, `closed`, últimos leídos por participante) y `ChatMessage` inmutable |
| Application | `Abstractions/Persistence/IMessagingRepositories.cs` | Puertos `IRentalRequestRepository` e `IConversationRepository` |
| Application | `Contracts/Messaging/MessagingContracts.cs` | Entradas con `JsonUnmappedMemberHandling.Disallow` y respuestas del §6.1 |
| Application | `UseCases/MessagingServices.cs` | `RentalRequestService` (crear, bandejas, cancelar, aceptar, rechazar), `ConversationService` (listar, mensajes, enviar, leído), `MessagingRules` y `MessagingIds` |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | Llaves con hash y límites: 60/min, 10 solicitudes/día, 30 mensajes/min, 12 consultas/min por conversación; lock por estudiante |
| Infrastructure | `Persistence/Mongo/MongoMessagingRepositories.cs` | `rental_requests` (único parcial `{studentId, roomId}` con `active: true`; índices por estudiante y por dueño), `conversations` (único `requestId`; índices por participante y último mensaje), `messages` (índice `{conversationId, _id}` y único `{conversationId, senderId, clientMessageId}`); repositorios no disponibles con 503 |
| API | `Http/MessagingEndpoint.cs` | 12 endpoints del §6; ETag débil y 304 solo con el cursor al día |
| API | `Http/ProblemResponses.cs`, `Http/UserResourceProblems.cs` | Mensajes en español para NV-16 y 8 códigos nuevos |
| API | `Configuration/MessagingConfiguration.cs`, `Program.cs` | Registro de servicios y rutas |
| Pruebas | `UnitTests/MessagingTests.cs`, `IntegrationTests/MessagingHttpTests.cs`, `IntegrationTests/MessagingTestDoubles.cs`, `SecurityTests/MessagingSecurityTests.cs` | 33 unitarias, 16 de integración y 13 de seguridad; listas de OpenAPI actualizadas |

## 2. Comandos y resultados (local, 2026-11-10)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 799/799 en ambas: unitarias 284, seguridad 74, integración 441 |
| `dotnet test TheHouse.slnx` (con la prueba de concurrencia y `main` integrado) | 800/800: unitarias 284, seguridad 74, integración 442 |
| `dotnet test --filter MessagingHttpTests` (con la prueba de concurrencia) | 16/16 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Crear solicitud | `Create_builds_request_and_linked_conversation`, `Create_validates_questions_and_message`, `Create_rejects_long_message_and_bad_room_id`, `Create_requires_a_published_room_and_not_an_own_room`, `Only_one_active_request_per_room_and_ten_in_total`, `Suspended_participants_cannot_start_a_request`, `Create_fails_when_the_user_lock_is_held`, `A_failed_conversation_leaves_no_request_behind`, `Create_rejects_bad_input_with_400`, `Student_requests_landlord_accepts_and_both_chat` |
| R02 Bandeja del estudiante y cancelación | `Student_cancels_pending_or_accepted_but_not_twice`, `Requests_are_private_to_their_student_and_owner`, `Student_cancels_and_conversation_closes` |
| R03 Bandeja del arrendador, aceptar y rechazar | `Landlord_accepts_once_and_sees_the_student_summary_and_visit`, `Reject_requires_a_valid_reason_and_closes_the_conversation`, `Accept_fails_when_the_room_is_no_longer_published`, `Landlord_inbox_requires_landlord_mode_and_filters_by_status_and_room`, `Conflicts_and_rules_return_their_codes`, `Unpublished_or_unknown_rooms_and_foreign_resources_are_404` |
| R04 Chat por consulta incremental | `Messages_are_idempotent_ordered_and_paged_by_cursor`, `Send_validates_text_and_client_id`, `Closed_conversations_and_suspended_participants_cannot_write`, `Student_requests_landlord_accepts_and_both_chat` (304 con `If-None-Match`) |
| R05 No leídos y lectura | `Unread_counts_only_the_other_participant_and_read_marks_progress`, `Student_requests_landlord_accepts_and_both_chat` |
| R06 Persistencia e índices | `Concurrent_double_taps_create_one_request_and_one_message` (6 solicitudes y 6 envíos simultáneos → 1 y 1), dobles con la misma unicidad que los índices; los índices reales se verifican en Azure |
| R07 Privacidad y abuso | `No_contact_data_in_responses_and_no_content_in_logs`, `Rate_limits_return_429_with_retry_after`, `Fails_closed_when_mongo_or_redis_are_down`, `Suspended_landlord_blocks_new_requests_and_messages`, `Requires_a_valid_bearer_token`, `Redis_keys_never_contain_raw_user_or_conversation_ids`, `Inputs_reject_unknown_fields`, `Every_messaging_code_has_a_spanish_detail` |
| R08 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R09 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **BOLA:** el estudiante solo ve sus solicitudes, el arrendador solo las de sus cuartos y una conversación solo sus dos participantes; lo ajeno responde 404.
- **Privacidad:** ninguna respuesta incluye teléfono, correo, apellidos ni ids de usuario. El arrendador aparece con su primer nombre y el estudiante con el nombre de su perfil y su universidad.
- **Asignación masiva:** las entradas rechazan `status`, `studentId`, `senderId` y cualquier campo desconocido.
- **Abuso:** límites en Redis con llaves con hash; una cuenta suspendida (de cualquiera de las dos partes) no crea solicitudes ni escribe; el texto de solicitudes y mensajes no se registra en logs.
- **Fallo cerrado:** MongoDB o Redis caídos → 503.
- **Coherencia con NV-17:** la visita no se duplica; la solicitud muestra la visita activa del estudiante para ese cuarto.

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Índice único de solicitudes activas | Campo `active` (true en `pending` y `accepted`) como filtro parcial, en lugar de `$in` sobre `status`, para no depender de la versión de MongoDB |
| Solicitud y conversación enlazadas | Ids generados antes de insertar; primero la solicitud (decide la unicidad) y luego la conversación; si la conversación falla, la solicitud se borra |
| 304 en mensajes | ETag débil `m-{último mensaje}`; responde 304 solo si coincide **y** el cursor `after` ya es el último mensaje, para que la paginación con `hasMore` nunca reciba 304 |
| Concurrencia en la creación | Lock por estudiante en Redis (`request_in_progress` si otra creación está en curso), además del índice único |
| Cuartos de demostración | Admiten solicitudes; su arrendador es la cuenta de sistema `system-demo` («TheHouse»), que no inicia sesión y no responde |

## 5. Pendiente para cerrar

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv16_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
