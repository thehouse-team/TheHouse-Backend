# NV-10 — Sesión: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `spec.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | Requisitos del curso §16 (eliminar cuenta, sesión siempre viva, aviso de privacidad); RF-01 (términos y aviso de privacidad), RF-02 (sesión persistente), RF-05 (ajustes de la cuenta); política de Google Play para apps con cuentas (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-09 |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |
| Módulo | Identity |
| Precondición | NV-10 1.1.0 y NV-11 a NV-22 desplegadas |
| Relación | Extiende NV-05/NV-06/NV-10 1.1.0; borra o anonimiza datos de Profile (NV-11), Listings (NV-12), Media (NV-13), Favorites (NV-15), Messaging (NV-16), Visits (NV-17), Rentals (NV-18), Reviews (NV-19), Notifications (NV-20), Roommates (NV-22) y Analytics (NV-25); reutiliza la revocación de sesiones de NV-21 |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.2.0-borrador (2026-11-17):** la versión 1.1.0 (refresh token y `/users/me`) quedó completa y desplegada (ver `evidence.md`). Esta versión cubre tres requisitos del curso (§16 Móvil de «Requerimientos para el desarrollo del proyecto Móvil v3»): **eliminar cuenta** desde la app, la **sesión siempre viva** salvo que el usuario cierre sesión y el **aviso de privacidad y manejo de datos** vinculado a la landing page. La eliminación es inmediata e irreversible, pide la contraseña y explica antes qué se borra. Los datos personales se borran; lo que otra persona necesita conservar (sus conversaciones, convenios terminados y las reseñas) queda como «Usuario eliminado». El tope absoluto de 90 días de la sesión se retira: la sesión dura mientras la app se use al menos una vez cada 30 días. Todo es aditivo: ninguna respuesta existente pierde campos.

## 1. Contexto

El curso pide que la app tenga «Eliminar cuenta», que la sesión siga viva salvo logout y que el aviso de privacidad esté en la app y en la landing page. Google Play también exige que una app que crea cuentas permita borrarlas desde la app y desde una página web. Hoy no hay forma de borrar una cuenta, la sesión vence a los 90 días aunque se use y el backend no registra qué versión del aviso aceptó cada persona. Mobile ya tiene la pantalla, la confirmación, la limpieza local y el logout preparados y solo espera el contrato.

## 2. Objetivo

Que cualquier usuario pueda ver qué pasará y eliminar su cuenta desde la app con su contraseña, que sus datos personales desaparezcan y sus sesiones se cierren en todos sus dispositivos; que la sesión no venza mientras la app se use; y que la app sepa qué versión del aviso de privacidad y de los términos aceptó el usuario.

## 3. Precondiciones

1. NV-10 1.1.0 y NV-11 a NV-22 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. URLs de la landing page para términos, aviso de privacidad y eliminación de cuenta.
4. Autorización «EJECUTA».

## 4. Alcance

1. Vista previa de la eliminación: qué se borra, qué se cancela y si algo la impide.
2. Eliminación inmediata con contraseña y confirmación escrita.
3. Borrado de datos personales y anonimización de lo que comparte con otras personas, módulo por módulo.
4. Cierre de todas las sesiones (refresh y access tokens) al eliminar.
5. Sesión sin tope absoluto: vence solo tras 30 días sin usarse, con logout, suspensión o eliminación.
6. Versiones y URLs vigentes de términos y aviso de privacidad, y registro de la aceptación del usuario.
7. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Periodo de gracia o recuperación de una cuenta eliminada.
- Formulario web de eliminación en el backend: la landing page explica cómo hacerlo y da un correo de contacto.
- Exportar los datos del usuario (portabilidad).
- Cambio o recuperación de contraseña (RF-04) y verificación de correo (RF-36).
- Textos legales: viven en la landing page; el backend solo publica versiones y URLs.
- Cuentas de staff: primero se retira el rol (NV-21).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me/deletion` | Usuario | Vista previa: efectos y bloqueos | 200, 401, 429, 503 |
| POST | `/api/v1/users/me/deletion` | Usuario | Eliminar la cuenta | 204, 400, 401, 403, 409, 429, 503 |
| GET | `/api/v1/legal` | Público | Versiones y URLs de términos, aviso de privacidad y eliminación | 200, 429 |
| PUT | `/api/v1/users/me/legal` | Usuario | Registrar la aceptación de la versión vigente | 200, 400, 401, 409, 429, 503 |
| GET | `/api/v1/users/me` | Usuario | Cambio aditivo: `legal` y `legalUpToDate` | 200, 401, 503 |
| POST | `/api/v1/auth/refresh` | Público con refresh token | Cambio de comportamiento: sin tope absoluto de 90 días | 200, 400, 401, 429, 503 |

- Vista previa: `{ canDelete, blockers: [{ code: "active_rental" | "staff_account", count }], effects: { buildings, rooms, upcomingVisits, openRequests, pendingAgreements, conversations, reviews, favorites, roommateProfile } }`. `canDelete` es falso si hay algún bloqueo; `effects` cuenta lo que se borra o cancela para mostrarlo en la confirmación.
- Eliminar: cuerpo `{ password, confirmation: "ELIMINAR" }`. Contraseña incorrecta → 403 `password_mismatch` (no 401, para que la app no cierre la sesión); bloqueo → 409 `deletion_blocked` con `blockers`; éxito → 204 y todas las sesiones quedan revocadas. Si un paso falla → 503 `deletion_incomplete`: la sesión sigue viva y repetir la llamada termina el trabajo (cada paso es idempotente).
- Después del 204: el access token responde 401 en cualquier ruta, el refresh responde 401 y el login con ese correo responde 401 como una cuenta inexistente. El correo queda libre para registrarse de nuevo como una cuenta nueva sin relación con la anterior.
- Legal: `GET /legal` → `{ termsVersion, privacyVersion, landlordTermsVersion, termsUrl, privacyUrl, accountDeletionUrl, updatedAt }`, público con `Cache-Control: public, max-age=3600`. `PUT /users/me/legal` con `{ termsVersion, privacyVersion }` → 200 `{ termsVersion, privacyVersion, acceptedAt }`; una versión distinta de la vigente → 409 `legal_version_outdated`.
- `GET /users/me` agrega `legal: { termsVersion, privacyVersion, acceptedAt } | null` y `legalUpToDate` (verdadero si aceptó las versiones vigentes). La app pide aceptar de nuevo cuando sea falso.
- Refresh: la sesión ya no vence a los 90 días; cada rotación extiende 30 días. La detección de reuso, el logout, la suspensión (NV-21) y la eliminación la siguen cerrando.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/users/me/deletion
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { canDelete, blockers: [{ code: "active_rental" | "staff_account", count }], effects: { buildings, rooms, upcomingVisits, openRequests, pendingAgreements, conversations, reviews, favorites, roommateProfile } }
STATUS CODES 200, 401, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/deletion
METHOD       POST
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { password, confirmation: "ELIMINAR" }
RESPONSE     204 sin cuerpo; después: 401 en toda ruta autenticada y en refresh
STATUS CODES 204, 400, 401, 403 (password_mismatch), 409 (deletion_blocked), 429, 503 (deletion_incomplete)
PAGINATION   No
```

```text
ENDPOINT     /api/v1/legal
METHOD       GET
AUTH         Pública
HEADERS      —
REQUEST      —
RESPONSE     200 { termsVersion, privacyVersion, landlordTermsVersion, termsUrl, privacyUrl, accountDeletionUrl, updatedAt }
STATUS CODES 200, 429
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me/legal
METHOD       PUT
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      { termsVersion, privacyVersion }
RESPONSE     200 { termsVersion, privacyVersion, acceptedAt }
STATUS CODES 200, 400, 401, 409 (legal_version_outdated), 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/users/me
METHOD       GET
AUTH         Bearer JWT (usuario tomado del token; nunca `userId` del cliente)
HEADERS      —
REQUEST      —
RESPONSE     200 { …campos de 1.1.0, legal: { termsVersion, privacyVersion, acceptedAt } | null, legalUpToDate }
STATUS CODES 200, 401, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Identity** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `users`, `refresh_tokens`, `deleted_accounts` |
| Usa (puertos de otros módulos) | — |
| Publica (eventos de dominio) | `Puerto `IAccountDeletionParticipant` (`CheckAsync` devuelve bloqueos y efectos; `EraseAsync` borra, cancela o anonimiza de forma idempotente), en `Application/Abstractions` para que Identity no dependa de los demás módulos` |
| Consume (eventos de dominio) | `Implementaciones de `IAccountDeletionParticipant` en Profile (NV-11), Listings (NV-12), Media (NV-13), Favorites (NV-15), Messaging (NV-16), Visits (NV-17), Rentals (NV-18, bloqueo por renta activa), Reviews (NV-19), Notifications (NV-20), Moderation (NV-21, bloqueo por staff), Roommates (NV-22) y Analytics (NV-25)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `deleted_accounts` (colección nueva de Identity): `userId`, `deletedAt`. Solo el id interno, sin correo ni nombre; sirve para mostrar «Usuario eliminado» y para auditoría.
- `users` agrega `legal { termsVersion, privacyVersion, acceptedAt }`; sin migración (ausente = `null`).
- `refresh_tokens`: los tokens nuevos ya no llevan tope absoluto; los vigentes conservan el suyo hasta su próxima rotación (sin migración).
- Versiones legales y URLs en configuración (`Legal__TermsVersion`, `Legal__PrivacyVersion`, `Legal__TermsUrl`, `Legal__PrivacyUrl`, `Legal__AccountDeletionUrl`); no son secretos.

## 9. Reglas de negocio

1. Se pide la contraseña actual y la palabra `ELIMINAR`; 5 intentos por hora por usuario.
2. Bloquea la eliminación: un convenio `active` como estudiante o como arrendador (`active_rental`, hay que terminarlo con NV-18) y un rol de staff (`staff_account`).
3. Se borra: cuenta (correo y hash de contraseña), perfil y preferencias (NV-11), perfil de compañero (NV-22), favoritos (NV-15), notificaciones (NV-20), refresh tokens, perfil de arrendador (NV-12), documentos de verificación con sus archivos (NV-13), fotos de sus edificios y cuartos (NV-13) y las vistas de sus cuartos (NV-25).
4. Se cancela con motivo `account_deleted` y aviso a la otra persona: visitas futuras (NV-17), solicitudes abiertas (NV-16) y convenios `pending` (NV-18).
5. Se retira: sus edificios y cuartos pasan a `deleted` y dejan de aparecer en búsqueda, mapa y detalle (404).
6. Se anonimiza (la otra persona conserva su historial): conversaciones y mensajes (la conversación queda de solo lectura con «Usuario eliminado»), convenios terminados o cancelados y reseñas escritas (siguen visibles con autor «Usuario eliminado»). Reportes y auditoría de moderación (NV-21) conservan solo el id interno.
7. Orden: primero se limpian los módulos (con la sesión viva, para poder reintentar), al final se revocan todas las sesiones y se borra la cuenta. Cada paso es idempotente.
8. Arquitectura: Identity publica el puerto `IAccountDeletionParticipant` y cada módulo lo implementa para sus propios datos; Identity no conoce a los demás módulos (sin dependencias circulares, constitution §6).
9. Sesión: 30 días sin usar la app la vencen; usarla la renueva sin límite; logout, reuso de un refresh rotado, suspensión y eliminación la cierran.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Reautenticación con contraseña para eliminar (protege un teléfono desbloqueado o un token robado).
- BOLA: solo la cuenta del token; no existe forma de eliminar otra cuenta por esta ruta.
- Tras eliminar, el access token se rechaza en todas las réplicas (revocación por sesión en Redis, NV-10/NV-21).
- Sin correo, nombre ni contraseña en logs; el evento de auditoría guarda solo el id interno y la hora.
- Rate limit: 5 eliminaciones intentadas por hora por usuario y 30 lecturas por minuto; Redis caído → 503.
- Contratos estrictos: campos desconocidos → 400.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Contraseña incorrecta | 403 | Contraseña incorrecta (`password_mismatch`) |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Renta activa o cuenta de staff | 409 | No se puede eliminar (`deletion_blocked`) |
| Versión legal distinta de la vigente | 409 | Versión desactualizada (`legal_version_outdated`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-10-R01 | Vista previa y bloqueos | La vista previa cuenta lo que se borra o cancela y lista los bloqueos (`active_rental`, `staff_account`); coincide con lo que hace la eliminación. | F1 | T01 |
| NV-10-R02 | Eliminación de cuenta | Contraseña y `ELIMINAR`; cada dato de la tabla de reglas queda borrado, cancelado, retirado o anonimizado (una prueba por módulo); pasos idempotentes; un fallo deja la sesión viva y repetir termina; al final 401 en toda ruta, refresh y login. | F2 | T02 |
| NV-10-R03 | Sesión siempre viva | Sin tope absoluto: una sesión usada cada menos de 30 días no vence; 30 días sin uso, logout, reuso, suspensión o eliminación la cierran; tokens existentes sin migración. | F1 | T03 |
| NV-10-R04 | Aviso de privacidad y términos | `GET /legal` con versiones y URLs; aceptación registrada; `/users/me` con `legal` y `legalUpToDate`; versión distinta → 409. | F1 | T04 |
| NV-10-R05 | Seguridad, privacidad y límites | Reautenticación, rate limit, sin PII en logs, archivos borrados del almacenamiento, otra cuenta intacta, Redis caído 503. | F3 | T05 |
| NV-10-R06 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure: cuenta de prueba con perfil, favorito, solicitud y visita, eliminada, y la contraparte ve «Usuario eliminado». | F4 | T06 |
| NV-10-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Momento | Eliminación inmediata e irreversible, sin periodo de gracia | PROPUESTO |
| Confirmación | Contraseña actual y la palabra `ELIMINAR` | PROPUESTO |
| Bloqueos | Una renta activa (como estudiante o arrendador) impide eliminar hasta terminarla; staff primero pierde el rol | PROPUESTO |
| Datos compartidos | Conversaciones, convenios terminados y reseñas se conservan con «Usuario eliminado» y el texto original; lo demás se borra | PROPUESTO |
| Sesión | Sin tope absoluto de 90 días; vence tras 30 días sin uso (requisito del curso: sesión viva salvo logout) | PROPUESTO |
| Legal | Versiones y URLs en configuración; los textos viven en la landing page; aceptación registrada en `users` | PROPUESTO |
| Correo | Queda libre al eliminar; registrarse de nuevo crea una cuenta nueva sin relación | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`deleted_accounts`) y un campo en `users`; sin recursos nuevos |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-10-RK01 | Eliminar por error | Pérdida de la cuenta | Vista previa, contraseña y `ELIMINAR`; Mobile muestra la confirmación |
| NV-10-RK02 | Fallo a mitad | Datos a medias | Pasos idempotentes con la sesión viva hasta el final; repetir termina el trabajo |
| NV-10-RK03 | Evadir una suspensión registrándose de nuevo | Abuso | Se acepta en el piloto; los reportes y la moderación (NV-21) siguen aplicando a la cuenta nueva |
| NV-10-RK04 | Sesiones largas en un teléfono robado | Acceso indebido | Logout remoto por suspensión y eliminación; reuso de refresh cierra la sesión; se reconsidera si aparece abuso |

## 16. Dependencias

- NV-11 a NV-22 y NV-25 (datos que se borran o anonimizan).
- NV-20 1.2.0 (dispositivos de push): si se despliega antes, la eliminación también borra los dispositivos.
- Landing page con términos, aviso de privacidad y la página de eliminación de cuenta (Mobile y landing).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
