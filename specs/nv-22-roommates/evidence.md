# NV-22 — Evidencia: búsqueda de compañero de vivienda

| Campo | Valor |
| --- | --- |
| ID | NV-22 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-22 1.1.0, tasks T01–T10 (M-BE-074) |
| Rama | `feat/nv-22-roommates` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T06 completas y verificadas en local; T07–T10 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Roommate.cs` (nuevo) | `RoommateProfile` (visible y vigente 60 días), `RoommateHabits` (6 hábitos), `MatchesWith` (hábitos iguales + traslape de presupuesto), `ConversationKinds` y la llave de pareja |
| Domain | `Entities/Messaging.cs` (NV-16) | `Conversation.Kind` (`rental` por defecto) e `IsRoommate` |
| Application | `Abstractions/Persistence/IRoommateProfileRepository.cs` (nuevo), `IMessagingRepositories.cs` | Perfil por usuario; `CreateIfAbsentAsync` en conversaciones |
| Application | `Contracts/Roommates/RoommateContracts.cs` (nuevo), `Contracts/Messaging` | Entradas con `JsonUnmappedMemberHandling.Disallow`; `ConversationResponse` agrega `kind` al final y `room` puede ser `null` |
| Application | `UseCases/RoommateServices.cs` (nuevo) | `RoommateRules` (catálogos y validación) y `RoommateService` (perfil, lista recíproca, detalle y contacto) |
| Application | `UseCases/MessagingServices.cs`, `NotificationServices.cs` | En conversaciones `roommate`: rol `student` para los dos, sin cuarto ni solicitud en la respuesta, aviso «te escribió sobre tu búsqueda de compañero» |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | 60/min por usuario y 10 contactos nuevos al día |
| Infrastructure | `Persistence/Mongo/MongoRoommateProfileRepository.cs` (nuevo), `MongoMessagingRepositories.cs` | `roommate_profiles` con índices `ux_roommate_profiles_user` e `ix_roommate_profiles_university_visible_expires`; `kind` en `conversations` (ausente = `rental`) |
| API | `Http/RoommatesEndpoint.cs`, `Configuration/RoommatesConfiguration.cs` (nuevos), `Program.cs` | 6 operaciones en 4 rutas |
| API | `Http/ProblemResponses.cs`, `UserResourceProblems.cs` | Mensajes y códigos `roommate_profile_required`, `university_required`, `own_profile` |
| Pruebas | `UnitTests/RoommateTests.cs`, `IntegrationTests/RoommatesHttpTests.cs`, `SecurityTests/RoommateSecurityTests.cs`, `IntegrationTests/RoommateTestDoubles.cs` | 12 unitarias, 3 de integración y 8 de seguridad; la prueba de NV-16 que lista campos agrega `kind`; las listas OpenAPI agregan las 4 rutas |

## 2. Comandos y resultados (local, 2026-11-17)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx --configuration Release` (dos corridas seguidas, como el CI) | 957/957 en ambas corridas: unitarias 365, seguridad 123, integración 469; 0 fallas |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Mi perfil de compañero | `Profile_uses_the_university_of_the_user_and_renews_on_update`, `Profile_input_is_validated` (7 casos), `Two_students_find_each_other_and_chat` (201 y 200), `List_requires_own_profile_university_and_hides_other_universities` (`university_required`, borrar) |
| R02 Lista y coincidencias | `Matches_count_shared_habits_plus_budget_overlap`, `List_is_reciprocal_same_university_and_ordered_by_matches`, `Two_students_find_each_other_and_chat` |
| R03 Detalle del compañero | `List_requires_own_profile_university_and_hides_other_universities` (otra universidad 404), `Two_students_find_each_other_and_chat` |
| R04 Contacto por chat | `Contact_opens_one_roommate_conversation_per_pair`, `Two_students_find_each_other_and_chat` (201 y luego 200 con la misma conversación, aviso de NV-20) |
| R05 Compatibilidad con NV-16 | `Roommate_conversations_appear_without_room_and_with_the_first_name`, `MessagingHttpTests` en verde con `kind` |
| R06 Privacidad, autorización y límites | `Cards_never_carry_user_ids_contact_data_or_location`, `Roommate_inputs_reject_unknown_fields`, `Roommate_codes_have_a_spanish_detail`, `Dependencies_fail_closed`, suspensión en `List_is_reciprocal...` y `Contact_opens...` |
| R07 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R08 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **Reciprocidad y alcance:** solo quien tiene su perfil visible y vigente ve a otros, y solo de su universidad de NV-11; los vencidos, ocultos y suspendidos no aparecen.
- **RNF-02:** las tarjetas usan el id del perfil (nunca el del usuario), el primer nombre y la universidad; sin correo, teléfono, apellidos ni ubicación; el contacto es solo por el chat.
- **Abuso:** 10 contactos nuevos al día, reporte y suspensión con NV-21, una conversación por pareja.
- **Asignación masiva:** el perfil no acepta `userId` ni `universityId` del cliente (400).
- **Fallo cerrado:** MongoDB o Redis caídos → 503.
- **Coherencia:** las conversaciones de renta no cambian (sin `kind` en la base = `rental`); el nuevo campo `kind` va al final de la respuesta.

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Una conversación por pareja | La conversación `roommate` guarda como `requestId` interno `roommate:<ids ordenados>`, así el índice único existente `ux_conversations_request` evita duplicados sin crear ni cambiar índices; la API lo muestra como `""` |
| Lugar en el aviso | Un aviso sin cuarto ni edificio dice «sobre tu búsqueda de compañero» (antes «TheHouse», que ningún aviso usaba) |
| Candidatos | Se leen hasta 500 perfiles vigentes de la universidad y se ordenan en memoria por coincidencias |

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv22_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
