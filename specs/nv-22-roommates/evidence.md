# NV-22 — Evidencia: búsqueda de compañero de vivienda

| Campo | Valor |
| --- | --- |
| ID | NV-22 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-22 1.1.0, tasks T01–T10 (M-BE-074) |
| Rama | `feat/nv-22-roommates` → PR #56, merge `31614c5` |
| Resultado | **✅ PASS WITH WARNINGS** — T01–T10 completas: CI 957/957, desplegado en `ca-thehouse-u2--c31614c5-51-1`, prueba HTTP real en Azure 20/20 |

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
| R07 Contrato para Mobile y despliegue | CD run 44210636026; prueba HTTP real en Azure 20/20 (§6); contrato entregado (§7) |
| R08 Pruebas, seguridad y evidencia | TRX del CI run 44209958409: 957/957; `TheHouse.SecurityTests` 123/123; cobertura en §5; esta evidencia |

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

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#55](https://github.com/thehouse-team/TheHouse-Backend/pull/55) (spec 1.1.0) y [#56](https://github.com/thehouse-team/TheHouse-Backend/pull/56) (aprobación e implementación), aprobados y mezclados por `IsaiAxel` (merge `31614c5`) |
| CI de `main` (`31614c5`, run 44209958409) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 957/957: unitarias 365, integración 469, seguridad 123 |
| Cobertura de líneas (CI) | 68.2 % total (ramas 72.0 %). `Roommate` 100 %, `RoommateServices` 98.3 %, `RoommatesEndpoint` 96.2 %, `MessagingServices` 98.6 %. `MongoRoommateProfileRepository` en 0 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 44210636026) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c31614c5-51-1`, Healthy |
| Rol admin | No se usó: la prueba solo necesita cuentas de estudiante |

## 6. Prueba HTTP real en Azure

`.azure-check/nv22_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-17 con tres cuentas de estudiante `@example.com` nuevas (Ana, Beto y Caro) en la misma universidad (UTTT); solo se imprimen códigos, campos y conteos, nunca tokens, contraseñas ni el texto de los mensajes. Resultado: **20/20 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Perfiles | Sin universidad 409 `university_required`; lista sin perfil propio 403 `roommate_profile_required`; crear 201 con los campos del §6.1 y `no-store`; actualizar 200; Beto y Caro 201; campos desconocidos 400 | 7/7 |
| Lista y coincidencias | Lista 200 con los campos del §6.1; orden Beto (7) y Caro (5); sin ids de usuario ni apellidos; detalle 200 | 4/4 |
| Contacto | A sí misma 409 `own_profile`; primer contacto 201; segundo contacto 200 con la misma conversación; Beto la ve con `kind=roommate`, `room=null` y rol `student`; responde 201; aviso `message_received` | 6/6 |
| Limpieza | Los tres perfiles borrados (204) | 3/3 |

## 7. Contrato entregable a Mobile

Entregado el 2026-11-17 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 6 operaciones en 4 rutas son nuevas y el único cambio a una respuesta existente es el campo `kind` al final de las conversaciones de NV-16.

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44209958409): 957/957 (unitarias 365, integración 469, seguridad 123); prueba HTTP real en Azure 20/20 (2026-11-17)
```

## 8. Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | Adaptadores Mongo sin cobertura en CI | Baja | Aceptado: se verifican con la prueba real en Azure |
| H2 | La lista lee hasta 500 perfiles vigentes por universidad y ordena en memoria | Baja | Aceptado para el volumen del piloto; si crece, se pasa a un orden en la base sin cambiar el contrato |
| H3 | Las cuentas y la conversación de prueba quedan en Production | Información | Intencional, como en specs anteriores: los perfiles de compañero se borraron y las cuentas son `@example.com` |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos; una colección `roommate_profiles` con dos índices en MongoDB Atlas M0, dentro del plan gratuito. Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 1.1.0 completa 10/10; las rutas de NV-12 a NV-21 no cambiaron y el cambio en NV-16 es aditivo.
