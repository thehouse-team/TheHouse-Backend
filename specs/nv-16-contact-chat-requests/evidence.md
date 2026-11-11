# NV-16 — Evidencia: contacto, solicitudes y chat

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-11 |
| Autorización | «Apruebo y EJECUTA NV-16, NV-23 y NV-24» — Victor Manuel Jose Jose, 2026-11-10: NV-16 1.1.0, tasks T01–T11 (M-BE-068) |
| Rama | `feat/nv-16-messaging` → PR #36 (spec y aprobación en #35), mezclado en `main` (`00839f8`) |
| Resultado | **✅ PASS WITH WARNINGS**: 11/11 tasks completas; desplegada en Azure y verificada con prueba HTTP real 33/33. Advertencias: adaptador Mongo sin cobertura automatizada (verificado en Azure) y cuartos de demostración con arrendador de sistema que no responde |

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
| R08 Contrato para Mobile y despliegue | Despliegue y prueba HTTP real en Azure 33/33 (§5 y §6); contrato entregado (§7) |
| R09 Pruebas, seguridad y evidencia | CI de `main`: 800/800 y cobertura del §5; TRX de `TheHouse.SecurityTests` 74/74 |

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

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#35](https://github.com/thehouse-team/TheHouse-Backend/pull/35) (spec 1.1.0 y aprobación) y [#36](https://github.com/thehouse-team/TheHouse-Backend/pull/36) (implementación), aprobados y mezclados por `IsaiAxel` (merge `00839f8`, 2026-11-11) |
| CI de `main` (`00839f8`, run 42931704436) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 800/800: unitarias 284, integración 442, seguridad 74 |
| Cobertura de líneas (CI) | 68.1 % total. `MessagingEndpoint` 100 %, `MessagingServices` 98.4 %. `MongoMessagingRepositories` en 0 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 42932081749) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c00839f8-35-1`; `/api/v1/conversations` sin token → 401 |
| Rol de prueba (M-BE-065) | Autorizado por Victor el 2026-11-11 («autorizo rol»): `--grant-role` dio `admin` a la cuenta de prueba `nv16-azure-admin-a9050368@example.com` (usuario `6af3b987…`) para aprobar el edificio de prueba, y al terminar `--revoke-role` lo retiró; ambos cambios en la bitácora con actor `cli` |

## 6. Prueba HTTP real en Azure

`.azure-check/nv16_azure_check.py` (fuera del repositorio), en dos fases: `prepare` creó tres cuentas `@example.com` (estudiante, arrendador y admin) y `run` ejecutó la prueba; no se imprimen tokens, contraseñas ni el texto de los mensajes. Resultado: **33/33 PASS** (2026-11-11).

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Preparación | Edificio «PRUEBA AUTOMATIZADA NV-16» con dos cuartos, aprobado por la cuenta admin de prueba | 2/2 |
| Solicitudes | Crear (201, campos del §6.1, `no-store`); el estudiante no ve datos del estudiante y solo el primer nombre del arrendador; 409 `active_request_exists`; 400 por `status` en el cuerpo; 422 `own_room`; 404 cuarto inexistente; 401 sin token; mis solicitudes | 8/8 |
| Bandeja del arrendador | 403 `landlord_required` sin modo arrendador; bandeja con `student { name, university }`; aceptar; 409 `request_state` al repetir; el estudiante no usa la bandeja | 5/5 |
| Chat | Enviar 201; repetir `clientMessageId` → 200 con el mismo id; conversaciones con 1 no leído; mensajes con ETag y `pollAfterSeconds: 10`; **304** con `If-None-Match`; marcar leído 204 y no leídos en 0; respuesta del arrendador; otra cuenta → 404 | 9/9 |
| Rechazo y cancelación | Segunda solicitud; 400 `other` sin nota; rechazar; 409 `conversation_closed`; cancelar la aceptada | 5/5 |
| Privacidad | Sin teléfono ni correos en las respuestas; arrendador solo con su primer nombre | 2/2 |
| Limpieza | Edificio de prueba suspendido y fuera de la búsqueda | 2/2 |

Primera corrida interrumpida: se ejecutó `run` antes de asignar el rol; el script se detuvo tras crear el edificio de prueba. Se ajustó para comprobar el rol antes de empezar y para reutilizar ese edificio; el API no cambió.

## 7. Contrato entregable a Mobile

Entregado el 2026-11-11 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` con la URL y las pruebas reales:

```text
ENDPOINT     /api/v1/rooms/{roomId}/requests
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      { quickQuestions: [..], message }
RESPONSE     201 { id, status, room: { id, buildingId, title, city }, quickQuestions: [..], message, student: { name, university } | null, landlord: { name }, visit: { id, scheduledAt, status } | null, conversationId, rejection: { reason, note } | null, roomAvailable, createdAt, updatedAt }
STATUS CODES 201, 400, 401, 404 cuarto no publicado, 409 code=active_request_exists | active_requests_limit, 422 code=own_room, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/requests?status=pending&page=1&pageSize=20 y /users/me/requests/{id}
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      status, page, pageSize | id
RESPONSE     200 sobre de NV-01 con solicitudes | 200 solicitud
STATUS CODES 200, 400, 401, 404 ajena, 429, 503
PAGINATION   Sí: orden updatedAt desc, id
```

```text
ENDPOINT     /api/v1/users/me/requests/{id}/cancel
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      sin cuerpo
RESPONSE     200 solicitud (cancelled)
STATUS CODES 200, 401, 404, 409 code=request_state, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/requests?status=pending&roomId=..&page=1 y /landlord/requests/{id}
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      status, roomId, page, pageSize | id
RESPONSE     200 sobre de NV-01 con solicitudes | 200 solicitud
STATUS CODES 200, 400, 401, 403 sin perfil de arrendador, 404 ajena, 429, 503
PAGINATION   Sí
```

```text
ENDPOINT     /api/v1/landlord/requests/{id}/accept | /reject
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      accept: sin cuerpo; reject: { reason, note }
RESPONSE     200 solicitud
STATUS CODES 200, 400, 401, 403, 404, 409 code=request_state | room_unavailable, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/conversations?page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      page, pageSize
RESPONSE     200 { items: [{ id, requestId, room: { id, title }, counterpart: { name, role }, status: "open" | "closed", lastMessageAt, unreadCount }], ... }
STATUS CODES 200, 400, 401, 429, 503
PAGINATION   Sí: orden lastMessageAt desc, id
```

```text
ENDPOINT     /api/v1/conversations/{id}/messages?after=..&limit=50
METHOD       GET
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      after, limit; If-None-Match opcional
RESPONSE     200 { items: [{ id, senderRole: "student" | "landlord", text, createdAt, mine }], hasMore, pollAfterSeconds } con ETag | 304
STATUS CODES 200, 304, 400, 401, 404, 429, 503
PAGINATION   Cursor `after`
```

```text
ENDPOINT     /api/v1/conversations/{id}/messages
METHOD       POST
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      { text (1-1000), clientMessageId (UUID) }
RESPONSE     201 { id, senderRole: "student" | "landlord", text, createdAt, mine } | 200 si se repite clientMessageId
STATUS CODES 201, 200, 400, 401, 404, 409 code=conversation_closed | participant_suspended, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/conversations/{id}/read
METHOD       POST
AUTH         Bearer JWT; solo el estudiante o el arrendador de la conversación
HEADERS      —
REQUEST      { lastMessageId }
RESPONSE     204
STATUS CODES 204, 400, 401, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 42931704436): 800/800 (unitarias 284, integración 442, seguridad 74); prueba HTTP real en Azure 33/33 (2026-11-11)
```
