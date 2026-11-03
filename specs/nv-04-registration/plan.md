# NV-04 — Plan de ejecución Registration U2

| Campo | Valor |
| --- | --- |
| ID | NV-04 |
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

Registration se implementa en cuatro fases: contrato y validación; persistencia y protección de contraseña; endpoint, errores y rate limiting; pruebas y evidencia. Cada fase produce un artefacto revisable antes de la siguiente.

Principios:

1. Solo email y contraseña; no se amplía el perfil.
2. El cuerpo se valida en el límite HTTP.
3. El índice único es la garantía de unicidad.
4. La contraseña nunca se devuelve ni se registra.
5. Rate limit distribuido en Redis.
6. La aprobación documental no autoriza ejecución.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y validación | R01, R02 | T01, T02 | DTO, reglas y errores 400 |
| F2 | Persistencia y seguridad | R03, R04, R06 | T03, T04, T05 | Usuario persistido y hash seguro |
| F3 | Endpoint, errores y límites | R01, R05, R07 | T06, T07, T08 | 201/400/409/415/429/503 |
| F4 | Pruebas y evidencia | R01–R08 | T09, T10, T11 | Suite y evidencia |

## 3. Detalle de fases

### F1 — Contrato y validación

**Requisitos:** NV-04-R01, NV-04-R02.

**Tasks:** T01, T02.

**Entregables U2:**

- DTO `RegisterRequest` con `email` y `password`.
- Respuesta `RegisterResponse` con `id`, `email`, `createdAt`.
- Normalización de email y reglas de contraseña.
- Rechazo de campos desconocidos y cuerpos > 8 KB.

**Criterios de salida:**

1. El contrato coincide con `spec.md`.
2. Los errores de validación son 400 Problem Details.
3. No se aceptan campos internos.

**Evidencia:** pruebas unitarias de normalización/validación y revisión del contrato.

### F2 — Persistencia y seguridad

**Requisitos:** NV-04-R03, NV-04-R04, NV-04-R06.

**Tasks:** T03, T04, T05.

**Entregables U2:**

- Colección `users` y índice único `{ email: 1 }`.
- Servicio de hashing PBKDF2-HMAC-SHA256 con factor de trabajo centralizado/configurable, salt 128 bits CSPRNG único por contraseña, hash 256 bits y formato versionado.
- Caso de uso que normaliza, valida, hashea e inserta.
- Mapeo de duplicado a 409.
- DTO explícito sin mass assignment.

**Criterios de salida:**

1. No se guarda contraseña en texto plano.
2. Un duplicado no crea dos documentos.
3. El hash no sale del dominio de persistencia.
4. No se aceptan `role`, `status`, `id` o `passwordHash` del cliente.
5. El factor de trabajo está centralizado/configurado y cada contraseña tiene un salt único generado por CSPRNG.

**Evidencia:** pruebas de repositorio, hashing y mass assignment.

### F3 — Endpoint, errores y límites

**Requisitos:** NV-04-R01, NV-04-R05, NV-04-R07.

**Tasks:** T06, T07, T08.

**Entregables U2:**

- Endpoint `POST /api/v1/auth/register`.
- Códigos 201, 400, 409, 415, 429, 503 y 500.
- Problem Details con `traceId`.
- Rate limit 10/60 s por IP con Redis y `Retry-After`.

**Criterios de salida:**

1. 201 no devuelve contraseña ni hash.
2. 409 no filtra detalles de Mongo.
3. 429 respeta el umbral y `Retry-After`.
4. Redis caído produce 503, no omite el límite.

**Evidencia:** pruebas de integración y capturas sanitizadas.

### F4 — Pruebas y evidencia

**Requisitos:** NV-04-R01 a NV-04-R08.

**Tasks:** T09, T10, T11.

**Entregables U2:**

- Unitarias, integración y seguridad.
- Evidencia por requisito.
- Revisión de seguridad y hallazgos.

**Criterios de salida:**

1. Todas las suites relevantes pasan o tienen brecha declarada.
2. No se declaran pruebas no ejecutadas.
3. No hay Critical/High abierto dentro del alcance.
4. La revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** reportes, comandos, resultados y acta.

## 4. Secuencia y dependencias

```text
F1 Contrato/validación
  ↓
F2 Persistencia/seguridad
  ↓
F3 Endpoint/errores/límites
  ↓
F4 Pruebas/evidencia
```

- T02 depende de T01.
- T03 depende de T01.
- T04 depende de T01.
- T05 depende de T02, T03 y T04.
- T06 depende de T05.
- T07 depende de T06.
- T08 depende de T01.
- T09 depende de T02 y T04.
- T10 depende de T05, T06, T07 y T08.
- T11 depende de T09 y T10.

## 5. Testing

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Normalización, validación, hash, DTO | xUnit | Reporte |
| Integración | 201/400/409/415/429, Mongo/Redis | WebApplicationFactory + Testcontainers si se autoriza | Reporte |
| Seguridad | Mass assignment, logs, rate limit, stack trace | Pruebas negativas | Hallazgos |

Testcontainers no se instala en Fase A. Si Fase B no autoriza el paquete, se usan servicios de prueba aislados.

## 6. Seguridad

- No registrar contraseña, hash ni cuerpo completo.
- No devolver hash ni detalles internos.
- Índice único para unicidad.
- Rate limit compartido; fallo cerrado.
- HTTPS en despliegue.
- Revisar mass assignment y errores.

## 7. Costo

Registration no requiere servicios pagos. MongoDB/Redis deben caber en COST-001 según NV-03. Si el costo es incierto, BLOCKED.

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.0.0-u2 | BLOCKED |
| G0-Ejec | Inicio | Autorización con SPEC/PLAN/TASK/ESTADO | BLOCKED |
| G1 | Fin F1 | Contrato y validación | No avanzar |
| G2 | Fin F2 | Persistencia y hash | No avanzar |
| G3 | Fin F3 | Endpoint y límites | No avanzar |
| G4 | Fin F4 | Pruebas y evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T06, T10 |
| R02 | F1 | T02, T09, T11 |
| R03 | F2 | T03, T05, T10 |
| R04 | F2 | T04, T05, T09, T11 |
| R05 | F3 | T06, T07, T10 |
| R06 | F2 | T05, T09, T11 |
| R07 | F3 | T08, T10 |
| R08 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, build, tests, Docker, Terraform ni despliegue. Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B**.
