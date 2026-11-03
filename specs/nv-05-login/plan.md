# NV-05 — Plan de ejecución Login U2

| Campo | Valor |
| --- | --- |
| ID | NV-05 |
| Documento | `plan.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Spec | `spec.md` 1.0.0-u2 |
| Tasks | `task.md` 1.0.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

Plan aprobado documentalmente para U2. No se implementa código en esta fase.

## 1. Enfoque

Login se implementa en cuatro fases: contrato y validación genérica; JWT y seguridad; rate limiting y errores; pruebas y evidencia. El orden evita emitir tokens antes de definir validación, claims y límites.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y validación | R01, R02 | T01, T02 | DTO y 401 genérico |
| F2 | JWT y seguridad | R03, R06 | T03, T04, T07 | Token válido y logs seguros |
| F3 | Rate limiting y errores | R01, R04, R05 | T05, T06 | 200/400/401/429/503 |
| F4 | Pruebas y evidencia | R01–R07 | T08, T09, T10, T11 | Suite y evidencia |

## 3. Detalle de fases

### F1 — Contrato y validación

**Requisitos:** NV-05-R01, NV-05-R02.

**Tasks:** T01, T02.

**Entregables U2:**

- DTO `LoginRequest` con `email` y `password`.
- Respuesta `LoginResponse` con `accessToken`, `tokenType`, `expiresIn`.
- Normalización de email y validación de contraseña.
- Verificación de credenciales con mensaje genérico y hash dummy.

**Criterios de salida:**

1. El contrato coincide con `spec.md`.
2. Usuario inexistente y contraseña incorrecta devuelven el mismo 401.
3. No se aceptan campos desconocidos.

**Evidencia:** pruebas unitarias y revisión del contrato.

### F2 — JWT y seguridad

**Requisitos:** NV-05-R03, NV-05-R06.

**Tasks:** T03, T04, T07.

**Entregables U2:**

- Servicio de emisión/validación JWT HS256.
- Claims `iss`, `aud`, `sub`, `email`, `jti`, `iat`, `nbf`, `exp`.
- Validación de algoritmo, firma, emisor, audiencia y expiración.
- `JWT_SIGNING_KEY` desde entorno.
- Logs y respuestas sin secreto; `no-store`.

**Criterios de salida:**

1. Token válido con 900 s de vigencia.
2. Se rechaza `alg=none`, algoritmo distinto, firma inválida y token expirado.
3. La clave no se registra ni se versiona.
4. La respuesta no incluye refresh token ni hash.

**Evidencia:** pruebas de claims, firma y logs.

### F3 — Rate limiting y errores

**Requisitos:** NV-05-R01, NV-05-R04, NV-05-R05.

**Tasks:** T05, T06.

**Entregables U2:**

- Rate limit 5/60 s por IP+email y 20/60 s por IP en Redis.
- 429 con `Retry-After: 60`.
- 503 si Redis no está disponible.
- Problem Details para 400/401/415/429/503/500.

**Criterios de salida:**

1. El umbral real se respeta.
2. El límite se comparte entre réplicas.
3. Redis caído no omite el límite.
4. Los errores no filtran detalles internos.

**Evidencia:** pruebas de límite, fallo y contrato de errores.

### F4 — Pruebas y evidencia

**Requisitos:** NV-05-R01 a NV-05-R07.

**Tasks:** T08, T09, T10, T11.

**Entregables U2:**

- Unitarias, integración y seguridad.
- Evidencia de rate limit, claims y logs.
- Revisión de seguridad y hallazgos.

**Criterios de salida:**

1. Todas las suites relevantes pasan o tienen brecha declarada.
2. No se declaran pruebas no ejecutadas.
3. No hay Critical/High abierto en el alcance.
4. La revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** reportes, comandos, resultados y acta.

## 4. Secuencia y dependencias

```text
F1 Contrato/validación
  ↓
F2 JWT/seguridad
  ↓
F3 Rate limit/errores
  ↓
F4 Pruebas/evidencia
```

- T02 depende de T01.
- T03 depende de T01.
- T04 depende de T02 y T03.
- T05 depende de T01.
- T06 depende de T04 y T05.
- T07 depende de T04.
- T08 depende de T02 y T03.
- T09 depende de T05 y T06.
- T10 depende de T04, T05 y T07.
- T11 depende de T08, T09 y T10.

## 5. Testing

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Validación, hash, claims, expiración | xUnit | Reporte |
| Integración | 200/400/401/415/429/503, `no-store` | WebApplicationFactory + Testcontainers si se autoriza | Reporte |
| Seguridad | Enumeración, timing, logs, rate limit | Pruebas negativas | Hallazgos |

Testcontainers no se instala en Fase A. Si Fase B no autoriza el paquete, se usan servicios aislados.

## 6. Seguridad

- Mensaje genérico 401.
- Hash dummy para usuario inexistente.
- Sin contraseña/token en logs.
- `JWT_SIGNING_KEY` fuera de Git.
- Allowlist HS256.
- Rate limit compartido y fallo cerrado.
- HTTPS y `no-store`.

## 7. Costo

Login no requiere servicios pagos. MongoDB/Redis deben caber en COST-001 según NV-03. Si el costo es incierto, BLOCKED.

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.0.0-u2 | BLOCKED |
| G0-Ejec | Inicio | Autorización con SPEC/PLAN/TASK/ESTADO | BLOCKED |
| G1 | Fin F1 | Contrato y 401 genérico | No avanzar |
| G2 | Fin F2 | JWT y logs seguros | No avanzar |
| G3 | Fin F3 | Rate limit y errores | No avanzar |
| G4 | Fin F4 | Pruebas y evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T04, T06 |
| R02 | F1 | T02, T04, T08, T10 |
| R03 | F2 | T03, T04, T08 |
| R04 | F3 | T05, T09, T10 |
| R05 | F3 | T06, T09 |
| R06 | F2 | T04, T07, T08, T10 |
| R07 | F4 | T08, T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, build, tests, Docker, Terraform ni despliegue. Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B**.
