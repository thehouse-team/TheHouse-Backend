# NV-08 — Plan de ejecución University Catalog U2

| Campo | Valor |
| --- | --- |
| ID | NV-08 |
| Documento | `plan.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |
| Spec | `spec.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

**Cambio 1.2.0-u2 (2026-11-07) — PENDIENTE DE APROBACIÓN Y «EJECUTA»:** el seed pasa a un catálogo regional de diez universidades de Tula–Tepeji–Jilotepec (NV-08-R04, task T14) y se corrige el comando de seed documentado, que eludía M-BE-043. Las tasks 1.1.0-u2 siguen cerradas.

Plan aprobado documentalmente para U2. No se implementa código en esta fase.

## 1. Enfoque

University Catalog se implementa en cuatro fases: contrato y validación; seed y acceso a datos; endpoints y errores; pruebas y evidencia. El seed es pequeño, idempotente y reproducible.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y validación | R01, R02, R03 | T01, T02 | DTOs y validación |
| F2 | Seed y acceso a datos | R04, R05 | T03, T04 | Seed idempotente e índices |
| F3 | Endpoints y errores | R01, R02, R05, R06, R08 | T05, T06, T07, T12 | 200/304/400/404/429/503 |
| F4 | Pruebas y evidencia | R01–R08 | T08, T09, T10, T11, T13 | Suite y evidencia |

## 3. Detalle de fases

### F1 — Contrato y validación

**Requisitos:** NV-08-R01, NV-08-R02, NV-08-R03.

**Tasks:** T01, T02.

**Entregables U2:**

- DTOs de listado y detalle.
- Validación de `page`, `pageSize` e `id`.
- Orden estable `name`/`_id`.
- Resultado vacío 200.

**Criterios de salida:**

1. El contrato coincide con `spec.md`.
2. `pageSize` máximo 50.
3. ID inválido produce 400.
4. No se aceptan filtros no aprobados.

**Evidencia:** pruebas unitarias y revisión del contrato.

### F2 — Seed y acceso a datos

**Requisitos:** NV-08-R04, NV-08-R05.

**Tasks:** T03, T04.

**Entregables U2:**

- Colección `universities`.
- Índice único `{code:1}` e índice `{name:1,_id:1}`.
- Seed de tres registros mexicanos (UTTT/UAEH/ITSOEH) con upsert por `code`.
- Evidencia de idempotencia y origen.

**Criterios de salida:**

1. Ejecutar el seed dos veces no duplica.
2. No se eliminan registros ajenos.
3. No hay datos personales.
4. El listado usa el índice.

**Evidencia:** revisión de índices, comando de seed, conteos antes/después y `evidence.md`.

### F3 — Endpoints y errores

**Requisitos:** NV-08-R01, NV-08-R02, NV-08-R05, NV-08-R06.

**Tasks:** T05, T06, T07.

**Entregables U2:**

- `GET /api/v1/universities`.
- `GET /api/v1/universities/{id}`.
- 200, 400, 404, 429, 503, 500.
- Problem Details con `traceId`.
- Rate limit 60/60 s por IP en Redis.
- Caché HTTP condicional: `ETag` débil, `Cache-Control` y 304 (T12, 1.1.0-u2).

**Criterios de salida:**

1. Listado vacío devuelve 200.
2. Detalle inexistente devuelve 404.
3. Los errores no filtran MongoDB ni stack traces.
4. Redis caído produce 503.

**Evidencia:** pruebas de integración y respuestas sanitizadas.

### F4 — Pruebas y evidencia

**Requisitos:** NV-08-R01 a NV-08-R07.

**Tasks:** T08, T09, T10, T11.

**Entregables U2:**

- Unitarias, integración y seguridad.
- Evidencia de seed y endpoints.
- Revisión de seguridad y hallazgos.

**Criterios de salida:**

1. Todas las suites relevantes pasan o tienen brecha declarada.
2. El seed es idempotente.
3. No hay Critical/High abierto.
4. La revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** reportes, comandos y acta.

## 4. Secuencia y dependencias

```text
F1 Contrato/validación
  ↓
F2 Seed/datos
  ↓
F3 Endpoints/errores
  ↓
F4 Pruebas/evidencia
```

- T02 depende de T01.
- T03 depende de T01.
- T04 depende de T03.
- T05 depende de T02 y T03.
- T06 depende de T02 y T03.
- T07 depende de T05 y T06.
- T08 depende de T02 y T04.
- T09 depende de T03, T04, T05 y T06.
- T10 depende de T04, T06 y T07.
- T11 depende de T08, T09 y T10.
- T12 depende de T05 y T06.
- T13 depende de T10 y T12.

## 5. Testing

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Paginación, ID, DTO, seed | xUnit | Reporte |
| Integración | Listado, detalle, seed dos veces, 400/404/429/503 | WebApplicationFactory + Testcontainers si se autoriza | Reporte |
| Seguridad | Datos personales, errores, rate limit | Pruebas negativas | Hallazgos |
| Integración y seguridad | Caché HTTP: `ETag`, 304, `no-store` en errores | WebApplicationFactory | Reporte (T13) |

Testcontainers no se instala en Fase A.

## 6. Seguridad

- Solo datos institucionales públicos.
- Proyección explícita; sin campos internos.
- Validar ObjectId antes de consultar.
- Sin datos personales en logs.
- Rate limit compartido y fallo cerrado.
- HTTPS en despliegue.

## 7. Costo

University Catalog no requiere servicios pagos. MongoDB/Redis deben caber en COST-001 según NV-03. Si el costo es incierto, BLOCKED.

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.0.0-u2; 1.1.0-u2 para T12 y T13 | BLOCKED |
| G0-Ejec | Inicio | Autorización con SPEC/PLAN/TASK/ESTADO | BLOCKED |
| G1 | Fin F1 | Contrato y validación | No avanzar |
| G2 | Fin F2 | Seed idempotente e índices | No avanzar |
| G3 | Fin F3 | Endpoints y errores | No avanzar |
| G4 | Fin F4 | Pruebas y evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T02, T05, T09 |
| R02 | F1 | T01, T02, T06, T10 |
| R03 | F1 | T02, T06, T08 |
| R04 | F2 | T04, T08, T09, T10, T14 |
| R05 | F2 | T03, T05, T06, T09 |
| R06 | F3 | T05, T06, T07, T09 |
| R07 | F4 | T10, T11, T13 |
| R08 | F3 | T12, T13 |

## 10. No ejecución

Este plan no autoriza código, paquetes, build, tests, Docker, Terraform ni despliegue. Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)**.
