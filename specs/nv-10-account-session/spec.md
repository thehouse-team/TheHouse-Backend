# NV-10 — Sesión: refresh token y cuenta propia

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `spec.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-06 («Apruebo y EJECUTA»: NV-10 1.1.0, tasks T01–T11, rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/` y `specs/nv-10-account-session/`) |
| Estado de ejecución | **✅ COMPLETADA — DESPLEGADA EN AZURE (revisión `c55feed2-7-1`) Y VERIFICADA 13/13 (2026-11-06)** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-02 (sesión persistente sin volver a iniciar sesión); requisito académico U2 (refresh y `/users/me`). RF-01, RF-04, RF-36 y RF-71 se cubrirán en la versión posterior de NV-10 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-09 |
| Plan | `plan.md` 1.1.0 |
| Tasks | `task.md` 1.1.0 |
| Módulo | Identity |
| Precondición | NV-04, NV-05 y NV-06 cerradas y desplegadas; NV-09 (CD) operativa |
| Relación | Extiende NV-05 (login) y NV-06 (logout) con cambios aditivos; NV-04 (registro) sin cambios |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0 (2026-11-06):** el alcance se reduce a lo que la app móvil necesita para U2: `POST /auth/refresh`, `refreshToken` en el login, revocación de la sesión en el logout y `GET /users/me` con los campos reales. Recuperación de contraseña, verificación de correo, insignia de estudiante, eliminación de cuenta y nuevos datos de registro pasan a una versión posterior de NV-10 (el borrador 1.0.0 queda en el historial de Git). Alcance revisado con el responsable de Mobile: la app ya ignora campos desconocidos (`ignoreUnknownKeys = true`), declara `refreshToken` como opcional y lo guarda cifrado (AES-GCM + Android Keystore).

## 1. Contexto

El login (NV-05) devuelve solo `accessToken`, `tokenType` y `expiresIn`; el access token dura 15 minutos y `IJwtTokenService` solo crea y valida access tokens. El logout (NV-06) revoca el access token por `jti` en Redis con TTL hasta su expiración. No existe refresh token: cuando el access token vence, la app recibe 401, borra la sesión y obliga a iniciar sesión de nuevo. Tampoco existe una forma de consultar la cuenta propia: `User` solo tiene `Id`, `Email`, `PasswordHash`, `CreatedAt` y `UpdatedAt`, e `IUserRepository` no busca por id.

## 2. Objetivo

Agregar sesiones renovables con refresh tokens rotativos y revocables, integrarlas con el login y el logout sin romper sus contratos, y exponer `GET /users/me` con los campos reales del usuario autenticado.

## 3. Precondiciones

1. NV-04, NV-05 y NV-06 desplegadas en Azure.
2. Redis (Upstash) y MongoDB Atlas operativos.
3. Autorización «EJECUTA» que incluya las rutas protegidas necesarias (`TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/` y los tres proyectos de pruebas).

## 4. Alcance

1. Refresh token opaco emitido en el login como campo adicional `refreshToken` (login sigue en 200).
2. `POST /auth/refresh` con rotación en cada uso, detección de reuso y revocación de la sesión completa.
3. Identificador de sesión `sid` como claim adicional del access token (los tokens sin `sid` siguen siendo válidos hasta su expiración).
4. Logout revoca también la sesión del refresh token (sigue en 204).
5. Rechazo inmediato de access tokens de una sesión revocada.
6. `GET /users/me` con `id`, `email` y `createdAt`.
7. Pruebas de regresión de register 201, login 200 y logout 204.

## 5. Fuera de alcance

- Cambios en `POST /auth/register`: sigue recibiendo solo `email` y `password` y respondiendo 201.
- Nombre, aceptación de términos, recuperación de contraseña (RF-04), verificación de correo (RF-36), insignia de estudiante (RF-71) y eliminación de cuenta: versión posterior de NV-10.
- Campos de `/users/me` que el modelo no tiene (`name`, `emailVerified`, `studentBadge`, `landlordEnabled`).
- Inicio de sesión con Google y verificación por SMS.
- Cerrar todas las sesiones del usuario ("cerrar sesión en todos los dispositivos").

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/login` | Público | Sin cambios en la entrada; la respuesta agrega `refreshToken` | 200, 400, 401, 429, 503 (sin cambios) |
| POST | `/api/v1/auth/refresh` | Público (el refresh token es la credencial) | Rota el refresh token y emite un access token nuevo | 200, 400, 401, 429, 503 |
| POST | `/api/v1/auth/logout` | Autenticado | Sin cambios en el contrato; además revoca la sesión `sid` | 204, 401, 429, 503 (sin cambios) |
| GET | `/api/v1/users/me` | Autenticado | Cuenta propia: `id`, `email`, `createdAt` | 200, 401, 503 |

- Login 200: `{ accessToken, tokenType, expiresIn, refreshToken }`; los tres primeros campos no cambian de nombre, tipo ni significado.
- Refresh: entrada `{ "refreshToken": "..." }` (43 caracteres base64url); respuesta 200 con la misma forma que el login, incluido el `refreshToken` nuevo. El anterior deja de servir.
- Cualquier falla del refresh (token desconocido, vencido, revocado o reusado) responde el mismo 401 sin distinguir la causa.
- `/users/me` 200: `{ "id": "...", "email": "...", "createdAt": "2026-11-06T12:00:00Z" }` con `Cache-Control: no-store`; nuevos campos solo se agregan de forma aditiva cuando existan en el dominio.
- Register: entrada `{ email, password }` y 201 sin ningún cambio.

## 7. Módulo, datos propios e integración

Módulo **Identity** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `users`, `refresh_tokens` |
| Usa (puertos de otros módulos) | — |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `refresh_tokens`: `tokenHash` (SHA-256, índice único), `sessionId` (= `sid`, índice), `userId`, `createdAt`, `expiresAt` (índice TTL), `sessionExpiresAt`, `rotatedAt`, `revokedAt`, `revokedReason` (`logout`, `reuse`).
- Access token: claim adicional `sid` (aleatorio, 128 bits) con el identificador de la sesión; `sub`, `email`, `jti`, `iat` y `exp` no cambian.
- Redis: clave `auth:revoked-sid:{sid}` con TTL igual a la vida del access token (15 min) para rechazar de inmediato los access tokens de una sesión revocada.
- `IUserRepository` agrega la búsqueda por id; `User` no cambia.

## 9. Reglas de negocio

1. El refresh token son 32 bytes aleatorios de un generador criptográfico, codificados en base64url; solo se almacena su hash SHA-256.
2. Cada refresh token vale 30 días desde su emisión y la sesión completa como máximo 90 días desde el login; vencido cualquiera de los dos, el refresh responde 401.
3. La rotación es atómica: una actualización condicional marca el token como rotado solo si no estaba rotado ni revocado; de dos solicitudes simultáneas con el mismo token, solo una obtiene tokens nuevos (constitution §20).
4. Presentar un refresh token ya rotado se trata como reuso: se revoca toda la sesión (todos sus refresh tokens y la clave `revoked-sid` en Redis) y se responde 401.
5. El refresh valida que el usuario siga existiendo; si no, revoca la sesión y responde 401.
6. El logout mantiene su comportamiento (revoca el `jti` del access token) y, si el token trae `sid`, revoca además la sesión; un access token antiguo sin `sid` funciona como hoy.
7. El middleware de revocación rechaza con 401 cualquier access token cuyo `sid` esté revocado.
8. Si el login no puede guardar el refresh token, responde 503 y no emite tokens (sin sesiones a medias).
9. `/users/me` usa solo el `sub` del token; no recibe identificadores en la ruta ni en la consulta.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- El refresh token viaja solo en el cuerpo JSON por HTTPS, nunca en cookies ni en la URL (sin riesgo CSRF); la app lo guarda cifrado con Android Keystore y mantiene el access token solo en memoria.
- Refresh tokens, `sid` revocados y hashes nunca aparecen en logs, Problem Details ni evidencia.
- Rate limit de refresh: 30 solicitudes por 60 s por IP en Redis; si Redis no está disponible, el refresh y la validación de revocación fallan cerrados con 503.
- La detección de reuso protege contra el robo de un refresh token: el primer uso del token robado o del legítimo invalida la sesión entera.
- `/users/me` no permite consultar a otros usuarios (BOLA imposible por diseño) y no devuelve `PasswordHash` ni `UpdatedAt`.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Refresh token desconocido, vencido, revocado o reusado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-10-R01 | Refresh token en el login | Login 200 devuelve `refreshToken` además de los campos actuales sin cambiar su forma; el access token incluye `sid`; en la base solo queda el hash; prueba de contrato que compara la respuesta con la de NV-05. | F1 | T01 |
| NV-10-R02 | Renovación con rotación | `POST /auth/refresh` con token válido devuelve access y refresh nuevos y el anterior deja de servir; token desconocido, vencido o revocado 401 uniforme; cuerpo inválido 400; vencimiento de 30 días por token y 90 por sesión. | F1 | T02 |
| NV-10-R03 | Cuenta propia | `GET /users/me` con Bearer válido devuelve exactamente `id`, `email` y `createdAt` del `sub`; sin token o con token revocado 401; `Cache-Control: no-store`. | F1 | T03 |
| NV-10-R04 | Persistencia e índices | `refresh_tokens` con índice único en `tokenHash`, índice por `sessionId` e índice TTL; búsqueda de usuario por id con proyección explícita. | F2 | T04 |
| NV-10-R05 | Detección de reuso y concurrencia | Reusar un token rotado revoca toda la sesión y responde 401; dos refresh simultáneos con el mismo token producen una sola emisión; pruebas de ambos casos. | F2 | T05 |
| NV-10-R06 | Logout integrado | Logout sigue respondiendo 204 y revoca el `jti` como hoy; con `sid` revoca también la sesión y su refresh token deja de servir; un access token sin `sid` sigue funcionando hasta su expiración. | F2 | T06 |
| NV-10-R07 | Rechazo inmediato de sesiones revocadas | Tras el logout o un reuso, cualquier access token de esa sesión recibe 401 en la siguiente solicitud, en cualquier réplica (estado en Redis). | F3 | T07 |
| NV-10-R08 | Límites y no regresión | Rate limit de refresh con 429 y `Retry-After`; fallo de Redis 503; pruebas de regresión de register 201, login 200 y logout 204 con los contratos actuales. | F3 | T08 |
| NV-10-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias (generación, hash, vencimientos, rotación), de integración (login, refresh, logout, `/users/me`, register sin cambios) y de seguridad (reuso, concurrencia, sesión revocada en otra réplica, rate limit, Redis caído, ausencia de tokens en logs); después del despliegue por el CD, prueba HTTP real en Azure del flujo login → `/users/me` → refresh → logout; `evidence.md` con resultados y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Vigencia | 30 días por refresh token y 90 días como máximo por sesión | PROPUESTO |
| Refresh simultáneos | Sin periodo de gracia: el segundo uso cuenta como reuso; la app debe serializar las renovaciones (una sola renovación en curso) | PROPUESTO |
| Alcance diferido | Recuperación de contraseña, verificación de correo, insignia, eliminación de cuenta y datos de registro en una versión posterior de NV-10 | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Documentos pequeños con TTL |
| Redis Upstash | $0 | Claves con TTL de 15 minutos; vigilar el límite de comandos del plan gratuito |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-10-RK01 | Robo del refresh token | Sesión prolongada de un atacante | Rotación, detección de reuso y almacenamiento cifrado en el dispositivo |
| NV-10-RK02 | Falso reuso por reintentos de red | El usuario tiene que volver a iniciar sesión | Renovación serializada en la app; decisión documentada |
| NV-10-RK03 | Romper la app actual | Login o registro fallan en Mobile | Cambios solo aditivos, confirmados con Mobile, y pruebas de regresión |
| NV-10-RK04 | Redis no disponible | Refresh y validación de revocación con 503 | Fallo cerrado (constitution §14) y monitoreo |
| NV-10-RK05 | Crecimiento de la colección | Límite de 512 MB de M0 | Índice TTL y tokens de 30 días |

## 16. Dependencias

- NV-05 (login) y NV-06 (logout), que se extienden de forma aditiva.
- NV-04 (registro), sin cambios.
- NV-09 (CD) para desplegar y probar en Azure.
- App móvil: conectará refresh y `/users/me` cuando se anuncie "NV-10 desplegada".

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 11 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **✅ COMPLETADA — DESPLEGADA EN AZURE (revisión `c55feed2-7-1`) Y VERIFICADA 13/13 (2026-11-06)** |

## 18. Aprobación y ejecución

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
