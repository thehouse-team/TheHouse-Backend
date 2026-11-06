# NV-10 — Sesión y cuenta — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero se fija el contrato y la rotación de refresh tokens (lo más usado por la app), luego los flujos que dependen del proveedor de correo y al final los límites anti-abuso.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Refresh token con rotación; Login y registro extendidos; Consulta de la cuenta propia |
| F2 | Persistencia e integración | R04, R05, R06, R07 | T04, T05, T06, T07 | Eliminación de cuenta; Recuperación de contraseña; Verificación de correo e insignia; Persistencia y expiración |
| F3 | Seguridad, autorización y límites | R08 | T08 | Límites y anti-enumeración |
| F4 | Pruebas y evidencia | R09 | T09, T10, T11 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Refresh token con rotación** (R01): `POST /auth/refresh` emite access y refresh nuevos; el refresh anterior deja de servir; reuso de uno rotado revoca la familia y responde 401; expiración a 30 días.
- **T02 · Login y registro extendidos** (R02): Login devuelve `refreshToken` sin romper el contrato NV-05; registro acepta `name` (1–80) y `acceptedTermsVersion` obligatorio; clientes antiguos siguen funcionando.
- **T03 · Consulta de la cuenta propia** (R03): `GET /users/me` devuelve solo campos públicos de la cuenta del `sub`; nunca hash de contraseña ni tokens.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Eliminación de cuenta** (R04): `DELETE /users/me` con contraseña correcta anonimiza, revoca sesiones y limpia datos dependientes; 409 con renta activa; la cuenta eliminada no puede iniciar sesión.
- **T05 · Recuperación de contraseña** (R05): `forgot` siempre 202 y envía un enlace solo si la cuenta existe; `reset` con token válido cambia la contraseña, revoca sesiones y consume el token; token vencido o usado 400.
- **T06 · Verificación de correo e insignia** (R06): Token de 24 h de un solo uso; al verificar se fija `emailVerifiedAt`; `studentBadge` es true solo con correo verificado y dominio del catálogo.
- **T07 · Persistencia y expiración** (R07): Colecciones con índices únicos y TTL; sin datos personales en tokens; consultas con proyección explícita.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T08 · Límites y anti-enumeración** (R08): Rate limits de la §9 con 429 y `Retry-After`; respuestas indistinguibles para cuentas existentes e inexistentes.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T09 · Pruebas unitarias y de integración** (R09): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T10 · Pruebas de seguridad** (R09): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T11 · Evidencia y revisión final** (R09): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03.
- T07 depende de: T01, T02, T03.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Refresh token con rotación | Pruebas automatizadas en CI |
| R02 | Login y registro extendidos | Pruebas automatizadas en CI |
| R03 | Consulta de la cuenta propia | Pruebas automatizadas en CI |
| R04 | Eliminación de cuenta | Pruebas automatizadas en CI |
| R05 | Recuperación de contraseña | Pruebas automatizadas en CI |
| R06 | Verificación de correo e insignia | Pruebas automatizadas en CI |
| R07 | Persistencia y expiración | Pruebas automatizadas en CI |
| R08 | Límites y anti-enumeración | Pruebas automatizadas en CI |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Refresh tokens y tokens de un solo uso solo se almacenan como hash; nunca aparecen en logs ni en Problem Details.
- Rate limit: refresh 30/min por familia; forgot 3/hora por correo y 10/hora por IP; verify y reset 10/hora por IP.
- `DELETE /users/me` exige la contraseña actual (reautenticación) para evitar borrados con un token robado.
- Respuestas idénticas y con tiempo similar para correos existentes e inexistentes (OWASP API2).
- Los enlaces de correo usan un esquema de la app (deep link) y no incluyen el correo del usuario.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Dentro de los límites del plan gratuito |
| Correo transaccional | $0 solo en plan gratuito permanente | PENDIENTE DE VERIFICAR; si no, RF-04 y RF-36 quedan BLOCKED |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §12 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F3 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
