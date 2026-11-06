# NV-06 — Logout (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-06 |
| Documento | `spec.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-03 (`Requerimientos_TheHouse_v2`) |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks | `task.md` 1.0.0-u2 |
| Precondición | NV-05 U2 y NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza NV-05 |

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B.

## 1. Contexto

En una API stateless, el cliente puede borrar su token, pero eso no demuestra revocación en servidor. Logout debe invalidar el JWT de acceso en un almacén compartido para que la reutilización sea rechazada por cualquier réplica. U2 no emite refresh tokens; la revocación de refresh queda como extensión futura.

## 2. Objetivo

Definir el contrato verificable de `POST /api/v1/auth/logout`, la revocación del JWT por `jti`, el comportamiento multi-réplica, los errores y las pruebas.

## 3. Precondiciones

1. NV-05 U2 emite JWT con `jti` y `exp`.
2. NV-03 U2 aprobada documentalmente para la verificación desplegada; las pruebas locales pueden usar Redis aislado.
3. NV-01 U2 provee Problem Details y configuración.
4. Existe autorización de Fase B.

## 4. Alcance

1. `POST /api/v1/auth/logout`.
2. Validación de JWT.
3. Revocación por `jti` en Redis con TTL hasta `exp`.
4. Rechazo de token revocado en cualquier réplica.
5. Problem Details.
6. Pruebas unitarias, de integración, multi-réplica y seguridad.

## 5. Fuera de alcance

- Login y emisión de JWT; pertenece a NV-05.
- Refresh tokens y endpoint de refresh.
- Logout de todos los dispositivos o revocación por usuario.
- Borrar tokens del cliente o modificar cookies.
- Roles, permisos o administración.
- Implementar MongoDB, Redis o infraestructura.
- Ejecutar NV-02.
- Declarar pruebas no ejecutadas.

## 6. Contrato HTTP

### 6.1 Request

```http
POST /api/v1/auth/logout
Authorization: Bearer <jwt>
```

Sin cuerpo. El token debe ser un JWT válido emitido por NV-05.

### 6.2 Response 204 No Content

Sin cuerpo. La respuesta no devuelve el token, su `jti` ni detalles de Redis.

### 6.3 Status codes

| Código | Condición |
| --- | --- |
| 204 | Token válido y revocado correctamente |
| 400 | Cuerpo inesperado o `Authorization` mal formado |
| 401 | Token ausente, inválido, expirado o ya revocado |
| 429 | Rate limit excedido; `Retry-After` |
| 503 | Redis no disponible; no se permite el logout silencioso |
| 500 | Error interno; Problem Details sin stack trace |

## 7. Revocación

### 7.1 Clave y TTL

- Clave: `auth:revoked:jti:{jti}`.
- Valor: `1`.
- TTL: `exp - now`, mínimo 1 segundo.
- Operación atómica `SET` con expiración; no se usan dos pasos no atómicos.
- El TTL no debe exceder la vida restante del token.

### 7.2 Validación en cada solicitud autenticada

- Validar firma, algoritmo, issuer, audience, `exp`, `nbf` y `jti`.
- Consultar Redis por `jti`.
- Si existe la clave, responder 401.
- Si Redis no está disponible, responder 503; no se permite el acceso por fallback local.
- No se cachea el estado de revocación en memoria del proceso.

### 7.3 Semántica e idempotencia

- Primer logout con token válido: 204.
- Segundo logout con el mismo token: 401, porque el token ya está revocado.
- Token expirado: 401.
- La revocación es por token, no por usuario; no cierra otras sesiones.
- U2 no emite refresh tokens. Si una revisión futura los incorpora, Logout deberá revocar también el refresh token y su familia; esa extensión requerirá nueva aprobación.

### 7.4 Multi-réplica

La revocación debe ser visible desde cualquier réplica que comparta el Redis de U2. No se acepta un almacén local, caché en memoria ni sticky sessions. La prueba debe emitir el token en una réplica, ejecutar logout en otra y reutilizar el token en la primera.

## 8. Seguridad

- No registrar el JWT completo, el `jti`, la firma ni el header `Authorization`.
- No devolver el token ni el `jti`.
- `Cache-Control: no-store`.
- Rate limit: 30 solicitudes/60 s por IP en Redis; 429 con `Retry-After: 60`.
- Si Redis falla, el logout devuelve 503; no se reporta éxito sin revocación.
- HTTPS obligatorio en el entorno desplegado.
- Errores sin detalles de Redis, stack traces ni rutas internas.
- No aceptar token por query string.

## 9. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance`, `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Token ausente/inválido/revocado | 401 | `No autorizado` |
| Rate limit | 429 | `Demasiadas solicitudes` |
| Redis no disponible | 503 | `Servicio no disponible` |
| Error interno | 500 | `Error interno del servidor` |

## 10. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-06-R01 | Contrato de logout | `POST /api/v1/auth/logout` exige Bearer, no acepta cuerpo y devuelve 204 sin contenido en el primer logout válido. | F1 | T01, T04, T08 |
| NV-06-R02 | Revocación por jti | El token válido se revoca en Redis con clave `auth:revoked:jti:{jti}` y TTL hasta `exp`; la reutilización devuelve 401. | F1 | T01, T02, T03, T07, T09 |
| NV-06-R03 | Consistencia multi-réplica y fallo cerrado | La revocación se comparte entre réplicas; no hay fallback local; Redis caído devuelve 503. | F2 | T02, T03, T08, T09 |
| NV-06-R04 | Semántica de logout | Primer logout 204; segundo 401; token expirado 401; no cierra otras sesiones; refresh futuro documentado. | F2 | T04, T07, T09 |
| NV-06-R05 | Problem Details | 400, 401, 429, 503 y 500 usan Problem Details sin detalles internos. | F3 | T05, T08 |
| NV-06-R06 | Seguridad y logging | Sin JWT/jti en logs o respuestas; `no-store`; rate limit; token no aceptado por query. | F2 | T06, T09 |
| NV-06-R07 | Pruebas y evidencia | Unitarias, integración, multi-réplica y seguridad; evidencia de comandos y resultados. | F4 | T07, T08, T09, T10 |

## 11. Testing

- Unitarias: construcción de clave/TTL, token sin `jti`, token expirado, semántica 204/401.
- Integración: 204, reutilización 401, token ausente 401, token inválido 401, Redis caído 503.
- Multi-réplica: token emitido en A, logout en B, reutilización en A rechazada.
- Seguridad: logs sin JWT/jti, `no-store`, rate limit y errores sin stack trace.
- Redis: dependencia efímera con Testcontainers solo si Fase B autoriza el paquete; alternativa aislada.

## 12. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-06-RK01 | Revocación solo local | Token reutilizable en otra réplica | Redis compartido |
| NV-06-RK02 | TTL mayor que `exp` | Clave residual | TTL calculado |
| NV-06-RK03 | Logout sin revocación reportado 204 | Falsa seguridad | 503 si Redis falla |
| NV-06-RK04 | JWT en logs | Robo de sesión | Redacción |
| NV-06-RK05 | `jti` ausente | No se puede revocar | Validación de claims |
| NV-06-RK06 | Cerrar todas las sesiones por error | Impacto funcional | Revocación por token |

## 13. Dependencias

- NV-05 U2 para JWT y `jti`.
- NV-03 U2 para Redis y HTTPS.
- NV-01 U2 para Problem Details y configuración.
- NV-02 (CI) quedó completada el 2026-11-05 y la CD se especifica en NV-09 (M-BE-049).

## 14. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 10 |
| Tasks U2 | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 10 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** |

## 15. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta autorización de Fase B.
