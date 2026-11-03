# NV-04 — User Registration (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-04 |
| Documento | `spec.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks | `task.md` 1.0.0-u2 |
| Precondición | NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza NV-05 ni NV-06 |

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B con SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVA y ESTADO AUTORIZADO.

## 1. Contexto

La Entrega Unidad 2 necesita un registro mínimo de usuarios para habilitar Login y Logout. Registration crea una cuenta con los campos estrictamente necesarios para autenticación: email y contraseña. No incluye perfil, verificación de email, recuperación de contraseña, roles ni inicio de sesión social.

## 2. Objetivo

Definir el contrato verificable de `POST /api/v1/auth/register`, su validación, persistencia, protección de contraseña, errores y pruebas, sin ampliar el alcance funcional de U2.

## 3. Precondiciones

1. NV-01 U2 aprobada documentalmente: Problem Details, validación, configuración y límites.
2. NV-03 U2 aprobada documentalmente para la verificación desplegada; el contrato y las pruebas locales pueden ejecutarse con dependencias de prueba aisladas.
3. Existe autorización de Fase B para las rutas y tasks U2.
4. COST-001 vigente.
5. No se implementa ningún endpoint adicional.

## 4. Alcance

1. `POST /api/v1/auth/register`.
2. Contrato de request y response.
3. Validación y normalización de email.
4. Validación de contraseña.
5. Persistencia en MongoDB con unicidad de email.
6. Hashing de contraseña.
7. Problem Details y códigos de estado.
8. Rate limiting del endpoint.
9. Pruebas unitarias, de integración y de seguridad.

## 5. Fuera de alcance

- Login, Logout, refresh tokens o emisión de JWT.
- Perfil de usuario, nombre, teléfono, dirección o foto.
- Verificación de email, recuperación o cambio de contraseña.
- Roles, permisos, administración o inicio de sesión social.
- Devolver la contraseña o el hash en cualquier respuesta.
- Implementar MongoDB, Redis o infraestructura; pertenece a NV-03 y NV-01.
- Ejecutar NV-02 o sus scanners.
- Declarar pruebas o cobertura no ejecutadas.

## 6. Contrato HTTP

### 6.1 Request

```http
POST /api/v1/auth/register
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "<contraseña del usuario>"
}
```

Campos:

| Campo | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| `email` | string | Sí | Se recorta, se convierte a minúsculas y se valida |
| `password` | string | Sí | No se recorta; longitud 12–128 caracteres |

No se aceptan campos adicionales. Un campo desconocido produce 400 para evitar mass assignment. El cuerpo máximo es 8 KB.

### 6.2 Response 201 Created

```json
{
  "id": "665f1c2a9b1e4a0012345678",
  "email": "user@example.com",
  "createdAt": "2026-11-02T12:00:00Z"
}
```

No se devuelve `password`, `passwordHash`, roles, estado interno ni tokens. El `id` es el identificador público del usuario.

### 6.3 Validación

| Entrada | Regla | Error |
| --- | --- | --- |
| `email` | Obligatorio, string, máximo 254, formato RFC 5322 simplificado, una sola `@`, sin espacios | 400 |
| `email` | Se normaliza con `trim` y `lowercase` antes de validar y persistir | 400 si queda inválido |
| `password` | Obligatorio, string, mínimo 12, máximo 128, no vacío ni solo espacios | 400 |
| `password` | No se recorta ni se transforma | 400 si excede límites |
| Campos desconocidos | Rechazados | 400 |
| `Content-Type` | `application/json` | 415 |
| Cuerpo | Máximo 8 KB | 400/413 según middleware |

### 6.4 Status codes

| Código | Condición |
| --- | --- |
| 201 | Cuenta creada |
| 400 | Validación inválida, campo desconocido o JSON malformado |
| 409 | Email ya registrado |
| 415 | `Content-Type` no soportado |
| 429 | Rate limit excedido; incluye `Retry-After` |
| 503 | Dependencia requerida no disponible; sin filtrar detalles |
| 500 | Error interno; Problem Details sin stack trace |

## 7. Persistencia y seguridad

### 7.1 MongoDB

- Base: la configurada para U2; colección `users`.
- Documento mínimo: `_id`, `email`, `passwordHash`, `createdAt`, `updatedAt`.
- `email` se guarda normalizado en minúsculas.
- Índice único `{ email: 1 }`.
- Un duplicado se detecta por índice único y se traduce a 409; no se hace “buscar y luego insertar” como única protección.
- No se guarda contraseña en texto plano, ni en logs, ni en respuestas.

### 7.2 Hashing

- PBKDF2-HMAC-SHA256 usando `Rfc2898DeriveBytes.Pbkdf2` de .NET; no se implementa criptografía propia.
- Factor de trabajo centralizado en una opción tipada (`PasswordHashing:Iterations`), con valor por defecto 210,000 y mínimo 210,000 validado al inicio. Hashing y verificación leen la misma configuración; no se admiten números mágicos dispersos.
- Salt: 128 bits (16 bytes) generados por contraseña con CSPRNG (`RandomNumberGenerator.Fill`). Cada salt es único; nunca se reutiliza un salt global.
- Subkey: 256 bits.
- Formato almacenado: `pbkdf2-sha256$<iterations>$<salt-base64>$<hash-base64>`. Se almacenan de forma segura, asociados al usuario, el algoritmo, el factor de trabajo, el salt y el hash necesarios para verificar; nunca la contraseña en texto plano.
- Comparación en tiempo constante con `CryptographicOperations.FixedTimeEquals`.
- La verificación usa el factor de trabajo almacenado. Si la configuración actual difiere, se rehashea con el factor vigente después de verificar.
- Nunca se registra ni se devuelve la contraseña, el salt o el hash.
- `JWT_SIGNING_KEY` no se usa en Registration.

### 7.3 Seguridad

- DTO explícito; no se enlaza el cuerpo directamente al documento de persistencia.
- No se aceptan `id`, `role`, `status`, `passwordHash` ni campos internos.
- No se registra la contraseña, el hash, el cuerpo completo ni headers sensibles.
- La respuesta no revela hash, detalles de MongoDB ni stack traces.
- Rate limit: 10 solicitudes por ventana de 60 segundos por IP, distribuido en Redis; 429 con `Retry-After: 60`.
- Si Redis no está disponible, el endpoint falla cerrado con 503; no se omite el límite.
- HTTPS obligatorio en el entorno desplegado.
- Email duplicado devuelve 409; se acepta como riesgo de enumeración controlado por rate limiting.

## 8. Errores

Todos los errores usan `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`. No incluyen stack traces, rutas internas, nombres de colecciones, cadenas de conexión ni secretos.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | `Validation failed` |
| Email duplicado | 409 | `Email already registered` |
| Rate limit | 429 | `Too many requests` |
| Dependencia no disponible | 503 | `Service unavailable` |
| Error interno | 500 | `Internal server error` |

## 9. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-04-R01 | Contrato de registro | Request y response coinciden con el contrato; 201 devuelve `id`, `email` y `createdAt`; no devuelve contraseña ni hash. | F1 | T01, T06, T10 |
| NV-04-R02 | Validación y normalización | Email y contraseña cumplen reglas; email se normaliza; campos desconocidos y cuerpos excesivos se rechazan con 400/413. | F1 | T02, T09, T11 |
| NV-04-R03 | Persistencia y unicidad | El usuario se guarda en `users`; el índice único de email impide duplicados; un duplicado produce 409. | F2 | T03, T05, T10 |
| NV-04-R04 | Protección de contraseña | PBKDF2-HMAC-SHA256 con APIs criptográficas de .NET; factor de trabajo centralizado y configurable (default 210,000, mínimo 210,000 validado); salt 128 bits único por contraseña generado con CSPRNG; hash y parámetros almacenados; comparación constante; nunca se devuelve o registra la contraseña. | F2 | T04, T05, T09, T11 |
| NV-04-R05 | Problem Details | 400, 409, 415, 429, 503 y 500 usan Problem Details sin detalles internos. | F3 | T06, T07, T10 |
| NV-04-R06 | Prevención de mass assignment | El DTO rechaza campos internos y no permite asignar rol, estado, id o hash. | F2 | T05, T09, T11 |
| NV-04-R07 | Rate limiting | 10 solicitudes/60 s por IP con Redis compartido; 429 con `Retry-After`; fallo de Redis devuelve 503. | F3 | T08, T10 |
| NV-04-R08 | Pruebas y evidencia | Existen pruebas unitarias, de integración y de seguridad; la evidencia registra comandos, resultados y hallazgos. | F4 | T09, T10, T11 |

## 10. Testing

- Unitarias: normalización de email, reglas de contraseña, serialización, hashing/verificación, salt único/CSPRNG, factor de trabajo configurado y mapeo de duplicado.
- Integración: 201, 400, 409, 415, 429, cuerpo máximo, email normalizado y ausencia de contraseña en respuesta.
- Seguridad: mass assignment (`role`, `status`, `passwordHash`, `id`), logs sin contraseña, rate limit distribuido y respuesta sin stack trace.
- Mongo/Redis: se prefiere dependencia efímera con Testcontainers **solo si Fase B autoriza el paquete**; alternativa: servicios de prueba aislados. No se instala nada en Fase A.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-04-RK01 | Enumeración de emails por 409 | Privacidad | Rate limit y aceptación documentada |
| NV-04-RK02 | Hashing débil o parámetros obsoletos | Compromiso de credenciales | Factor centralizado, salt CSPRNG y revisión |
| NV-04-RK03 | Mass assignment | Escalada de privilegios | DTO explícito y pruebas negativas |
| NV-04-RK04 | Contraseña en logs | Fuga | Redacción y pruebas |
| NV-04-RK05 | Duplicado por carrera | Datos inconsistentes | Índice único |
| NV-04-RK06 | Rate limit local | Abuso entre réplicas | Redis compartido |
| NV-04-RK07 | Dependencia de Mongo/Redis caída | Error no controlado | 503 y Problem Details |

## 12. Dependencias

- NV-01 U2: Problem Details, validación, configuración, límites.
- NV-03 U2: MongoDB, Redis, HTTPS y despliegue.
- NV-05 Login consume el usuario creado.
- NV-06 Logout no depende de Registration para su contrato, pero comparte identidad.
- NV-02 permanece **⛔ NO EJECUTAR**.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 11 |
| Tasks U2 | 11 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |

## 14. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta la autorización de Fase B.
