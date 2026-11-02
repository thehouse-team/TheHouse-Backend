# NV-03 — Task list Scaling Cloud

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `task.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks totales | 21 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas y deben permanecer así durante la planificación.
2. Una task solo se completa con aceptación, evidencia y revisión.
3. Escribir este documento no completa ninguna task.
4. Cada task declara al menos un requisito.
5. Ninguna task autoriza seleccionar proveedor, provisionar o gastar.
6. La aprobación de NV-01 o NV-02 no autoriza estas tasks.
7. Si el costo $0 no se demuestra, la task se detiene y se reporta BLOCKED.

## F1 — Auditoría stateless

- [ ] **T01 · Inventario de estado local** — Requisito: NV-03-R01 · Fase: F1 · Aceptación: sesión, caché, revocación, cuotas, bloqueos y trabajos en segundo plano inventariados y clasificados. · Evidencia: matriz de estado y revisión. · Depende de: autorización NV-03.
- [ ] **T02 · Diseño de externalización de estado** — Requisito: NV-03-R01 · Fase: F1 · Aceptación: cada estado necesario tiene destino compartido o justificación; sin cambios de código en esta entrega. · Evidencia: diseño revisado. · Depende de: T01.

## F2 — Estado compartido y datos

- [ ] **T03 · Topología y claves Redis** — Requisito: NV-03-R04 · Fase: F2 · Aceptación: namespace, TTL, tamaño, autenticación/TLS y separación por usuario definidos. · Evidencia: diseño de claves y revisión de seguridad. · Depende de: T01.
- [ ] **T04 · Invalidación y fallos de Redis** — Requisito: NV-03-R04 · Fase: F2 · Aceptación: invalidación tras escritura confirmada, stampede, timeout, reconexión y degradación sin abrir controles. · Evidencia: matriz de fallos y pruebas futuras. · Depende de: T03.
- [ ] **T05 · Estado distribuido para autenticación y límites** — Requisitos: NV-03-R01, NV-03-R04 · Fase: F2 · Aceptación: revocación y cuotas que deban ser globales usan estado compartido; no hay fallback local de seguridad. · Evidencia: diseño y revisión. · Depende de: T03, T04.
- [ ] **T06 · Conexión MongoDB, pooling e índices** — Requisito: NV-03-R05 · Fase: F2 · Aceptación: pooling, timeouts, cancelación, TLS, credenciales protegidas e índices justificados por consultas. · Evidencia: diseño y revisión de consultas. · Depende de: T01.
- [ ] **T07 · Resiliencia y seguridad MongoDB** — Requisito: NV-03-R05 · Fase: F2 · Aceptación: reconexión, límites, mínimo privilegio y fallo controlado; sin exponer la base a Internet. · Evidencia: matriz de fallos y revisión. · Depende de: T06.

## F3 — Réplicas, balanceo y TLS

- [ ] **T08 · Modelo de dos instancias** — Requisito: NV-03-R02 · Fase: F3 · Aceptación: misma imagen/configuración, salud independiente, reemplazo sin afinidad de sesión. · Evidencia: topología y revisión. · Depende de: T01, T02.
- [ ] **T09 · Load Balancer y health checks** — Requisito: NV-03-R03 · Fase: F3 · Aceptación: distribución, retirada por salud, cabeceras de proxy controladas y ausencia de sticky sessions. · Evidencia: configuración y pruebas futuras. · Depende de: T08.
- [ ] **T10 · TLS, HSTS y confianza en proxy** — Requisito: NV-03-R06 · Fase: F3 · Aceptación: certificado válido, terminación, renovación, HSTS y validación de certificados habilitada. · Evidencia: configuración y análisis TLS. · Depende de: T08.

## F4 — Observabilidad y contenedor

- [ ] **T11 · Instrumentación OpenTelemetry** — Requisito: NV-03-R07 · Fase: F4 · Aceptación: trazas, métricas y logs correlacionados entre instancias y dependencias. · Evidencia: diseño y trazas futuras. · Depende de: T08.
- [ ] **T12 · Cardinalidad, PII, retención y costo de telemetría** — Requisitos: NV-03-R07, NV-03-R13 · Fase: F4 · Aceptación: exportador, muestreo, límites, retención y costo $0 verificados; sin datos sensibles. · Evidencia: matriz de telemetría y costo. · Depende de: T11.
- [ ] **T13 · Imagen Docker multi-stage y no root** — Requisito: NV-03-R08 · Fase: F4 · Aceptación: base fijada, usuario no root, sin secretos, healthcheck y configuración para dos instancias. · Evidencia: Dockerfile futuro y revisión. · Depende de: T01.
- [ ] **T14 · Handoff de imagen a NV-02** — Requisito: NV-03-R08 · Fase: F4 · Aceptación: digest, SBOM y contexto entregados para Trivy, Dockle y Cosign. · Evidencia: registro de handoff. · Depende de: T13 y NV-02.

## F5 — Terraform y matriz cloud

- [ ] **T15 · Estructura Terraform y estado** — Requisito: NV-03-R09 · Fase: F5 · Aceptación: módulos, variables, versiones fijadas, estado protegido, etiquetado y plan/apply/destroy. · Evidencia: diseño IaC y revisión. · Depende de: T08, T13.
- [ ] **T16 · Matriz AWS vs Azure vs Google Cloud** — Requisito: NV-03-R10 · Fase: F5 · Aceptación: 13 criterios completos con evidencia vigente, región, cuota y fecha. · Evidencia: matriz y fuentes. · Depende de: T15.
- [ ] **T17 · Gate de costo y decisión de proveedor** — Requisitos: NV-03-R10, NV-03-R13 · Fase: F5 · Aceptación: costo total $0 demostrado; si no, BLOCKED; la selección requiere aprobación humana separada. · Evidencia: dictamen y acta. · Depende de: T16.

## F6 — Creación, verificación y destrucción

- [ ] **T18 · Plan de creación de infraestructura** — Requisitos: NV-03-R11, NV-03-R02, NV-03-R03, NV-03-R04, NV-03-R05, NV-03-R06, NV-03-R09 · Fase: F6 · Aceptación: plan aprobado con dos instancias, LB, TLS, Redis, MongoDB y observabilidad; inventario y costo inicial. · Evidencia: plan revisado e inventario. · Depende de: T09, T10, T11, T15, T17.
- [ ] **T19 · Verificación de escalado, fallos y TLS** — Requisitos: NV-03-R01, NV-03-R02, NV-03-R03, NV-03-R04, NV-03-R05, NV-03-R06, NV-03-R07 · Fase: F6 · Aceptación: reemplazo de instancia, distribución, retirada, TLS, fallos Redis/MongoDB y telemetría verificados. · Evidencia: reportes de prueba. · Depende de: T18.
- [ ] **T20 · Destrucción y verificación de recursos residuales** — Requisitos: NV-03-R12, NV-03-R13, NV-03-R09 · Fase: F6 · Aceptación: inventario vacío o justificado, sin recursos facturables y con evidencia posterior. · Evidencia: plan de destrucción, inventario y facturación. · Depende de: T18, T19.
- [ ] **T21 · Atestación final de costo $0** — Requisito: NV-03-R13 · Fase: F6 · Aceptación: costo total del ciclo de vida $0 con evidencia; cualquier duda se reporta BLOCKED. · Evidencia: informe final y revisión. · Depende de: T20.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R01 | F1 | ABIERTA |
| T03 | R04 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R01, R04 | F2 | ABIERTA |
| T06 | R05 | F2 | ABIERTA |
| T07 | R05 | F2 | ABIERTA |
| T08 | R02 | F3 | ABIERTA |
| T09 | R03 | F3 | ABIERTA |
| T10 | R06 | F3 | ABIERTA |
| T11 | R07 | F4 | ABIERTA |
| T12 | R07, R13 | F4 | ABIERTA |
| T13 | R08 | F4 | ABIERTA |
| T14 | R08 | F4 | ABIERTA |
| T15 | R09 | F5 | ABIERTA |
| T16 | R10 | F5 | ABIERTA |
| T17 | R10, R13 | F5 | ABIERTA |
| T18 | R11, R02, R03, R04, R05, R06, R09 | F6 | ABIERTA |
| T19 | R01, R02, R03, R04, R05, R06, R07 | F6 | ABIERTA |
| T20 | R12, R13, R09 | F6 | ABIERTA |
| T21 | R13 | F6 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01, T02, T05, T19 |
| R02 | T08, T18, T19 |
| R03 | T09, T18, T19 |
| R04 | T03, T04, T05, T18, T19 |
| R05 | T06, T07, T18, T19 |
| R06 | T10, T18, T19 |
| R07 | T11, T12, T19 |
| R08 | T13, T14 |
| R09 | T15, T18, T20 |
| R10 | T16, T17 |
| R11 | T18 |
| R12 | T20 |
| R13 | T12, T17, T20, T21 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Tasks | 21 |
| Tasks abiertas | 21 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado | **⛔ NO EJECUTAR** |

Ninguna task puede ejecutarse hasta la aprobación humana de `spec.md`, `plan.md` y `task.md`, con versiones, IDs, rutas, proveedor, servicios y restricciones explícitas.
