# NV-10 — Sesión y cuenta

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-01 (nombre y términos), RF-04, RF-36, RF-71; refresh y `/users/me` (requisito académico U2) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-02, RNF-09, RNF-10 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | NV-04, NV-05 y NV-06 cerradas; NV-09 (CD) operativa |
| Relación | Extiende NV-04/NV-05/NV-06 con cambios aditivos; NV-11 usa `/users/me` |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El backend emite un access token JWT de 15 minutos (NV-05) y lo revoca en logout (NV-06), pero no tiene refresh token: la app móvil obliga a iniciar sesión cada 15 minutos. Tampoco existe un endpoint para consultar la cuenta propia, eliminarla, recuperar la contraseña o verificar el correo. RF-01 pide además guardar el nombre y la aceptación de términos y aviso de privacidad, que NV-04 no registra.

## 2. Objetivo

Definir el ciclo de vida completo de la sesión y de la cuenta: refresh token con rotación y detección de reuso, consulta y eliminación de la cuenta propia, recuperación de contraseña, verificación de correo e insignia de estudiante.

## 3. Precondiciones

1. NV-04, NV-05 y NV-06 cerradas y desplegadas.
2. Decisión de proveedor de correo resuelta (§12) para RF-04 y RF-36.
3. Autorización «EJECUTA».

## 4. Alcance

1. `POST /auth/refresh` con rotación de refresh token.
2. Cambio aditivo de NV-05: el login devuelve también `refreshToken`.
3. Cambio aditivo de NV-04: `name` y `acceptedTermsVersion` en el registro.
4. `GET /users/me` y `DELETE /users/me`.
5. Recuperación de contraseña por correo (RF-04).
6. Verificación de correo (RF-36).
7. Insignia de estudiante por dominio universitario verificado (RF-71).
8. Revocación de todas las sesiones al cambiar la contraseña, al eliminar la cuenta y al suspenderla (NV-21).

## 5. Fuera de alcance

- Inicio de sesión con Google (RF-02 parcial): requiere su propia spec de OAuth/OIDC.
- Verificación de teléfono por SMS (RF-70): el envío de SMS no tiene opción $0 (COST-001); BLOCKED.
- Edición de perfil y preferencias: NV-11.
- Modo arrendador: NV-12.
- Autenticación multifactor.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/refresh` | Público (refresh token) | Rota el refresh token y emite un access token nuevo | 200, 400, 401, 429 |
| GET | `/api/v1/users/me` | Autenticado | Cuenta propia: id, email, name, emailVerified, studentBadge, landlordEnabled, createdAt | 200, 401 |
| DELETE | `/api/v1/users/me` | Autenticado + contraseña | Elimina la cuenta con reautenticación | 204, 400, 401, 409, 429 |
| POST | `/api/v1/auth/password/forgot` | Público | Envía enlace de recuperación; siempre 202 | 202, 400, 429 |
| POST | `/api/v1/auth/password/reset` | Público (token) | Cambia la contraseña con token de un solo uso | 204, 400, 429 |
| POST | `/api/v1/users/me/email/verification` | Autenticado | Envía el correo de verificación | 202, 401, 409, 429 |
| POST | `/api/v1/auth/email/verify` | Público (token) | Marca el correo como verificado | 204, 400, 429 |

- `POST /auth/refresh` recibe `{ "refreshToken": "..." }` y responde `{ accessToken, tokenType, expiresIn, refreshToken }`.
- `DELETE /users/me` recibe `{ "password": "..." }`; 409 si la cuenta tiene una renta activa como estudiante o arrendador (NV-18).
- `POST /auth/password/reset` recibe `{ token, newPassword }` y aplica `PasswordPolicy` de NV-04.

## 7. Modelo de datos

- `refresh_tokens`: `tokenHash` (SHA-256), `userId`, `familyId`, `createdAt`, `expiresAt`, `rotatedAt`, `revokedAt`, `userAgentHash`. Índice único en `tokenHash`, índice por `familyId` e índice TTL en `expiresAt`.
- `one_time_tokens`: `tokenHash`, `userId`, `purpose` (`password_reset`, `email_verification`), `expiresAt`, `usedAt`. Índice TTL.
- `users` (NV-04) agrega `name`, `acceptedTermsVersion`, `acceptedTermsAt`, `emailVerifiedAt`, `deletedAt`, `status` (`active`, `suspended`, `deleted`).
- La insignia de estudiante se calcula: correo verificado y dominio presente en `universities.emailDomains` (extensión aditiva de NV-08).

## 8. Reglas de negocio

1. Refresh token opaco de 256 bits, válido 30 días, de un solo uso: cada uso emite uno nuevo de la misma familia y marca el anterior como rotado.
2. Si se presenta un refresh token ya rotado, se revoca toda la familia (detección de reuso) y se responde 401.
3. Logout (NV-06) revoca también la familia del refresh token de esa sesión.
4. El token de recuperación vale 30 minutos y el de verificación 24 horas; ambos de un solo uso y guardados solo como hash.
5. `password/forgot` responde 202 aunque el correo no exista (sin enumeración de cuentas).
6. Cambiar la contraseña revoca todas las familias de refresh token y los access tokens vigentes del usuario.
7. Eliminar la cuenta: borrado lógico, anonimización de correo y nombre, revocación de sesiones y borrado de favoritos, preferencias y publicaciones de compañero; las reseñas se conservan como "Usuario eliminado".

## 9. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Refresh tokens y tokens de un solo uso solo se almacenan como hash; nunca aparecen en logs ni en Problem Details.
- Rate limit: refresh 30/min por familia; forgot 3/hora por correo y 10/hora por IP; verify y reset 10/hora por IP.
- `DELETE /users/me` exige la contraseña actual (reautenticación) para evitar borrados con un token robado.
- Respuestas idénticas y con tiempo similar para correos existentes e inexistentes (OWASP API2).
- Los enlaces de correo usan un esquema de la app (deep link) y no incluyen el correo del usuario.

## 10. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Reuso de refresh token o token expirado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Cuenta con renta activa al eliminar | 409 | Conflicto |
| Correo ya verificado | 409 | Conflicto |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 11. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-10-R01 | Refresh token con rotación | `POST /auth/refresh` emite access y refresh nuevos; el refresh anterior deja de servir; reuso de uno rotado revoca la familia y responde 401; expiración a 30 días. | F1 | T01 |
| NV-10-R02 | Login y registro extendidos | Login devuelve `refreshToken` sin romper el contrato NV-05; registro acepta `name` (1–80) y `acceptedTermsVersion` obligatorio; clientes antiguos siguen funcionando. | F1 | T02 |
| NV-10-R03 | Consulta de la cuenta propia | `GET /users/me` devuelve solo campos públicos de la cuenta del `sub`; nunca hash de contraseña ni tokens. | F1 | T03 |
| NV-10-R04 | Eliminación de cuenta | `DELETE /users/me` con contraseña correcta anonimiza, revoca sesiones y limpia datos dependientes; 409 con renta activa; la cuenta eliminada no puede iniciar sesión. | F2 | T04 |
| NV-10-R05 | Recuperación de contraseña | `forgot` siempre 202 y envía un enlace solo si la cuenta existe; `reset` con token válido cambia la contraseña, revoca sesiones y consume el token; token vencido o usado 400. | F2 | T05 |
| NV-10-R06 | Verificación de correo e insignia | Token de 24 h de un solo uso; al verificar se fija `emailVerifiedAt`; `studentBadge` es true solo con correo verificado y dominio del catálogo. | F2 | T06 |
| NV-10-R07 | Persistencia y expiración | Colecciones con índices únicos y TTL; sin datos personales en tokens; consultas con proyección explícita. | F2 | T07 |
| NV-10-R08 | Límites y anti-enumeración | Rate limits de la §9 con 429 y `Retry-After`; respuestas indistinguibles para cuentas existentes e inexistentes. | F3 | T08 |
| NV-10-R09 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T09, T10, T11 |

## 12. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Proveedor de correo | Servicio con plan gratuito permanente y API (por ejemplo, Brevo o Resend free tier); credenciales fuera de Terraform (M-BE-038) | PENDIENTE |
| Duración del refresh token | 30 días con rotación | PROPUESTO |
| Google Sign-In (RF-02) | Spec propia posterior (OAuth/OIDC) | PROPUESTO |
| Verificación por SMS (RF-70) | Fuera de alcance por costo | BLOCKED (COST-001) |

## 13. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Dentro de los límites del plan gratuito |
| Correo transaccional | $0 solo en plan gratuito permanente | PENDIENTE DE VERIFICAR; si no, RF-04 y RF-36 quedan BLOCKED |

Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 14. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-10-RK01 | Robo de refresh token | Sesión prolongada de un atacante | Rotación, detección de reuso y revocación de familia |
| NV-10-RK02 | Enumeración de cuentas | Privacidad | Respuestas uniformes y rate limit |
| NV-10-RK03 | Correo no entregado | Usuario sin acceso | Reintento acotado y mensaje claro en la app |
| NV-10-RK04 | Borrado incompleto | Datos personales residuales | Lista explícita de colecciones afectadas y prueba de integración |

## 15. Dependencias

- NV-04, NV-05 y NV-06 (contratos que se extienden).
- NV-08 (`emailDomains` de universidades) para la insignia.
- NV-18 para la regla de renta activa al eliminar la cuenta.
- NV-21 usa la revocación de sesiones al suspender cuentas.

## 16. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 17. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §12. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
