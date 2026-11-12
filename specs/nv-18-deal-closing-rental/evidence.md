# NV-18 — Evidencia: cierre de trato con QR y renta

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-12 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-12: NV-18 1.1.0, tasks T01–T11 (M-BE-070) |
| Rama | `feat/nv-18-deal-closing` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T07 completas y verificadas en local; T08–T11 esperan CI, despliegue y prueba HTTP real en Azure |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Rental.cs` (nuevo) | `Agreement` (`pending`, `active`, `ended`, `cancelled`), `AgreementTerms`, `ClosingCode` (solo hashes, vence en 10 min, 5 fallos), historial con actor; `Activate`, `RevertActivation`, `Cancel`, `End`, `EnsureCodeUsable` |
| Domain | `Entities/Listings.cs` | `Room.Rent` (`available` → `rented`) y `Room.Release` (`rented` → `available`) |
| Domain | `Exceptions/GoneException.cs` (nuevo), `NotFoundException.cs`, `RateLimitExceededException.cs` | 410 con código; 404 y 429 con código opcional (`code_invalid`, `no_active_rental`, `too_many_attempts`) |
| Application | `Abstractions/Persistence/IAgreementRepository.cs`, `IRentalHistoryQuery.cs` (nuevos) | Puerto de `agreements` (reemplazo condicional por `updatedAt`, suma atómica de fallos) y puerto de historial para NV-19 |
| Application | `Contracts/Rentals/RentalContracts.cs` (nuevo) | Entradas con `JsonUnmappedMemberHandling.Disallow`; respuestas del §6.1 |
| Application | `UseCases/RentalServices.cs` (nuevo) | `ClosingCodes` (generación, normalización y SHA-256), `AgreementRules` (términos, próximo pago) y `AgreementService` (crear, listar, detalle, `/code`, cancelar, terminar, revisar, aceptar, Mi renta); `RentalHistoryQuery` |
| Application | `Common/RateLimitKeys.cs`, `Common/UserRateLimitOptions.cs` | Llaves con hash y límites: 60/min general, 10/min por usuario y 30/min por IP para el código, 5 intentos por cuenta en 15 min; candado por cuarto |
| Infrastructure | `Persistence/Mongo/MongoAgreementRepository.cs` (nuevo) | Colección `agreements`; índices `ux_agreements_open_room` (único parcial `open: true`), `ix_agreements_code_hash`, `ix_agreements_qr_token_hash`, `ix_agreements_owner_created`, `ix_agreements_student_status` |
| API | `Http/RentalsEndpoint.cs` (nuevo), `Configuration/RentalsConfiguration.cs` (nuevo), `Program.cs` | 9 endpoints; `AddRentals()` y `MapRentals()` |
| API | `Http/ProblemResponses.cs`, `Http/UserResourceProblems.cs`, `Http/UserEndpointSupport.cs` | Mensajes de convenios; título del 410; `code` también en 404, 410 y 429; códigos nuevos con detalle en español |
| Pruebas | `UnitTests/RentalTests.cs`, `IntegrationTests/RentalsHttpTests.cs`, `SecurityTests/RentalSecurityTests.cs`, `IntegrationTests/RentalTestDoubles.cs` | 26 unitarias, 5 de integración y 15 de seguridad; doble en memoria de `agreements`; las listas de rutas OpenAPI agregan las 9 operaciones |

## 2. Comandos y resultados (local, 2026-11-12)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 876/876 en ambas: unitarias 327, seguridad 95, integración 454 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Convenio desde una solicitud aceptada | `Landlord_creates_from_an_accepted_request_and_gets_a_code`, `Create_rules_and_codes`, `Terms_are_validated` (10 casos), `Terms_compute_the_end_date_and_the_next_payment`, `Errors_have_stable_codes` |
| R02 Código de cierre seguro | `Closing_codes_are_random_hashed_and_tolerant_to_format`, `New_code_replaces_the_previous_one_and_cancel_invalidates_it`, `Code_checks_expired_blocked_and_missing`, `Closing_codes_are_stored_only_as_hashes` |
| R03 Revisar y aceptar | `Pending_agreement_activates_once_and_the_code_becomes_used`, `Accept_rules_terms_expiry_and_request_state`, `Room_changed_midway_reverts_the_agreement_without_a_code`, `Two_simultaneous_accepts_rent_the_room_once`, `Landlord_closes_the_deal_student_accepts_and_the_rental_ends` |
| R04 Efectos al rentar | `Accept_rents_the_room_and_closes_other_requests_and_visits`, `Effects_failures_do_not_undo_the_rental` |
| R05 Mi renta | `Accept_rents_the_room_and_closes_other_requests_and_visits`, `End_frees_the_room_and_removes_the_address`, `Only_my_rental_reveals_the_address_and_nobody_sees_contact_data` |
| R06 Convenios del arrendador, cancelar y fin de renta | `Landlord_list_filters_by_status_and_room`, `End_frees_the_room_and_removes_the_address` (incluye `RentalHistoryQuery`), `Only_active_rentals_end_and_rooms_move_between_available_and_rented`, `Agreements_are_private_to_their_landlord_and_student` |
| R07 Fuerza bruta y privacidad | `Another_account_gets_404_and_five_failures_block_the_code`, `Closing_code_attempts_are_limited_per_account`, `Dependencies_fail_closed`, `Rental_inputs_reject_unknown_fields`, `Rental_codes_have_a_spanish_detail`, `Agreements_are_private_to_their_landlord_and_student` |
| R08 Contrato para Mobile y despliegue | Pendiente: CI, despliegue y prueba HTTP real en Azure |
| R09 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

## 4. Seguridad y coherencia

- **BOLA:** el arrendador solo ve y cambia convenios de sus cuartos y solo crea desde sus solicitudes; el estudiante solo revisa y acepta los suyos; lo ajeno responde 404 (un código de otra cuenta responde `code_invalid` y suma un fallo al convenio).
- **Código de cierre (RNF-17):** 8 símbolos de 32 (sin I, O, 0 ni 1) y token de 32 bytes; en `agreements` solo hay SHA-256; el código en claro sale una sola vez en la respuesta de crear o de `/code`; nunca se registra en logs.
- **Fuerza bruta:** 10/min por usuario, 30/min por IP, 5 intentos por cuenta en 15 minutos (429 `too_many_attempts`, `Retry-After: 900`) y bloqueo del código tras 5 fallos.
- **Atomicidad (RNF-18):** candado de Redis por cuarto; el convenio pasa a `active` con reemplazo condicional; el cuarto pasa a `rented` con su `version`; si el cuarto cambió, el convenio vuelve a `pending` sin código y responde 409 `room_unavailable`. Dos aceptaciones simultáneas: una gana.
- **RNF-02:** la dirección exacta y la ubicación solo salen en Mi renta con convenio `active`; la vista previa y las vistas del arrendador no tienen esos campos; nadie ve teléfono, correo ni ids de usuario.
- **Asignación masiva:** las cuatro entradas rechazan campos desconocidos (`studentId`, `roomId`, `status` → 400).
- **Fallo cerrado:** MongoDB o Redis caídos → 503.
- **Coherencia:** las rutas de NV-12 a NV-17 no cambian; el `code` en 404, 410 y 429 solo aparece cuando un endpoint lo indica, así que las respuestas anteriores siguen iguales (suite completa en verde).

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Índices de los hashes | `code.codeHash` y `code.qrTokenHash` con índice normal (no único). Dos códigos vigentes iguales tienen probabilidad ~1 en 10^12 y aun así se valida la cuenta del estudiante; un índice único habría obligado a reintentar la generación y complicaba el código usado (410). El índice único parcial por cuarto sí está |
| Intentos por cuenta | Se cuentan todos los intentos con código (revisar y aceptar), no solo los fallidos, porque el contador de Redis no se puede consultar sin sumar. Es más estricto que la spec; el flujo normal usa 2 de 5 |
| Candado ocupado | 409 `agreement_state` |
| Efectos al rentar | Rechazar solicitudes y cancelar visitas se reintenta 3 veces en la misma petición; si aun así falla, la renta queda y se registra una advertencia solo con el id del convenio |
| `endDate` | Exclusiva para el próximo pago: no se calcula un pago en la fecha de fin |

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv18_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
