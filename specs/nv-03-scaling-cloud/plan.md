# NV-03 — Plan de scaling cloud

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

Plan de ejecución futura. No se crea infraestructura, no se selecciona proveedor y no se despliega en esta entrega.

## 1. Enfoque

La ejecución futura se divide en seis fases. Primero se elimina el estado local y se define el estado compartido; después se habilitan réplicas, balanceo y TLS; luego observabilidad y contenedor; finalmente se evalúa el proveedor, se crea, se verifica y se destruye.

La secuencia no puede saltarse: crear dos instancias antes de resolver el estado local produce un sistema que falla al escalar. Seleccionar proveedor antes de completar la matriz viola el gobierno. Desplegar antes de demostrar $0 viola COST-001.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Auditoría stateless | R01 | T01, T02 | Inventario y diseño sin estado local |
| F2 | Estado compartido y datos | R01, R04, R05 | T03, T04, T05, T06, T07 | Redis y MongoDB con fallos definidos |
| F3 | Réplicas, balanceo y TLS | R02, R03, R06 | T08, T09, T10 | Dos instancias, LB y HTTPS |
| F4 | Observabilidad y contenedor | R07, R08 | T11, T12, T13, T14 | Telemetría e imagen |
| F5 | Terraform y matriz cloud | R09, R10 | T15, T16, T17 | IaC y decisión de proveedor |
| F6 | Creación, verificación y destrucción | R01–R07, R11–R13 | T18, T19, T20, T21 | Evidencia y costo $0 |

## 3. Detalle de fases

### F1 — Auditoría stateless

**Requisitos:** NV-03-R01.

**Tasks:** T01, T02.

**Entregables futuros:**

- Inventario de estado: sesión, caché, revocación, cuotas, bloqueos, trabajos en segundo plano.
- Clasificación por estado: local, compartido o innecesario.
- Diseño de externalización y criterios de corrección.
- Lista de cambios futuros, sin implementarlos ahora.

**Criterios de salida:**

1. Ningún estado necesario para la corrección queda solo en una instancia.
2. La revocación y las cuotas no dependen de memoria local.
3. Reemplazar una instancia no cambia el resultado funcional ni de seguridad.
4. Los cambios propuestos están dentro de una futura autorización.

**Evidencia:** inventario, matriz de estado y revisión de arquitectura.

### F2 — Estado compartido y datos

**Requisitos:** NV-03-R01, NV-03-R04, NV-03-R05.

**Tasks:** T03, T04, T05, T06, T07.

**Entregables futuros:**

- Topología Redis, claves, TTL, invalidación y autenticación/TLS.
- Comportamiento ante fallo de Redis y límites de degradación.
- Conexión MongoDB con pooling, timeouts, cancelación y TLS.
- Índices justificados por consultas y límites de documento.
- Diseño de estado distribuido para autenticación y límites, si la spec lo requiere.

**Criterios de salida:**

1. Redis no es fuente de verdad.
2. Ningún fallback local abre un control de seguridad.
3. MongoDB tiene timeouts y reconexión definidos.
4. Los índices responden a consultas reales.
5. El costo de ambos servicios es $0 o la fase se bloquea.

**Evidencia:** diseño de claves, consultas/índices, pruebas de fallo y revisión de costos.

### F3 — Réplicas, balanceo y TLS

**Requisitos:** NV-03-R02, NV-03-R03, NV-03-R06.

**Tasks:** T08, T09, T10.

**Entregables futuros:**

- Modelo de dos instancias con la misma imagen y configuración.
- Load Balancer con health checks, distribución y retirada.
- Política de cabeceras reenviadas y confianza en proxy.
- Certificado, terminación TLS, HSTS y renovación.
- Prueba de reemplazo y fallo de instancia.

**Criterios de salida:**

1. Dos instancias responden de forma independiente.
2. El balanceador retira una instancia no lista.
3. No se usan sticky sessions como solución.
4. TLS es válido y no se desactiva la verificación.
5. Las cabeceras de proxy se aceptan solo desde fuentes confiables.

**Evidencia:** topología, configuración revisada, pruebas de distribución y TLS.

### F4 — Observabilidad y contenedor

**Requisitos:** NV-03-R07, NV-03-R08.

**Tasks:** T11, T12, T13, T14.

**Entregables futuros:**

- Trazas, métricas y logs con correlación.
- Exportador, muestreo, cardinalidad y retención.
- Imagen multi-stage, mínima, no root, sin secretos y con healthcheck.
- Handoff de la imagen a NV-02 para escaneo y firma.

**Criterios de salida:**

1. Una petición se sigue entre instancias y dependencias.
2. No se registran secretos ni PII.
3. La telemetría no genera costo no aprobado.
4. La imagen cumple las restricciones y puede escanearse.
5. NV-02 recibe digest y contexto de la imagen.

**Evidencia:** configuración OTel, reporte de trazas, Dockerfile futuro y digest.

### F5 — Terraform y matriz cloud

**Requisitos:** NV-03-R09, NV-03-R10.

**Tasks:** T15, T16, T17.

**Entregables futuros:**

- Estructura Terraform con módulos, variables, versiones y etiquetas.
- Estado protegido y estrategia de plan/apply/destroy.
- Matriz AWS/Azure/Google Cloud completa.
- Evidencia de costo $0 y riesgos de cobro.
- Registro de decisión de proveedor, si corresponde.

**Criterios de salida:**

1. Terraform no contiene secretos.
2. La matriz cubre todos los criterios exigidos.
3. El costo total es $0 con evidencia vigente.
4. La selección de proveedor tiene aprobación humana separada.
5. Si no se demuestra $0, el resultado es BLOCKED.

**Evidencia:** código Terraform futuro, matriz, capturas de cuotas/precios y acta.

### F6 — Creación, verificación y destrucción

**Requisitos:** NV-03-R01 a NV-03-R07, NV-03-R11, NV-03-R12, NV-03-R13.

**Tasks:** T18, T19, T20, T21.

**Entregables futuros:**

- Inventario de recursos y plan aprobado.
- Dos instancias, Load Balancer, TLS, Redis, MongoDB y observabilidad.
- Pruebas de stateless, distribución, fallo, TLS y telemetría.
- Destrucción completa y verificación de recursos residuales.
- Atestación de costo $0.

**Criterios de salida:**

1. La creación coincide con el plan aprobado.
2. Las pruebas de escalado y fallo pasan o se reportan como brecha.
3. La destrucción deja inventario vacío o justificado.
4. El costo posterior es $0 y existe evidencia.
5. La revisión final emite PASS, PASS WITH WARNINGS o BLOCKED.

**Evidencia:** inventario, reportes de prueba, capturas de facturación, plan de destrucción y acta.

## 4. Secuencia y dependencias

```text
F1 Stateless
  ↓
F2 Redis/MongoDB
  ↓
F3 Dos instancias/LB/TLS
  ↓
F4 OTel/Docker
  ↓
F5 Terraform/matriz
  ↓
F6 Crear/verificar/destruir
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
- T14 depende de T13 y de NV-02.
- T15 depende de T08 y T13.
- T16 depende de T15.
- T17 depende de T16.
- T18 depende de T09, T10, T11, T15 y T17.
- T19 depende de T18.
- T20 depende de T18 y T19.
- T21 depende de T20.

## 5. Estrategia de verificación

| Prueba | Qué demuestra | Entorno | Evidencia |
| --- | --- | --- | --- |
| Reemplazo de instancia | Stateless y continuidad | Aislado | Logs y respuestas |
| Distribución LB | Balanceo real | Aislado | Métricas por instancia |
| Retirada por health | Tolerancia a fallo | Aislado | Estado del LB |
| TLS | Certificado y HSTS | Aislado | Análisis y cabeceras |
| Fallo Redis | Degradación controlada | Aislado | Reporte y salud |
| Fallo MongoDB | Timeouts y recuperación | Aislado | Reporte |
| Telemetría | Correlación entre instancias | Aislado | Trazas y métricas |
| Carga | SLO y comportamiento | Aislado | Reporte NBomber |
| Destrucción | Sin recursos residuales | Cuenta autorizada | Inventario y facturación |

Los SLO no están aprobados. Se proponen y calibran con evidencia; no se copian de otro repositorio.

## 6. Creación y destrucción

**Creación:**

1. Aprobar proveedor, servicios, región y costo.
2. Revisar `terraform plan` sin secretos.
3. Crear únicamente los recursos del plan.
4. Etiquetar por proyecto, entorno y propósito.
5. Verificar inventario y costo inicial.

**Destrucción:**

1. Congelar tráfico o retirar el entorno.
2. Ejecutar el plan de destrucción aprobado.
3. Verificar que no queden instancias, discos, IPs, balanceadores, certificados, logs ni registros.
4. Revisar facturación posterior.
5. Documentar cualquier recurso conservado y su justificación.

No se asume que `destroy` elimina recursos externos, backups, imágenes o servicios de terceros.

## 7. Costo — COST-001

| Componente | Requisito | Si no se demuestra |
| --- | --- | --- |
| Compute de dos instancias | $0 | BLOCKED |
| Load Balancer | $0 | BLOCKED |
| HTTPS/TLS | $0 | BLOCKED |
| Container hosting | $0 | BLOCKED |
| Redis | $0 | BLOCKED |
| MongoDB | $0 | BLOCKED |
| Observabilidad | $0 | BLOCKED |
| Storage/CDN | $0 | BLOCKED |
| Egress | $0 | BLOCKED |
| Terraform/estado | $0 | BLOCKED |
| Registro de imágenes | $0 | BLOCKED |

Un free tier, un crédito o una alerta de presupuesto no prueban costo cero. La matriz debe incluir tarjeta requerida, riesgo de cobro, duración, cuotas y región. Si el conjunto no puede ser $0, NV-03 no se aprueba.

## 8. Seguridad

- Identidades y roles con mínimo privilegio.
- Sin secretos en Terraform, imágenes, estado o logs.
- MongoDB y Redis no expuestos públicamente.
- TLS válido y verificación habilitada.
- Cabeceras de proxy confiables y validadas.
- Health checks sin credenciales ni detalles internos.
- Telemetría sin PII ni cardinalidad ilimitada.
- Imagen no root, escaneada y firmada.
- Destrucción de credenciales temporales.
- Revisión de seguridad antes de crear y después de destruir.

## 9. Puertas de revisión

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0 | Antes de empezar | Aprobación humana de los tres documentos | BLOCKED |
| G1 | Fin de F1 | Estado local inventariado y resuelto | No avanzar |
| G2 | Fin de F2 | Redis/MongoDB con fallos y costo $0 | No avanzar |
| G3 | Fin de F3 | Dos instancias, LB y TLS verificables | No avanzar |
| G4 | Fin de F4 | OTel e imagen listos | No avanzar |
| G5 | Fin de F5 | Matriz completa y costo $0 | BLOCKED |
| G6 | Fin de F6 | Creación, verificación y destrucción | NV-03 no se cierra |

## 10. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T02, T05, T19 |
| R02 | F3 | T08, T18, T19 |
| R03 | F3 | T09, T18, T19 |
| R04 | F2 | T03, T04, T05, T18, T19 |
| R05 | F2 | T06, T07, T18, T19 |
| R06 | F3 | T10, T18, T19 |
| R07 | F4 | T11, T12, T19 |
| R08 | F4 | T13, T14 |
| R09 | F5 | T15, T18, T20 |
| R10 | F5 | T16, T17 |
| R11 | F6 | T18 |
| R12 | F6 | T20 |
| R13 | F6 | T12, T17, T20, T21 |

## 11. No ejecución

Este plan no autoriza crear Dockerfile, Terraform, workflows, recursos cloud, ni seleccionar proveedor, ni desplegar, ni destruir. Estado: **⛔ NO EJECUTAR**. Siguiente paso: revisión humana de `spec.md`, `plan.md` y `task.md`.
