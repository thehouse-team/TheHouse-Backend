# NV-03 — Task list Scaling Cloud U2

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `task.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Spec | `spec.md` 1.2.0-u2 |
| Plan | `plan.md` 1.2.0-u2 |
| Tasks totales | 25 |
| Tasks U2 | 24 |
| Tasks futuras (no U2) | 1 |
| Tasks completadas | 3 (T13, T15, T23; ver `evidence.md`) |

## Reglas de esta lista

1. Solo se marcan como completadas las tasks con evidencia real en `evidence.md` (T13, T15, T23 al 2026-11-05).
2. Una task solo se completa con aceptación, evidencia y revisión.
3. Solo se ejecutan las tasks marcadas **Alcance: U2** y autorizadas en Fase B.
4. Cada task declara al menos un requisito.
5. Ninguna task autoriza gasto, apply o destroy por sí sola.
6. La aprobación de NV-01 o NV-02 no autoriza estas tasks.
7. `terraform apply` requiere `OK PARA APPLY`; `terraform destroy` requiere autorización humana.
8. Si el costo $0 no se demuestra, la task se detiene y se reporta BLOCKED.
9. T24 y T25 (1.2.0-u2) requieren la autorización humana «EJECUTA»; `terraform apply` sigue requiriendo `OK PARA APPLY`.

## F1 — Auditoría stateless

- [ ] **T01 · Inventario de estado local** — Requisito: NV-03-R01 · Fase: F1 · **Alcance: U2** · Aceptación: sesión, revocación, cuotas, bloqueos y trabajos en segundo plano inventariados y clasificados. · Evidencia: matriz de estado y revisión. · Depende de: autorización Fase B.
- [ ] **T02 · Diseño de externalización de estado** — Requisito: NV-03-R01 · Fase: F1 · **Alcance: U2** · Aceptación: cada estado necesario tiene destino compartido o justificación; los cambios se implementan en U2. · Evidencia: diseño revisado. · Depende de: T01.

## F2 — Estado compartido y datos

- [ ] **T03 · Topología y claves Redis** — Requisito: NV-03-R04 · Fase: F2 · **Alcance: U2** · Aceptación: namespace, TTL, tamaño, autenticación/TLS y separación por usuario definidos e implementados. · Evidencia: diseño de claves y revisión de seguridad. · Depende de: T01.
- [ ] **T04 · Invalidación y fallos de Redis** — Requisito: NV-03-R04 · Fase: F2 · **Alcance: U2** · Aceptación: invalidación tras escritura confirmada, stampede, timeout, reconexión y degradación sin abrir controles. · Evidencia: matriz de fallos y pruebas. · Depende de: T03.
- [ ] **T05 · Estado distribuido para autenticación y límites** — Requisitos: NV-03-R01, NV-03-R04 · Fase: F2 · **Alcance: U2** · Aceptación: revocación y cuotas globales usan estado compartido; no hay fallback local de seguridad. · Evidencia: diseño, implementación y revisión. · Depende de: T03, T04.
- [ ] **T06 · Conexión MongoDB, pooling e índices** — Requisito: NV-03-R05 · Fase: F2 · **Alcance: U2** · Aceptación: pooling, timeouts, cancelación, TLS, credenciales protegidas e índices justificados por consultas U2. · Evidencia: diseño, implementación y revisión de consultas. · Depende de: T01.
- [ ] **T07 · Resiliencia y seguridad MongoDB** — Requisito: NV-03-R05 · Fase: F2 · **Alcance: U2** · Aceptación: reconexión, límites, mínimo privilegio y fallo controlado; sin exponer la base a Internet. · Evidencia: matriz de fallos y revisión. · Depende de: T06.

## F3 — Réplicas, balanceo y TLS

- [ ] **T08 · Modelo de dos réplicas** — Requisito: NV-03-R02 · Fase: F3 · **Alcance: U2** · Aceptación: misma imagen/configuración, salud independiente, reemplazo sin afinidad de sesión. · Evidencia: topología y revisión. · Depende de: T01, T02.
- [ ] **T09 · Load balancing y health checks** — Requisito: NV-03-R03 · Fase: F3 · **Alcance: U2** · Aceptación: Container Apps distribuye tráfico, usa `/health/live` y `/health/ready`, retira réplicas no listas y no usa sticky sessions. · Evidencia: configuración y pruebas. · Depende de: T08.
- [ ] **T10 · TLS, HSTS y confianza en proxy** — Requisito: NV-03-R06 · Fase: F3 · **Alcance: U2** · Aceptación: certificado válido, terminación, renovación, HSTS y validación de certificados habilitada. · Evidencia: configuración y análisis TLS. · Depende de: T08.

## F4 — Observabilidad, imagen y GHCR

- [ ] **T11 · Instrumentación OpenTelemetry** — Requisito: NV-03-R07 · Fase: F4 · **Alcance: U2** · Aceptación: trazas, métricas y logs correlacionados entre réplicas y dependencias. · Evidencia: configuración y trazas. · Depende de: T08.
- [ ] **T12 · Cardinalidad, PII, retención y costo de telemetría** — Requisitos: NV-03-R07, NV-03-R13 · Fase: F4 · **Alcance: U2** · Aceptación: exportador, muestreo, límites, retención y costo $0 verificados; sin datos sensibles. · Evidencia: matriz de telemetría y costo. · Depende de: T11.
- [x] **T13 · Imagen Docker multi-stage y no root** — Requisito: NV-03-R08 · Fase: F4 · **Alcance: U2** · Aceptación: base fijada, usuario no root, sin secretos, healthcheck y configuración para dos réplicas. · Evidencia: Dockerfile, `.dockerignore` y revisión. · Depende de: T01.
- [ ] **T14 · Handoff de imagen a NV-02** — Requisito: NV-03-R08 · Fase: F4 · **Alcance: Futuro (no U2)** · Aceptación: digest, SBOM y contexto entregados para Trivy, Dockle y Cosign. · Evidencia: registro de handoff. · Depende de: T13 y NV-02.
- [ ] **T22 · Publicación GHCR y VERSIONING.md** — Requisitos: NV-03-R08, NV-03-R14 · Fase: F4 · **Alcance: U2** · Aceptación: imagen publicada en `ghcr.io` con tag/commit, sin token impreso; `VERSIONING.md` registra versión, fecha, imagen, tag, commit, entorno, URL y estado. · Evidencia: digest, tag, registro y revisión. · Depende de: T13.
- [ ] **T24 · Log Analytics con tope diario en Terraform** — Requisitos: NV-03-R15, NV-03-R07, NV-03-R13 · Fase: F4 · **Alcance: U2 (1.2.0-u2)** · Aceptación: `azurerm_log_analytics_workspace` con SKU `PerGB2018`, `daily_quota_gb = 0.1`, `retention_in_days = 30` y etiquetas; el entorno de Container Apps envía sus logs a ese workspace; `terraform plan` sin reemplazo del entorno ni de la app (si lo propone: BLOCKED y opción B); apply solo con `OK PARA APPLY`. · Evidencia: diff, plan revisado y salida del apply. · Depende de: T15, T16 y autorización «EJECUTA».
- [ ] **T25 · Evidencia de observabilidad y costo** — Requisitos: NV-03-R15, NV-03-R13 · Fase: F4 · **Alcance: U2 (1.2.0-u2)** · Aceptación: consulta KQL de errores y eventos con registros reales del backend; logs sin PII, tokens ni cadenas de conexión; uso de ingestión y Cost Management registrados con fecha; `evidence.md` actualizado. · Evidencia: consulta, salida sanitizada y costo. · Depende de: T24.

## F5 — Terraform y costo Azure

- [x] **T15 · Estructura Terraform y estado** — Requisito: NV-03-R09 · Fase: F5 · **Alcance: U2** · Aceptación: módulos, variables, versiones fijadas, estado protegido, etiquetado y plan/apply/destroy; sin secretos. · Evidencia: diseño IaC, `terraform fmt -check` y `terraform validate`. · Depende de: T08, T13.
- [ ] **T16 · Evidencia de costo Azure U2** — Requisito: NV-03-R10 · Fase: F5 · **Alcance: U2** · Aceptación: región, SKU, free allowance, cuotas, tarjeta, riesgo y estimación para Container Apps, dos réplicas, GHCR, logs, egress, Redis, MongoDB y TLS. · Evidencia: matriz y fuentes con fecha. · Depende de: T15.
- [ ] **T17 · Gate de costo y OK PARA APPLY** — Requisitos: NV-03-R10, NV-03-R13 · Fase: F5 · **Alcance: U2** · Aceptación: costo total $0 demostrado; si no, BLOCKED; el plan se muestra y se espera `OK PARA APPLY` humano antes de aplicar. · Evidencia: dictamen, resumen del plan y acta. · Depende de: T16.

## F6 — Creación, verificación, apagado y destrucción

- [ ] **T18 · Plan y creación de infraestructura** — Requisitos: NV-03-R11, NV-03-R02, NV-03-R03, NV-03-R04, NV-03-R05, NV-03-R06, NV-03-R09 · Fase: F6 · **Alcance: U2** · Aceptación: plan aprobado con dos réplicas, HTTPS, Redis, MongoDB y observabilidad; apply solo tras `OK PARA APPLY`; inventario y costo inicial. · Evidencia: plan revisado, inventario y respuestas. · Depende de: T09, T10, T11, T15, T17, T22.
- [ ] **T19 · Verificación de despliegue, escalado, fallos y TLS** — Requisitos: NV-03-R01, NV-03-R02, NV-03-R03, NV-03-R04, NV-03-R05, NV-03-R06, NV-03-R07 · Fase: F6 · **Alcance: U2** · Aceptación: smoke tests de 7 endpoints, health, rate limit 429, logout/reuso, réplicas >= 2, TLS y fallos Redis/MongoDB verificados. · Evidencia: reportes de prueba. · Depende de: T18.
- [ ] **T20 · Destrucción y verificación de recursos residuales** — Requisitos: NV-03-R12, NV-03-R13, NV-03-R09 · Fase: F6 · **Alcance: U2** · Aceptación: inventario vacío o justificado, sin recursos facturables y con evidencia posterior; destroy solo con autorización humana. · Evidencia: plan de destrucción, inventario y facturación. · Depende de: T18, T19.
- [ ] **T21 · Atestación de costo observado** — Requisito: NV-03-R13 · Fase: F6 · **Alcance: U2** · Aceptación: costo del ciclo de vida reportado con evidencia; si Azure no refleja datos, se registra PENDIENTE DE REFLEJO; cualquier duda se reporta BLOCKED. · Evidencia: informe final y revisión. · Depende de: T20.
- [x] **T23 · Scale-to-zero y apagado operativo** — Requisitos: NV-03-R12, NV-03-R13 · Fase: F6 · **Alcance: U2** · Aceptación: reducción de `min replicas` a 0 con nombres reales, sin destruir datos ni recursos externos; comando exacto documentado. · Evidencia: comando, salida y estado. · Depende de: T18, T19.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Alcance | Estado |
| --- | --- | --- | --- | --- |
| T01 | R01 | F1 | U2 | ABIERTA |
| T02 | R01 | F1 | U2 | ABIERTA |
| T03 | R04 | F2 | U2 | ABIERTA |
| T04 | R04 | F2 | U2 | ABIERTA |
| T05 | R01, R04 | F2 | U2 | ABIERTA |
| T06 | R05 | F2 | U2 | ABIERTA |
| T07 | R05 | F2 | U2 | ABIERTA |
| T08 | R02 | F3 | U2 | ABIERTA |
| T09 | R03 | F3 | U2 | ABIERTA |
| T10 | R06 | F3 | U2 | ABIERTA |
| T11 | R07 | F4 | U2 | ABIERTA |
| T12 | R07, R13 | F4 | U2 | ABIERTA |
| T13 | R08 | F4 | U2 | COMPLETADA |
| T14 | R08 | F4 | Futuro | ABIERTA |
| T15 | R09 | F5 | U2 | COMPLETADA |
| T16 | R10 | F5 | U2 | ABIERTA |
| T17 | R10, R13 | F5 | U2 | ABIERTA |
| T18 | R11, R02, R03, R04, R05, R06, R09 | F6 | U2 | ABIERTA |
| T19 | R01, R02, R03, R04, R05, R06, R07 | F6 | U2 | ABIERTA |
| T20 | R12, R13, R09 | F6 | U2 | ABIERTA |
| T21 | R13 | F6 | U2 | ABIERTA |
| T22 | R08, R14 | F4 | U2 | ABIERTA |
| T23 | R12, R13 | F6 | U2 | COMPLETADA |
| T24 | R15, R07, R13 | F4 | U2 | ABIERTA |
| T25 | R15, R13 | F4 | U2 | ABIERTA |

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
| R08 | T13, T14, T22 |
| R09 | T15, T18, T20 |
| R10 | T16, T17 |
| R11 | T18 |
| R12 | T20, T23 |
| R13 | T12, T17, T20, T21, T23 |
| R14 | T22 |
| R15 | T24, T25 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 15 |
| Tasks | 25 |
| Tasks U2 | 24 |
| Tasks futuras (no U2) | 1 |
| Tasks abiertas | 22 |
| Tasks completadas | 3 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |

Ninguna task puede ejecutarse hasta la aprobación de Fase B con versiones, IDs, rutas, proveedor, servicios y restricciones explícitas. `terraform apply` y `terraform destroy` requieren confirmación humana separada.
