# NV-05 — Login (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-05 |
| Documento | `spec.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks | `task.md` 1.0.0-u2 |
| Precondición | NV-04 U2 y NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza NV-06 |

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B.

## 1. Contexto

Login valida credenciales de un usuario registrado y emite un JWT de acceso. U2 no incluye un endpoint de refresh; por tanto, no se emiten refresh tokens en esta entrega. La estrategia futura de refresh se documenta para no contradecir la arquitectura, pero no se implementa.

## 2. Objetivo

Definir el contrato verificable de `POST /api/v1/auth/login`, la validación de credenciales, la emisión/validación JWT, el rate limiting, los errores y las pruebas.

## 3. Precondiciones

1. NV-04 U2 crea usuarios con email normalizado y hash PBKDF2.
2. NV-01 U2 provee Problem Details, configuración, correlación y límites.
3. NV-03 U2 aprobada documentalmente para la verificación desplegada; el contrato y las pruebas locales pueden usar dependencias aisladas.
4. Existe autorización de Fase B.
5. `JWT_SIGNING_KEY` se suministra por variable de entorno; nunca se versiona.

## 4. Alcance

1. `POST /api/v1/auth/login`.
2. Validación de email/contraseña.
3. Verificación de hash PBKDF2.
4. Emisión de JWT de acceso.
5. Rate limiting distribuido en Redis.
6. Problem Details.
7. Pruebas unitarias, de integración y de seguridad.

## 5. Fuera de alcance

- Refresh tokens y endpoint de refresh.
- Logout y revocación; pertenece a NV-06.
- Registro; pertenece a NV-04.
- Roles, permisos, MFA, recuperación de contraseña o login social.
- Cookies de sesión o estado local de autenticación.
- Implementar MongoDB, Redis, JWT o infraestructura.
- Ejecutar NV-02.
- Declarar pruebas o cobertura no ejecutadas.

## 6. Contrato HTTP

### 6.1 Request

```http
POST /api/v1/auth/login
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "<contraseña del usuario>"
}
```

| Campo | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| `email` | string | Sí | Trim, lowercase, máximo 254, formato válido |
| `password` | string | Sí | 1–128 caracteres; no se recorta |

No se aceptan campos adicionales. Cuerpo máximo 8 KB.

### 6.2 Response 200 OK

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

No se devuelve refresh token, contraseña, hash, roles ni datos internos. `Cache-Control: no-store`.

### 6.3 Status codes

| Código | Condición |
| --- | --- |
| 200 | Credenciales válidas; JWT emitido |
| 400 | Validación inválida, campo desconocido o JSON malformado |
| 401 | Credenciales inválidas; mensaje genérico |
| 415 | `Content-Type` no soportado |
| 429 | Rate limit excedido; `Retry-After` |
| 503 | Redis/Mongo no disponible; sin detalles |
| 500 | Error interno; Problem Details sin stack trace |

## 7. JWT y refresh

### 7.1 JWT de acceso U2

| Parámetro | Valor |
| --- | --- |
| Algoritmo | HS256; solo se acepta ese algoritmo |
| Clave | `JWT_SIGNING_KEY` desde entorno, mínimo 32 bytes UTF-8 |
| Emisor | `thehouse-api` |
| Audiencia | `thehouse-clients` |
| Duración | 900 segundos |
| Clock skew | 30 segundos |
| Claims | `iss`, `aud`, `sub` (userId), `email`, `jti`, `iat`, `nbf`, `exp` |
| `jti` | 128 bits aleatorios, único por token |

Validación: firma, algoritmo permitido, emisor, audiencia, expiración, `nbf` y `jti` presente. Se rechaza `alg=none`, algoritmo distinto, firma inválida, token expirado o claims obligatorios ausentes. La clave no se registra ni se devuelve.

### 7.2 Refresh tokens (futuro, no U2)

U2 no emite refresh tokens porque no existe endpoint de refresh autorizado. La estrategia futura queda definida para una revisión posterior de NV-05:

- Token opaco de 256 bits, almacenado como SHA-256 en MongoDB.
- Expiración de 7 días.
- Rotación en cada refresh.
- Detección de reutilización y revocación de la familia.
- Transporte y protección CSRF/XSS definidos por contrato.

Esta sección no autoriza implementar refresh ni crear endpoints.

## 8. Credenciales, rate limiting y seguridad

### 8.1 Verificación

- Buscar usuario por email normalizado.
- Si no existe, ejecutar una verificación PBKDF2 con un hash dummy para reducir diferencias de tiempo.
- Comparar el hash en tiempo constante.
- Cualquier fallo devuelve 401 con `No autorizado`, sin distinguir email inexistente, contraseña incorrecta o usuario inactivo.
- No se registra email completo, contraseña, hash ni token.

### 8.2 Rate limiting

| Scope | Ventana | Límite | Respuesta |
| --- | --- | --- | --- |
| IP + hash(email normalizado) | 60 s | 5 solicitudes | 429 + `Retry-After: 60` |
| IP | 60 s | 20 solicitudes | 429 + `Retry-After: 60` |

- Contadores en Redis compartido; no memoria local.
- Todas las solicitudes cuentan, válidas o inválidas.
- Si Redis no está disponible, el login falla cerrado con 503; no se omite el límite.
- El umbral es configurable, pero el valor por defecto y el usado en evaluación es el de esta tabla.

### 8.3 Seguridad

- HTTPS obligatorio en el entorno desplegado.
- `Cache-Control: no-store` y `Pragma: no-cache`.
- El token no se acepta por query string.
- No se registra el JWT completo ni la contraseña.
- No se devuelven stack traces, detalles de Mongo/Redis ni rutas internas.
- El endpoint es público; la autorización por recurso aplica a operaciones protegidas posteriores.

## 9. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance`, `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | `Validación fallida` |
| Credenciales | 401 | `No autorizado` |
| Rate limit | 429 | `Demasiadas solicitudes` |
| Dependencia | 503 | `Servicio no disponible` |
| Interno | 500 | `Error interno del servidor` |

## 10. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-05-R01 | Contrato de login | Request y response coinciden; 200 devuelve `accessToken`, `tokenType` y `expiresIn`; no devuelve refresh token, contraseña ni hash. | F1 | T01, T04, T06 |
| NV-05-R02 | Validación genérica de credenciales | Email/contraseña se validan; usuario inexistente y contraseña incorrecta devuelven el mismo 401; se usa hash dummy para reducir timing. | F1 | T02, T04, T08, T10 |
| NV-05-R03 | Emisión y validación JWT | HS256, clave de entorno >=32 bytes, issuer/audience/expiración/claims definidos; se rechazan algoritmos no permitidos y tokens inválidos. | F2 | T03, T04, T08 |
| NV-05-R04 | Rate limiting distribuido | 5/60 s por IP+email y 20/60 s por IP en Redis; 429 con `Retry-After`; fallo de Redis 503. | F3 | T05, T09, T10 |
| NV-05-R05 | Problem Details | 400, 401, 415, 429, 503 y 500 usan Problem Details sin detalles internos. | F3 | T06, T09 |
| NV-05-R06 | Seguridad y logging | Sin contraseña/token en logs; `no-store`; token no aceptado por query; errores genéricos. | F2 | T04, T07, T08, T10 |
| NV-05-R07 | Pruebas y evidencia | Unitarias, integración y seguridad; evidencia de comandos, resultados y hallazgos. | F4 | T08, T09, T10, T11 |

## 11. Testing

- Unitarias: validación, verificación de hash, claims JWT, expiración, algoritmo inválido y mensaje genérico.
- Integración: 200, 400, 401, 415, 429, `no-store`, expiración y firma inválida.
- Seguridad: enumeración de usuarios, timing, logs sin secretos, rate limit compartido y token no aceptado por query.
- Redis/Mongo: dependencia efímera con Testcontainers solo si Fase B autoriza el paquete; alternativa aislada.

## 12. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-05-RK01 | Enumeración por diferencias de tiempo | Privacidad | Hash dummy y mensaje genérico |
| NV-05-RK02 | Clave JWT débil o versionada | Falsificación | >=32 bytes, entorno y revisión |
| NV-05-RK03 | Token en logs | Robo de sesión | Redacción y pruebas |
| NV-05-RK04 | Rate limit local | Fuerza bruta distribuida | Redis compartido |
| NV-05-RK05 | Algoritmo no permitido | Bypass | Allowlist HS256 |
| NV-05-RK06 | Redis caído | Bypass del límite | Fallo cerrado 503 |
| NV-05-RK07 | Refresh mal diseñado | Revocación inviable | Fuera de U2; revisión futura |

## 13. Dependencias

- NV-04 U2 para usuarios y hashes.
- NV-01 U2 para Problem Details, configuración y correlación.
- NV-03 U2 para Redis, HTTPS y despliegue.
- NV-06 consume el JWT emitido para logout.
- NV-02 permanece **⛔ NO EJECUTAR**.

## 14. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 11 |
| Tasks U2 | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |

## 15. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta autorización de Fase B.
