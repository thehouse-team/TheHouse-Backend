# NV-03 — Plan de scaling cloud U2

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `plan.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-05 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **🟡 EJECUCIÓN PARCIAL — 5 de 25 tasks (T13, T15, T23, T24, T25); ver `evidence.md`** |
| Spec | `spec.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Reemplaza | `1.0.0-borrador`; el historial permanece en Git |

Plan aprobado documentalmente para U2. No se crea infraestructura, no se ejecuta Docker, no se ejecuta Terraform y no se despliega en esta fase. La ejecución requiere autorización de Fase B; apply y destroy requieren confirmación humana separada.

## 1. Enfoque

La ejecución U2 se divide en seis fases. Primero se resuelve el estado local; después Redis/MongoDB; luego réplicas, balanceo y TLS; después observabilidad, imagen y GHCR; finalmente Terraform, costo Azure y creación/verificación/apagado/destrucción.

La secuencia no puede saltarse: crear dos réplicas antes de resolver el estado local produce un sistema que falla al escalar. Desplegar antes de demostrar COST-001 viola la constitución. Ejecutar `terraform apply` sin `OK PARA APPLY` humano está prohibido.

## 2. Alcance U2

Tasks U2 de NV-03:

```text
T01, T02, T03, T04, T05, T06, T07, T08, T09, T10,
T11, T12, T13, T15, T16, T17, T18, T19, T20, T21,
T22, T23
```

Task no U2:

```text
T14 — handoff de imagen (digest y SBOM); lo cubre NV-09 (CD) desde el 2026-11-06.
```

## 3. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Auditoría stateless | R01 | T01, T02 | Inventario y diseño sin estado local |
| F2 | Estado compartido y datos | R01, R04, R05 | T03, T04, T05, T06, T07 | Redis y MongoDB con fallos definidos |
| F3 | Réplicas, balanceo y TLS | R02, R03, R06 | T08, T09, T10 | Dos réplicas, LB y HTTPS |
| F4 | Observabilidad, imagen y GHCR | R07, R08, R14, R15 | T11, T12, T13, T22, T24, T25 | Telemetría, logs persistentes, imagen y trazabilidad |
| F5 | Terraform y costo Azure | R09, R10 | T15, T16, T17 | IaC y gate de costo/apply |
| F6 | Creación, verificación, apagado y destrucción | R01–R07, R11–R13 | T18, T19, T20, T21, T23 | Evidencia, costo y limpieza |

## 4. Detalle de fases

### F1 — Auditoría stateless

**Requisitos:** NV-03-R01.

**Tasks:** T01, T02.

**Entregables U2:**

- Inventario de estado: sesión, revocación, cuotas, bloqueos y trabajos en segundo plano.
- Clasificación local/compartido.
- Diseño de externalización y criterios de corrección.
- Cambios necesarios para multi-réplica.

**Criterios de salida:**

1. Ningún estado necesario para la corrección queda solo en una instancia.
2. La revocación y las cuotas no dependen de memoria local.
3. Reemplazar una instancia no cambia el resultado funcional ni de seguridad.

**Evidencia:** inventario, matriz de estado y revisión de arquitectura.

### F2 — Estado compartido y datos

**Requisitos:** NV-03-R01, NV-03-R04, NV-03-R05.

**Tasks:** T03, T04, T05, T06, T07.

**Entregables U2:**

- Topología Redis, namespace, TTL, autenticación/TLS y timeouts.
- Comportamiento ante fallo de Redis y límites de degradación.
- Conexión MongoDB con pooling, timeouts, cancelación, TLS e índices.
- Estado distribuido para revocación y rate limiting.
- Readiness coherente ante fallos de MongoDB/Redis.

**Criterios de salida:**

1. Redis no es fuente de verdad.
2. Ningún fallback local abre un control de seguridad.
3. MongoDB tiene timeouts y reconexión definidos.
4. Los índices responden a las consultas U2.
5. El costo de ambos servicios es $0 o la fase se bloquea.

**Evidencia:** diseño de claves, consultas/índices, pruebas de fallo y revisión de costos.

### F3 — Réplicas, balanceo y TLS

**Requisitos:** NV-03-R02, NV-03-R03, NV-03-R06.

**Tasks:** T08, T09, T10.

**Entregables U2:**

- Modelo de dos réplicas con la misma imagen y configuración.
- Container Apps con health checks `/health/live` y `/health/ready`.
- Política de cabeceras reenviadas y confianza en proxy.
- Certificado, terminación TLS, HSTS y renovación.
- Prueba de reemplazo y fallo de instancia.

**Criterios de salida:**

1. Dos réplicas responden de forma independiente.
2. La plataforma retira una réplica no lista.
3. No se usan sticky sessions.
4. TLS es válido y no se desactiva la verificación.
5. Las cabeceras de proxy se aceptan solo desde fuentes confiables.

**Evidencia:** topología, configuración revisada, pruebas de distribución y TLS.

### F4 — Observabilidad, imagen y GHCR

**Requisitos:** NV-03-R07, NV-03-R08, NV-03-R14, NV-03-R15.

**Tasks:** T11, T12, T13, T22, T24, T25.

**Entregables U2:**

- Trazas, métricas y logs con correlación.
- Exportador, muestreo, cardinalidad y retención compatibles con COST-001.
- `Dockerfile` multi-stage, no root, sin secretos y con healthcheck.
- Publicación de imagen en GHCR con tag y commit trazables.
- `VERSIONING.md` sin secretos.
- Log Analytics con `daily_quota_gb = 0.1` y 30 días de retención, conectado al entorno de Container Apps (T24, 1.2.0-u2).
- Consulta de errores documentada y evidencia de uso y costo de ingestión (T25).

**Criterios de salida:**

1. Una petición se sigue entre réplicas y dependencias.
2. No se registran secretos ni PII.
3. La telemetría no genera costo no aprobado.
4. La imagen cumple las restricciones y se publica en GHCR.
5. `VERSIONING.md` coincide con la imagen y el commit.
6. Los logs de eventos y errores persisten en Azure, sin PII ni secretos, y el plan no recrea el entorno ni la app.

**Evidencia:** configuración OTel, Dockerfile, digest, tag, `VERSIONING.md` y reporte de publicación.

### F5 — Terraform y costo Azure

**Requisitos:** NV-03-R09, NV-03-R10.

**Tasks:** T15, T16, T17.

**Entregables U2:**

- Estructura Terraform con módulos, variables, versiones y etiquetas.
- Estado protegido y estrategia de plan/apply/destroy.
- Evidencia de costo Azure U2: región, SKU, free allowance, cuotas, tarjeta, riesgo y estimación.
- Gate de costo y confirmación `OK PARA APPLY`.

**Criterios de salida:**

1. Terraform no contiene secretos.
2. La evidencia cubre el conjunto completo U2.
3. El costo total es $0 con evidencia vigente; si no, BLOCKED.
4. El plan se muestra y se detiene antes de apply.
5. Sin `OK PARA APPLY` humano no se ejecuta apply.

**Evidencia:** código Terraform, plan, evidencia de precios/cuotas y acta.

### F6 — Creación, verificación, apagado y destrucción

**Requisitos:** NV-03-R01 a NV-03-R07, NV-03-R11, NV-03-R12, NV-03-R13.

**Tasks:** T18, T19, T20, T21, T23.

**Entregables U2:**

- Inventario de recursos y plan aprobado.
- Dos réplicas, HTTPS, Redis, MongoDB y observabilidad.
- Smoke tests de los siete endpoints, health, rate limit y logout.
- Verificación de réplicas con Azure CLI.
- Scale-to-zero y/o destrucción con autorización.
- Atestación de costo observado.

**Criterios de salida:**

1. La creación coincide con el plan aprobado.
2. Las pruebas de escalado y fallo pasan o se reportan como brecha.
3. El apagado/destrucción deja inventario vacío o justificado.
4. El costo se reporta con evidencia; no se afirma $0 sin datos.
5. La revisión final emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** inventario, reportes de prueba, capturas de Cost Management, plan de destrucción y acta.

## 5. Secuencia y dependencias

```text
F1 Stateless
  ↓
F2 Redis/MongoDB
  ↓
F3 Dos réplicas/LB/TLS
  ↓
F4 OTel/Docker/GHCR/VERSIONING
  ↓
F5 Terraform/costo Azure/OK PARA APPLY
  ↓
F6 Crear/verificar/apagar/destruir
```

Dependencias internas:

- T02 depende de T01.
- T05 depende de T03 y T04.
- T07 depende de T06.
- T08 depende de T01 y T02.
- T09 depende de T08.
- T10 depende de T08.
- T11 depende de T08.
- T12 depende de T11.
- T13 depende de T01.
- T15 depende de T08 y T13.
- T16 depende de T15.
- T17 depende de T16.
- T18 depende de T09, T10, T11, T15, T17 y T22.
- T19 depende de T18.
- T20 depende de T18 y T19.
- T21 depende de T20.
- T22 depende de T13.
- T23 depende de T18 y T19.
- T24 depende de T15 y T16.
- T25 depende de T24.

T14 (handoff a NV-02) no es U2 y no se ejecuta.

## 6. Estrategia de verificación

| Prueba | Qué demuestra | Entorno | Evidencia |
| --- | --- | --- | --- |
| Smoke HTTPS | Siete endpoints y errores | Azure U2 | Método, ruta, status |
| Health live/ready | Semántica de salud | Azure U2 | Respuestas y códigos |
| Login 429 | Rate limit real | Azure U2 | Umbral y respuestas |
| Logout/reuso | Revocación compartida | Azure U2 | Token antes/después |
| Réplicas | >= 2 activas | Azure U2 | `az containerapp replica list` |
| Reemplazo de réplica | Stateless | Azure U2 | Logs y respuestas |
| Fallo Redis | Degradación controlada | Azure U2 | Reporte y salud |
| Fallo MongoDB | Timeouts y readiness | Azure U2 | Reporte |
| TLS | Certificado y HSTS | Azure U2 | Análisis y cabeceras |
| Cost Management | Costo observado | Azure | Captura y fecha |
| Destroy | Sin recursos residuales | Azure | Inventario |

Los SLO no están aprobados. No se copian umbrales de otro repositorio.

## 7. Creación, apply y destrucción

**Antes de crear:**

1. Aprobar proveedor U2, servicios, región y costo.
2. Completar la evidencia de la sección 7 de `spec.md`.
3. Revisar `terraform plan` sin secretos.
4. Mostrar resumen de recursos a crear/modificar/destruir y riesgo de costo.
5. **DETENERSE** y esperar `OK PARA APPLY`.

**Después del OK humano:**

1. Ejecutar `terraform apply` con la configuración aprobada.
2. Verificar inventario, réplicas y HTTPS.
3. Ejecutar smoke tests y registrar evidencia.
4. Consultar Cost Management.

**Apagado/destrucción:**

1. Reducir `min replicas` a 0 si la arquitectura lo permite.
2. Ejecutar el plan de destrucción aprobado.
3. Verificar que no queden réplicas, discos, IPs, certificados, logs ni registros.
4. Revisar facturación posterior.
5. Documentar cualquier recurso conservado y su justificación.

No se asume que `destroy` elimina recursos externos, imágenes o servicios de terceros.

## 8. Costo — COST-001

| Componente | Requisito | Si no se demuestra |
| --- | --- | --- |
| Container Apps (2 réplicas) | $0 | BLOCKED |
| HTTPS/TLS | $0 | BLOCKED |
| GHCR | $0 | BLOCKED |
| Redis | $0 | BLOCKED |
| MongoDB | $0 | BLOCKED |
| Observabilidad | $0: Log Analytics 0–5 GB/mes a 0.00 USD y tope de 0.1 GB/día (spec §7.1) | BLOCKED |
| Egress | $0 | BLOCKED |
| Terraform/estado | $0 | BLOCKED |

Un free tier, un crédito o una alerta de presupuesto no prueban costo cero. Si el conjunto no puede ser $0, NV-03 U2 no se aprueba para ejecución.

## 9. Seguridad

- Identidades y roles con mínimo privilegio.
- Sin secretos en Terraform, imágenes, estado, `VERSIONING.md` o logs.
- MongoDB y Redis no expuestos públicamente.
- TLS válido y verificación habilitada.
- Cabeceras de proxy confiables y validadas.
- Health checks sin credenciales ni detalles internos.
- Telemetría sin PII ni cardinalidad ilimitada.
- Imagen no root y sin secretos.
- Destrucción de credenciales temporales.
- Revisión de seguridad antes de crear y después de destruir.
- NV-02 no se ejecuta; no se declaran SAST/DAST/SBOM/firma.

## 10. Puertas de revisión

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de Fase B | Aprobación documental 1.1.0-u2; 1.2.0-u2 para T24 y T25 | BLOCKED |
| G0-Ejec | Inicio de Fase B | Autorización con SPEC, PLAN, TASK y ESTADO | BLOCKED |
| G1 | Fin de F1 | Estado local inventariado y resuelto | No avanzar |
| G2 | Fin de F2 | Redis/MongoDB con fallos y costo $0 | No avanzar |
| G3 | Fin de F3 | Dos réplicas, LB y TLS verificables | No avanzar |
| G4 | Fin de F4 | OTel, imagen, GHCR y VERSIONING | No avanzar |
| G5 | Fin de F5 | Evidencia Azure y `OK PARA APPLY` | BLOCKED |
| G6 | Fin de F6 | Creación, verificación, apagado y destrucción | NV-03 no se cierra |

## 11. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T02, T05, T19 |
| R02 | F3 | T08, T18, T19 |
| R03 | F3 | T09, T18, T19 |
| R04 | F2 | T03, T04, T05, T18, T19 |
| R05 | F2 | T06, T07, T18, T19 |
| R06 | F3 | T10, T18, T19 |
| R07 | F4 | T11, T12, T19, T24 |
| R08 | F4 | T13, T14, T22 |
| R09 | F5 | T15, T18, T20 |
| R10 | F5 | T16, T17 |
| R11 | F6 | T18 |
| R12 | F6 | T20, T23 |
| R13 | F6 | T12, T17, T20, T21, T23, T24, T25 |
| R14 | F4 | T22 |
| R15 | F4 | T24, T25 |

## 12. No ejecución

Este plan no autoriza:

- Crear Dockerfile, `.dockerignore`, Terraform o recursos cloud.
- Ejecutar `docker build`, `docker push`, `terraform init`, `terraform plan`, `terraform apply` o `az login`.
- Desplegar, escalar o destruir recursos.
- Marcar tasks como completadas.
- Ejecutar NV-02 o declarar sus controles.
- Afirmar costo $0 sin evidencia.

Estado documental: **✅ APROBADA PARA U2**. Estado de ejecución: **🟡 EJECUCIÓN PARCIAL — 4 de 25 tasks**; ver `evidence.md`.
