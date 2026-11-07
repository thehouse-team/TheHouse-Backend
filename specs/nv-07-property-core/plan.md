# NV-07 — Plan de ejecución Property Core U2

| Campo | Valor |
| --- | --- |
| ID | NV-07 |
| Documento | `plan.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |
| Spec | `spec.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

**Cambio 1.2.0-u2 (2026-11-07) — APROBADO Y AUTORIZADO por Victor Manuel Jose Jose el 2026-11-07 («okey si esta bien una vez que quedo esto me dices», respuesta al paquete propuesto: aprobación documental, «EJECUTA» de NV-08 T14, NV-07 T13, NV-01 T19, NV-09 T13, T18 y T20, NV-03 T08, T18 y T19, y autorización del seed en producción; `terraform apply` sigue requiriendo «OK PARA APPLY»):** agrega NV-07-R09 (seed regional de doce propiedades vinculadas al catálogo de NV-08 1.2.0-u2) y la task T13. Las tasks 1.1.0-u2 siguen cerradas.

Plan aprobado documentalmente para U2. No se implementa código en esta fase.

## 1. Enfoque

Property Core se implementa en cuatro fases: contrato y paginación; acceso a datos y proyección; endpoints y errores; pruebas y evidencia. Solo se exponen dos lecturas públicas.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y paginación | R01, R02, R03 | T01, T02 | DTOs y validación |
| F2 | Acceso a datos y proyección | R04, R05 | T03 | Repositorio e índice |
| F3 | Endpoints y errores | R01, R02, R05, R06, R08 | T04, T05, T06, T11 | 200/304/400/404/429/503 |
| F4 | Pruebas y evidencia | R01–R08 | T07, T08, T09, T10, T12 | Suite y evidencia |

## 3. Detalle de fases

### F1 — Contrato y paginación

**Requisitos:** NV-07-R01, NV-07-R02, NV-07-R03.

**Tasks:** T01, T02.

**Entregables U2:**

- DTOs de listado y detalle.
- Validación de `page`, `pageSize` e `id`.
- Cálculo de `totalPages` y resultado vacío.
- Orden estable `createdAt`/`_id`.

**Criterios de salida:**

1. El contrato coincide con `spec.md`.
2. `pageSize` máximo 50.
3. ID inválido produce 400.
4. No se aceptan filtros no aprobados.

**Evidencia:** pruebas unitarias y revisión del contrato.

### F2 — Acceso a datos y proyección

**Requisitos:** NV-07-R04, NV-07-R05.

**Tasks:** T03.

**Entregables U2:**

- Repositorio MongoDB de solo lectura.
- Proyección explícita de campos públicos.
- Índice `{createdAt:-1,_id:-1}`.
- Sin `ownerId`, dirección exacta, coordenadas ni contacto.

**Criterios de salida:**

1. No se devuelve el documento completo.
2. El listado usa el índice.
3. El detalle no filtra campos internos.
4. No hay N+1.

**Evidencia:** revisión de consultas, índice y pruebas de proyección.

### F3 — Endpoints y errores

**Requisitos:** NV-07-R01, NV-07-R02, NV-07-R05, NV-07-R06.

**Tasks:** T04, T05, T06.

**Entregables U2:**

- `GET /api/v1/properties`.
- `GET /api/v1/properties/{id}`.
- 200, 400, 404, 429, 503, 500.
- Problem Details con `traceId`.
- Rate limit 60/60 s por IP en Redis.
- Caché HTTP condicional: `ETag` débil, `Cache-Control` y 304 (T11, 1.1.0-u2).

**Criterios de salida:**

1. Listado vacío devuelve 200.
2. Detalle inexistente devuelve 404.
3. Los errores no filtran MongoDB ni stack traces.
4. Redis caído produce 503.

**Evidencia:** pruebas de integración y respuestas sanitizadas.

### F4 — Pruebas y evidencia

**Requisitos:** NV-07-R01 a NV-07-R07.

**Tasks:** T07, T08, T09, T10.

**Entregables U2:**

- Unitarias, integración y seguridad.
- Evidencia de paginación, detalle y proyección.
- Revisión de seguridad y hallazgos.

**Criterios de salida:**

1. Todas las suites relevantes pasan o tienen brecha declarada.
2. No se exponen campos privados.
3. No hay Critical/High abierto.
4. La revisión emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** reportes, comandos y acta.

## 4. Secuencia y dependencias

```text
F1 Contrato/paginación
  ↓
F2 Datos/proyección
  ↓
F3 Endpoints/errores
  ↓
F4 Pruebas/evidencia
```

- T02 depende de T01.
- T03 depende de T01.
- T04 depende de T02 y T03.
- T05 depende de T02 y T03.
- T06 depende de T04 y T05.
- T07 depende de T02.
- T08 depende de T03, T04, T05 y T06.
- T09 depende de T03, T05 y T06.
- T10 depende de T07, T08 y T09.
- T11 depende de T04 y T05.
- T12 depende de T09 y T11.

## 5. Testing

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Paginación, ID, proyección, DTO | xUnit | Reporte |
| Integración | Listado, detalle, 400/404/429/503 | WebApplicationFactory + Testcontainers si se autoriza | Reporte |
| Seguridad | Campos privados, errores, rate limit | Pruebas negativas | Hallazgos |
| Integración y seguridad | Caché HTTP: `ETag`, 304, `no-store` en errores | WebApplicationFactory | Reporte (T12) |

Testcontainers no se instala en Fase A.

## 6. Seguridad

- Proyección explícita; no documento completo.
- Sin dirección exacta, coordenadas, propietario ni contacto.
- Validar ObjectId antes de consultar.
- Sin datos personales en logs.
- Rate limit compartido y fallo cerrado.
- HTTPS en despliegue.

## 7. Costo

Property Core no requiere servicios pagos. MongoDB/Redis deben caber en COST-001 según NV-03. Si el costo es incierto, BLOCKED.

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.0.0-u2; 1.1.0-u2 para T11 y T12 | BLOCKED |
| G0-Ejec | Inicio | Autorización con SPEC/PLAN/TASK/ESTADO | BLOCKED |
| G1 | Fin F1 | Contrato y paginación | No avanzar |
| G2 | Fin F2 | Proyección e índice | No avanzar |
| G3 | Fin F3 | Endpoints y errores | No avanzar |
| G4 | Fin F4 | Pruebas y evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T02, T04, T07, T08 |
| R02 | F1 | T01, T02, T05, T08 |
| R03 | F1 | T02, T05, T07 |
| R04 | F2 | T03, T04, T05, T08 |
| R05 | F2 | T01, T03, T06, T07, T09 |
| R06 | F3 | T04, T05, T06, T08, T09 |
| R07 | F4 | T09, T10, T12 |
| R08 | F3 | T11, T12 |
| R09 | F2 | T13 |

## 10. No ejecución

Este plan no autoriza código, paquetes, build, tests, Docker, Terraform ni despliegue. Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)**.
