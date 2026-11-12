# NV-18 — Evidencia: cierre de trato con QR y renta

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-12 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-12: NV-18 1.1.0, tasks T01–T11 (M-BE-070) |
| Rama | `feat/nv-18-deal-closing` → PR #43, merge `6dcf73d` |
| Resultado | **✅ PASS WITH WARNINGS** — T01–T11 completas: CI 876/876, desplegado en `ca-thehouse-u2--c6dcf73d-41-1`, prueba HTTP real en Azure 28/28 |

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
| R08 Contrato para Mobile y despliegue | CD run 43308222051; prueba HTTP real en Azure 28/28 (§6); contrato entregado (§7) |
| R09 Pruebas, seguridad y evidencia | TRX del CI run 43307781030: 876/876; `TheHouse.SecurityTests` 95/95; cobertura en §5; esta evidencia |

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

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#42](https://github.com/thehouse-team/TheHouse-Backend/pull/42) (spec 1.1.0) incluido en [#43](https://github.com/thehouse-team/TheHouse-Backend/pull/43) (aprobación e implementación), aprobado y mezclado por `IsaiAxel` el 2026-11-12 (merge `6dcf73d`). El conflicto de `specs/README.md` con #41 se resolvió conservando ambas filas |
| CI de `main` (`6dcf73d`, run 43307781030) | 7/7 checks obligatorios en verde; mutación semanal omitida |
| Pruebas en CI | 876/876: unitarias 327, integración 454, seguridad 95 |
| Cobertura de líneas (CI) | 68.8 % total (ramas 71.7 %). `Rental` 100 %, `RentalServices` 97.7 %, `RentalsEndpoint` 97.3 %. `MongoAgreementRepository` en 0 %, como los demás adaptadores Mongo: se verifica en Azure |
| CD (run 43308222051) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c6dcf73d-41-1`, 100 % del tráfico, Healthy |
| Rol admin temporal (M-BE-065) | Autorizado por Victor el 2026-11-12 («autorizo el rol»). `--grant-role` a la cuenta admin de prueba antes de la corrida y `--revoke-role` justo después (usuario `6af62e5ad51c6e14a29900a5`), vía `az containerapp exec` en esa revisión. El primer intento de asignación falló por un error de DNS local hacia `management.azure.com`; el segundo funcionó |

## 6. Prueba HTTP real en Azure

`.azure-check/nv18_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-12 con cuatro cuentas `@example.com` nuevas (estudiante, otro estudiante, arrendador y admin); no se imprimen tokens, contraseñas, códigos de cierre ni direcciones. Resultado: **28/28 PASS**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Preparación | Edificio de prueba creado (201) y aprobado por el admin (200) | 2/2 |
| Solicitudes (NV-16) | Dos estudiantes piden informes del mismo cuarto; el otro solicita una visita; el arrendador acepta al primero | 4/4 |
| Crear el convenio | 403 `landlord_required`; 409 `request_state` desde una solicitud pendiente; crear 201 con los campos del §6.1, `closingCode` y `no-store`; pendiente en MXN y estudiante solo con nombre y universidad; 409 `agreement_open`; el detalle ya no trae el código | 6/6 |
| Revisar y aceptar | 404 `code_invalid` con otra cuenta; revisar con el código en minúsculas y guion (200) sin dirección; aceptar sin `acceptedTerms` 400; aceptar con el QR 200 con dirección, ubicación y `conversationId`, sin teléfono; cuarto `rented`; la otra solicitud `rejected` con `not_available`; la visita del otro estudiante cancelada por el arrendador; 410 `code_used` | 10/10 |
| Mi renta y fin de renta | Mi renta 200 con `nextPayment` y `no-store`; el arrendador ve el convenio activo; terminar 200 (`ended`); el cuarto vuelve a `available`; 404 `no_active_rental` | 5/5 |
| Limpieza | Edificio de prueba suspendido | 1/1 |

## 7. Contrato entregable a Mobile

Entregado el 2026-11-12 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 9 rutas son nuevas y no cambia ninguna anterior.

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 43307781030): 876/876 (unitarias 327, integración 454, seguridad 95); prueba HTTP real en Azure 28/28 (2026-11-12)
```

## 8. Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | Los intentos por cuenta cuentan cada revisión o aceptación, no solo los fallidos (el contador de Redis no se consulta sin sumar) | Baja | Aceptado: más estricto que la spec; el flujo normal usa 2 de 5 |
| H2 | Índices de los hashes del código no únicos (la spec decía único parcial) | Baja | Aceptado: colisión ~1 en 10^12 y se valida la cuenta; el índice único por cuarto sí está |
| H3 | Adaptadores Mongo sin cobertura en CI | Baja | Aceptado: se verifican con la prueba real en Azure, como en versiones anteriores |
| H4 | El estudiante ve «Pagar» en el prototipo, pero TheHouse no cobra (RES-03) | Información | Mobile oculta o deshabilita el botón; `nextPayment` es solo informativo |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos; una colección pequeña (`agreements`) con cinco índices en MongoDB Atlas M0 y llaves de Redis con expiración, dentro de los planes gratuitos. Revisión de coherencia: spec, plan, tasks, README y esta evidencia indican 1.1.0 completa 11/11; las rutas de NV-12 a NV-17 no cambiaron.
