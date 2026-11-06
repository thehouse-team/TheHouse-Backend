# NV-10 — Sesión: refresh token y cuenta propia — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `plan.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-06 («Apruebo y EJECUTA»: NV-10 1.1.0, tasks T01–T11, rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/` y `specs/nv-10-account-session/`) |
| Estado de ejecución | **🟡 T01–T10 COMPLETADAS EN LOCAL (480/480 pruebas); T11 PENDIENTE DEL DESPLIEGUE EN AZURE** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0 |
| Tasks | `task.md` 1.1.0 |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el contrato y el dominio de la sesión (emisión, rotación y `/users/me`) con pruebas unitarias; después la persistencia, la detección de reuso y la integración con el logout; al final el rechazo de sesiones revocadas, los límites y la regresión de los contratos existentes. Al terminar, el CD despliega y se prueba en Azure antes de avisar a Mobile.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Refresh token en el login; Renovación con rotación; Cuenta propia |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Persistencia e índices; Detección de reuso y concurrencia; Logout integrado |
| F3 | Seguridad, autorización y límites | R07, R08 | T07, T08 | Rechazo inmediato de sesiones revocadas; Límites y no regresión |
| F4 | Pruebas y evidencia | R09 | T09, T10, T11 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Refresh token en el login** (R01): Login 200 devuelve `refreshToken` además de los campos actuales sin cambiar su forma; el access token incluye `sid`; en la base solo queda el hash; prueba de contrato que compara la respuesta con la de NV-05.
- **T02 · Renovación con rotación** (R02): `POST /auth/refresh` con token válido devuelve access y refresh nuevos y el anterior deja de servir; token desconocido, vencido o revocado 401 uniforme; cuerpo inválido 400; vencimiento de 30 días por token y 90 por sesión.
- **T03 · Cuenta propia** (R03): `GET /users/me` con Bearer válido devuelve exactamente `id`, `email` y `createdAt` del `sub`; sin token o con token revocado 401; `Cache-Control: no-store`.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Persistencia e índices** (R04): `refresh_tokens` con índice único en `tokenHash`, índice por `sessionId` e índice TTL; búsqueda de usuario por id con proyección explícita.
- **T05 · Detección de reuso y concurrencia** (R05): Reusar un token rotado revoca toda la sesión y responde 401; dos refresh simultáneos con el mismo token producen una sola emisión; pruebas de ambos casos.
- **T06 · Logout integrado** (R06): Logout sigue respondiendo 204 y revoca el `jti` como hoy; con `sid` revoca también la sesión y su refresh token deja de servir; un access token sin `sid` sigue funcionando hasta su expiración.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Rechazo inmediato de sesiones revocadas** (R07): Tras el logout o un reuso, cualquier access token de esa sesión recibe 401 en la siguiente solicitud, en cualquier réplica (estado en Redis).
- **T08 · Límites y no regresión** (R08): Rate limit de refresh con 429 y `Retry-After`; fallo de Redis 503; pruebas de regresión de register 201, login 200 y logout 204 con los contratos actuales.

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
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Refresh token en el login | Pruebas automatizadas en CI |
| R02 | Renovación con rotación | Pruebas automatizadas en CI |
| R03 | Cuenta propia | Pruebas automatizadas en CI |
| R04 | Persistencia e índices | Pruebas automatizadas en CI |
| R05 | Detección de reuso y concurrencia | Pruebas automatizadas en CI |
| R06 | Logout integrado | Pruebas automatizadas en CI |
| R07 | Rechazo inmediato de sesiones revocadas | Pruebas automatizadas en CI |
| R08 | Límites y no regresión | Pruebas automatizadas en CI |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- El refresh token viaja solo en el cuerpo JSON por HTTPS, nunca en cookies ni en la URL (sin riesgo CSRF); la app lo guarda cifrado con Android Keystore y mantiene el access token solo en memoria.
- Refresh tokens, `sid` revocados y hashes nunca aparecen en logs, Problem Details ni evidencia.
- Rate limit de refresh: 30 solicitudes por 60 s por IP en Redis; si Redis no está disponible, el refresh y la validación de revocación fallan cerrados con 503.
- La detección de reuso protege contra el robo de un refresh token: el primer uso del token robado o del legítimo invalida la sesión entera.
- `/users/me` no permite consultar a otros usuarios (BOLA imposible por diseño) y no devuelve `PasswordHash` ni `UpdatedAt`.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Documentos pequeños con TTL |
| Redis Upstash | $0 | Claves con TTL de 15 minutos; vigilar el límite de comandos del plan gratuito |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0 | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | BLOCKED |
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
| R07 | F3 | T07 |
| R08 | F3 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: T01–T11 autorizadas con «EJECUTA» el 2026-11-06; avance en `task.md` y `evidence.md`.
