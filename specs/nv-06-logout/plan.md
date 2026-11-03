# NV-06 — Plan de ejecución Logout U2

| Campo | Valor |
| --- | --- |
| ID | NV-06 |
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

Logout se implementa en cuatro fases: contrato y revocación; endpoint y semántica; errores y seguridad; pruebas y evidencia. La revocación compartida se resuelve antes de exponer el endpoint.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y revocación | R01, R02 | T01, T02, T03 | Clave Redis y validación jti |
| F2 | Endpoint y semántica | R03, R04, R06 | T04, T06 | 204, 401 y logs seguros |
| F3 | Errores | R05 | T05, T08 | Problem Details |
| F4 | Pruebas y evidencia | R01–R07 | T07, T08, T09, T10 | Suite y evidencia |

## 3. Detalle de fases

### F1 — Contrato y revocación

**Requisitos:** NV-06-R01, NV-06-R02.

**Tasks:** T01, T02, T03.

**Entregables U2:**

- Contrato `POST /api/v1/auth/logout` con Bearer.
- Clave `auth:revoked:jti:{jti}` y TTL hasta `exp`.
- Validación de JWT y `jti` antes de revocar.
- Rechazo 401 de token revocado.

**Criterios de salida:**

1. El TTL no excede la vida restante del token.
2. La revocación es atómica.
3. Un token sin `jti` se rechaza.
4. La revocación se consulta en cada solicitud autenticada.

**Evidencia:** pruebas unitarias de clave/TTL y pruebas de revocación.

### F2 — Endpoint y semántica

**Requisitos:** NV-06-R03, NV-06-R04, NV-06-R06.

**Tasks:** T04, T06.

**Entregables U2:**

- Endpoint que devuelve 204 en el primer logout.
- Segundo logout y token expirado devuelven 401.
- Sin cierre de otras sesiones.
- `no-store`, sin JWT/jti en logs ni respuestas.
- Rate limit 30/60 s por IP.

**Criterios de salida:**

1. 204 solo si la revocación se confirmó.
2. La reutilización es rechazada.
3. No se registra el token.
4. Redis caído produce 503, no 204.

**Evidencia:** pruebas de integración y logs.

### F3 — Errores

**Requisitos:** NV-06-R05.

**Tasks:** T05, T08.

**Entregables U2:**

- Problem Details para 400/401/429/503/500.
- 401 genérico para token ausente, inválido, expirado o revocado.
- Sin stack trace ni detalles de Redis.

**Criterios de salida:**

1. Todos los errores usan `application/problem+json`.
2. No se filtra información interna.
3. `traceId` presente.

**Evidencia:** pruebas de contrato y respuestas sanitizadas.

### F4 — Pruebas y evidencia

**Requisitos:** NV-06-R01 a NV-06-R07.

**Tasks:** T07, T08, T09, T10.

**Entregables U2:**

- Unitarias, integración, multi-réplica y seguridad.
- Evidencia de 204 → reutilización 401.
- Evidencia de consistencia entre réplicas.

**Criterios de salida:**

1. Todas las suites relevantes pasan o tienen brecha declarada.
2. La prueba multi-réplica demuestra revocación compartida.
3. No hay Critical/High abierto.
4. La revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** reportes, comandos y acta.

## 4. Secuencia y dependencias

```text
F1 Contrato/revocación
  ↓
F2 Endpoint/semántica
  ↓
F3 Errores
  ↓
F4 Pruebas/evidencia
```

- T02 depende de T01.
- T03 depende de T01 y T02.
- T04 depende de T03.
- T05 depende de T04.
- T06 depende de T04.
- T07 depende de T02 y T04.
- T08 depende de T04 y T05.
- T09 depende de T03, T04 y T06.
- T10 depende de T07, T08 y T09.

## 5. Testing

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Clave/TTL, claims, semántica | xUnit | Reporte |
| Integración | 204, 401, 503, Problem Details | WebApplicationFactory + Testcontainers si se autoriza | Reporte |
| Multi-réplica | Revocación entre instancias | Dos instancias + Redis | Logs y respuestas |
| Seguridad | Logs, `no-store`, rate limit | Pruebas negativas | Hallazgos |

Testcontainers no se instala en Fase A.

## 6. Seguridad

- Sin JWT/jti en logs o respuestas.
- `no-store`.
- Revocación atómica.
- Fallo cerrado si Redis no está disponible.
- Rate limit compartido.
- HTTPS en despliegue.

## 7. Costo

Logout no requiere servicios pagos. Redis debe caber en COST-001 según NV-03. Si el costo es incierto, BLOCKED.

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.0.0-u2 | BLOCKED |
| G0-Ejec | Inicio | Autorización con SPEC/PLAN/TASK/ESTADO | BLOCKED |
| G1 | Fin F1 | Revocación y validación jti | No avanzar |
| G2 | Fin F2 | 204, 401 y logs seguros | No avanzar |
| G3 | Fin F3 | Problem Details | No avanzar |
| G4 | Fin F4 | Pruebas y evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T04, T08 |
| R02 | F1 | T01, T02, T03, T07, T09 |
| R03 | F2 | T02, T03, T08, T09 |
| R04 | F2 | T04, T07, T09 |
| R05 | F3 | T05, T08 |
| R06 | F2 | T06, T09 |
| R07 | F4 | T07, T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, build, tests, Docker, Terraform ni despliegue. Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B**.
