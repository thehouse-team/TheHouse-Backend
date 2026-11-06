# NV-04 — Evidence Registration U2

| Campo | Valor |
| --- | --- |
| ID | NV-04 |
| Documento | `evidence.md` |
| Versión | 1.0.0-u2 |
| Fecha de cierre | 2026-11-03 |
| Alcance | U2 |
| Endpoint | `POST /api/v1/auth/register` |
| Estado | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Autorización Fase B | Confirmada |
| Rama | `feature/u2-entrega` |

## 1. Resultado general

NV-04 Registration fue implementada y validada dentro del alcance autorizado de la Unidad 2.

El endpoint implementado es exclusivamente:

`POST /api/v1/auth/register`

No se implementaron como parte de NV-04:

- Login.
- Logout.
- Properties.
- Universities.
- Refresh tokens.
- Docker/Cloud.
- Terraform apply.
- NV-02.

## 2. Commits de implementación y corrección

### Implementación y validación inicial

`428439b test: validate nv04 registration on linux`

Incluyó:

- contrato RegisterRequest/RegisterResponse;
- normalización y validación de email;
- política de contraseña;
- PBKDF2-HMAC-SHA256;
- repositorio MongoDB;
- índice único de email;
- rate limiting distribuido mediante Redis;
- endpoint HTTP;
- Problem Details;
- pruebas unitarias;
- pruebas de seguridad;
- pruebas de integración;
- contrato OpenAPI.

### Corrección de pruebas Linux

`6627879 test: fix nv04 linux integration assertions`

La primera validación Linux detectó dos problemas dentro del harness de pruebas:

1. El contenido HTTP chunked de la prueba de límite de cuerpo no establecía `Content-Type: application/json`, provocando correctamente un 415 antes del 413 esperado.
2. La prueba de configuración inválida buscaba el carácter `0` dentro de `Exception.ToString()`, incluyendo el stack trace, generando un falso positivo.

Ambos casos fueron corregidos sin modificar la lógica funcional del endpoint Registration.

## 3. Build local

Comando:

`dotnet build`

Resultado:

**PASS**

Los proyectos compilaron correctamente bajo .NET 10:

- TheHouse.Domain
- TheHouse.Application
- TheHouse.Infrastructure
- TheHouse
- TheHouse.UnitTests
- TheHouse.SecurityTests
- TheHouse.IntegrationTests

No se reportaron errores de compilación.

## 4. Validación CI Linux

Workflow:

`U2 Linux Validation`

Run:

`41718313761`

Job:

`126204066619`

Resultado:

**✅ SUCCESS**

El workflow ejecutó en GitHub Actions:

1. Checkout.
2. Setup .NET 10.
3. Restore.
4. Build Release.
5. Tests.
6. Publicación de resultados.

Artefacto generado:

`u2-test-results`

La validación Linux constituye la ejecución automatizada principal de pruebas para NV-04 debido a la política local de Windows Smart App Control detectada previamente en el entorno de desarrollo.

## 5. Evidencia por task

| Task | Evidencia | Estado |
| --- | --- | --- |
| T01 | RegisterRequest y RegisterResponse con contrato explícito | ✅ COMPLETADA |
| T02 | EmailNormalizer, PasswordPolicy, body limit y pruebas negativas | ✅ COMPLETADA |
| T03 | MongoUserRepository e índice único `ux_users_email` | ✅ COMPLETADA |
| T04 | Pbkdf2PasswordHasher, salt CSPRNG, SHA-256 y factor configurable | ✅ COMPLETADA |
| T05 | RegistrationService con normalización, hashing, persistencia y conflicto | ✅ COMPLETADA |
| T06 | `POST /api/v1/auth/register` con respuesta 201 | ✅ COMPLETADA |
| T07 | Problem Details sanitizados y traceId | ✅ COMPLETADA |
| T08 | RedisRegistrationRateLimiter con cuota 10/60 s y fail-closed | ✅ COMPLETADA |
| T09 | RegistrationValidationTests y RegistrationHashingTests | ✅ COMPLETADA |
| T10 | RegistrationHttpTests y contrato OpenAPI | ✅ COMPLETADA |
| T11 | Pruebas de seguridad, mass assignment, hashing y sanitización | ✅ COMPLETADA |

## 6. Seguridad

### Password

Se implementó:

- PBKDF2-HMAC-SHA256;
- salt de 128 bits generado mediante CSPRNG;
- subkey de 256 bits;
- factor de trabajo configurable;
- mínimo de 210,000 iteraciones;
- máximo de 2,000,000;
- formato versionado;
- verificación del hash;
- salts independientes para contraseñas iguales.

La contraseña:

- no se persiste en texto plano;
- no se devuelve en respuestas;
- no se incluye en DTO de salida;
- no se normaliza ni recorta antes del hashing.

### Mass assignment

El endpoint utiliza un DTO explícito.

No deserializa directamente:

- User;
- UserDocument;
- Role;
- Status;
- PasswordHash;
- Id interno.

Los campos desconocidos son rechazados.

### MongoDB

La implementación utiliza un índice único:

`ux_users_email`

sobre:

`email`

La inserción depende del índice de MongoDB y no realiza una preconsulta susceptible a condiciones de carrera.

`DuplicateKey` es convertido a conflicto de dominio y posteriormente a HTTP 409.

### Redis

Registration implementa un rate limiter distribuido mediante Redis:

- 10 solicitudes;
- ventana de 60 segundos;
- clave derivada de la dirección cliente;
- `INCR` + expiración mediante script Redis;
- HTTP 429 al exceder la cuota;
- `Retry-After: 60`;
- Redis no disponible => HTTP 503;
- sin fallback local en memoria.

## 7. Contrato HTTP validado

Se contemplan respuestas:

| Status | Significado |
| --- | --- |
| 201 | Registro exitoso |
| 400 | Solicitud inválida |
| 409 | Email ya registrado |
| 413 | Payload demasiado grande |
| 415 | Content-Type no permitido |
| 429 | Rate limit excedido |
| 503 | Dependencia no disponible |
| 500 | Error interno sanitizado |

Los errores utilizan `application/problem+json` y no exponen:

- stack traces;
- rutas locales;
- contraseña;
- password hash;
- detalles internos de MongoDB.

## 8. OpenAPI

OpenAPI fue actualizado para autorizar únicamente Registration dentro del alcance funcional actual.

Se mantiene fuera del contrato U2 de NV-04:

- `/api/v1/auth/login`
- `/api/v1/auth/logout`
- `/api/v1/properties`
- `/api/v1/universities`

El password está documentado como:

- `writeOnly`;
- mínimo 12 caracteres;
- máximo 128 caracteres.

## 9. Observaciones y límites de evidencia

### MongoDB real

La implementación e índice único fueron revisados y cubiertos a nivel de código y contrato.

NV-04 no afirma todavía una prueba contra una instancia MongoDB externa real.

La validación contra infraestructura real se realizará durante NV-03 / validación Cloud.

### Redis real

La semántica distribuida y fail-closed está implementada y las pruebas HTTP utilizan un contrato compartido entre hosts.

NV-04 no afirma todavía una prueba contra un servicio Redis externo real.

La comprobación con infraestructura distribuida real corresponde a NV-03.

### Windows Smart App Control

El entorno Windows local presentó anteriormente bloqueos de Code Integrity sobre assemblies de pruebas.

Por este motivo:

- el build local se mantiene como evidencia local;
- GitHub Actions Ubuntu se utiliza como ejecución autoritativa de las suites automatizadas.

Esto no representa un fallo funcional de NV-04.

## 10. Revisión SDD

Resultado:

**PASS WITH WARNINGS**

No se detectaron contradicciones bloqueantes con:

- constitution.md;
- AGENTS.md;
- memoria.md;
- spec.md;
- plan.md;
- task.md.

La advertencia restante corresponde exclusivamente a la validación futura contra MongoDB/Redis reales dentro de NV-03.

## 11. Revisión DevSecOps

Resultado:

**PASS WITH WARNINGS**

No existen hallazgos Critical o High conocidos que bloqueen el cierre de NV-04.

Pendiente fuera de NV-04:

- validación de infraestructura real;
- despliegue cloud;
- prueba multi-réplica real;
- observabilidad cloud;
- coste;
- Terraform.

## 12. Cierre

NV-04 Registration 1.0.0-u2:

**✅ EJECUTADA, VALIDADA Y CERRADA PARA U2**

No quedan tasks U2 abiertas en NV-04.

## Addendum — Correcciones de revisión U2 (2026-11-05)

Correcciones autorizadas por Victor Manuel Jose Jose el 2026-11-05 en la rama `fix/u2-review`. No se modifican requisitos, alcance ni tasks de NV-04.

- Problem Details de registro: títulos en español y consistentes (`Validación fallida`, `Solicitud demasiado grande`, `Tipo de contenido no admitido`, `Conflicto`, `Demasiadas solicitudes`, `Servicio no disponible`), con `detail` e `instance` en español.
- Pruebas actualizadas: `RegistrationHttpTests`, `RegistrationFactory` y `RegistrationValidationTests` verifican los títulos y mensajes en español.
- Codificación reparada en pruebas y documentos; sin BOM. `.editorconfig` fija UTF-8.
- Mass assignment: el boundary MVC continúa rechazando miembros JSON desconocidos; no se exponen campos internos ni credenciales.
- Verificación ejecutada: `dotnet build --verbosity quiet` con 0 errores y 0 advertencias; `dotnet test --verbosity quiet` con 130 pruebas unitarias, 39 de seguridad y 227 de integración: 396 aprobadas y 0 fallidas.
- Nota de entorno: `TheHouse/appsettings.Development.json` es un archivo local ignorado por Git, no forma parte del repositorio y habilita JWT sin `JWT_SIGNING_KEY`; para ejecutar la suite se excluyó temporalmente y se restauró byte a byte sin incorporarlo al commit. Esta limitación se declara para no atribuir a la suite una ejecución que dependa de configuración no versionada.
- Seguridad: revisión de OWASP API Security Top 10, secretos, autenticación, autorización, validación de entrada, exposición de datos, errores, logging, rate limiting y mass assignment. Sin hallazgos Critical/High ni secretos confirmados en el alcance revisado. Resultado: PASS WITH WARNINGS. No se ejecutaron SAST, SCA, SBOM, DAST, fuzzing, mutación ni performance.
- SDD: la revisión no crea tasks, no reabre el cierre U2 y no autoriza implementación adicional. Resultado: PASS WITH WARNINGS.
