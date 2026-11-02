# NV-03 — Scaling Cloud

| Campo | Valor |
| --- | --- |
| ID | NV-03 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | Bootstrap existente; NV-01 y NV-02 como dependencias funcionales |
| Relación | No autoriza NV-01 ni NV-02; su aprobación es independiente |

Este documento es un borrador. No autoriza crear recursos, seleccionar proveedor, escribir Terraform, crear Dockerfile o desplegar. **⛔ NO EJECUTAR** prevalece hasta la aprobación humana de los tres documentos.

## 1. Contexto

La arquitectura objetivo debe funcionar con al menos dos instancias de la API, un Load Balancer, estado compartido en Redis, conectividad a MongoDB, HTTPS/TLS, observabilidad con OpenTelemetry y empaquetado en Docker. La infraestructura se describirá con Terraform y deberá poder crearse y destruirse con costo $0.

Hoy no existe ninguna de esas capacidades implementada. La solución solo contiene el bootstrap y proyectos vacíos. Este documento planifica; no afirma despliegue, escalado ni gratuidad.

Tampoco existe un proveedor cloud seleccionado. AWS, Azure y Google Cloud son los únicos permitidos y la comparación sigue pendiente.

## 2. Objetivo

Definir los requisitos verificables para una arquitectura escalable, stateless y reproducible, con dos o más instancias, balanceo, TLS, datos/caché compartidos, observabilidad y ciclo de vida de infraestructura, sin violar COST-001.

## 3. Precondiciones

1. Bootstrap técnico existente y sin modificar.
2. NV-01 define la base stateless y los health checks.
3. NV-02 define el escaneo y la firma de la imagen. La dependencia es por fase: la imagen de NV-03 se entrega a NV-02 F5; NV-03 no requiere que NV-02 esté cerrado para crearla.
4. Existe una decisión aprobada de proveedor y servicios, precedida por la matriz.
5. Existe evidencia vigente de costo $0 para el conjunto completo.
6. Existe autorización de rutas para Docker, Terraform, workflows y configuración.
7. Existe un entorno aislado para pruebas de carga y fallos.
8. La aprobación de NV-01 o NV-02 no aprueba NV-03.

## 4. Alcance

1. API stateless.
2. Dos o más instancias.
3. Load Balancer.
4. Redis distribuido.
5. Conectividad MongoDB.
6. HTTPS/TLS.
7. OpenTelemetry.
8. Docker.
9. Terraform.
10. Nube y matriz AWS/Azure/Google Cloud.
11. Creación de infraestructura.
12. Destrucción de infraestructura.
13. Arquitectura de costo $0.

## 5. Fuera de alcance

- Implementar código, endpoints, entidades o repositorios.
- Crear Dockerfile, Terraform, workflows o recursos cloud en esta entrega.
- Seleccionar AWS, Azure o Google Cloud.
- Provisionar, desplegar, escalar o destruir recursos.
- Cambiar proyectos, referencias, `Program.cs` o configuración.
- Implementar JWT, Refresh Tokens, Logout, MongoDB o Redis.
- Declarar métricas, latencias, disponibilidad o costos no medidos.
- Aprobar gasto, tarjeta, créditos o free tiers.
- Usar sticky sessions como solución al estado local.

## 6. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-03-R01 | API stateless | Ningún estado necesario para la corrección depende de una instancia; el estado compartido se identifica y se externaliza; reemplazar una instancia no pierde sesión, revocación, cuota ni dato de negocio. | F1 | T01, T02, T05, T19 |
| NV-03-R02 | Dos o más instancias | La misma imagen y configuración se ejecuta en al menos dos instancias independientes; cada una reporta salud; el reemplazo no requiere afinidad de sesión. | F3 | T08, T18, T19 |
| NV-03-R03 | Load Balancer | Distribuye tráfico, usa health checks, retira instancias no listas, propaga cabeceras de proxy de forma controlada y no depende de sticky sessions. | F3 | T09, T18, T19 |
| NV-03-R04 | Redis distribuido | Redis tiene topología, claves con namespace, TTL, invalidación, autenticación/TLS cuando aplique, timeouts y comportamiento ante fallo; no es fuente de verdad. | F2 | T03, T04, T05, T18, T19 |
| NV-03-R05 | Conectividad MongoDB | La conexión usa pooling, timeouts, cancelación, reintentos justificados, credenciales protegidas, TLS y límites; los índices responden a consultas reales y el costo es $0. | F2 | T06, T07, T18, T19 |
| NV-03-R06 | HTTPS/TLS | Todo acceso público usa HTTPS con certificado válido; HSTS, terminación, renovación y confianza en cabeceras de proxy están definidos y probados; no se desactiva la validación. | F3 | T10, T18, T19 |
| NV-03-R07 | OpenTelemetry | Trazas, métricas y logs comparten correlación; se controlan cardinalidad, PII y retención; health checks no sustituyen telemetría; el exportador cumple COST-001. | F4 | T11, T12, T19 |
| NV-03-R08 | Docker | La imagen es multi-stage, mínima, no root, sin secretos, con base fijada, healthcheck y compatibilidad con dos instancias; se entrega a NV-02 para escaneo. | F4 | T13, T14 |
| NV-03-R09 | Terraform | La infraestructura se describe declarativamente, con módulos, variables, estado protegido, versiones fijadas, plan/apply/destroy y etiquetado; no contiene secretos. | F5 | T15, T18, T20 |
| NV-03-R10 | Nube y matriz AWS/Azure/Google Cloud | Existe una matriz comparativa completa con los criterios exigidos; ningún proveedor se selecciona antes de la revisión humana; la decisión registra evidencia y riesgos. | F5 | T16, T17 |
| NV-03-R11 | Creación de infraestructura | La creación sigue un plan aprobado, usa el proveedor seleccionado, incluye dos instancias, Load Balancer, TLS, Redis, conectividad MongoDB y observabilidad, y produce evidencia de inventario y costo. | F6 | T18 |
| NV-03-R12 | Destrucción de infraestructura | Existe inventario, plan de destrucción, verificación de recursos residuales y evidencia de costo posterior; no se eliminan recursos externos ni se asume limpieza automática. | F6 | T20 |
| NV-03-R13 | Arquitectura de costo $0 | El costo total del ciclo de vida es $0, incluidos compute, dos instancias, Load Balancer, TLS, almacenamiento, logs, Redis, MongoDB y egress; si no se demuestra, el resultado es BLOCKED. | F6 | T12, T17, T20, T21 |

## 7. Matriz cloud pendiente — AWS vs Azure vs Google Cloud

**Ningún proveedor está seleccionado.** La matriz se completará con evidencia vigente antes de cualquier decisión. Una celda vacía o "PENDIENTE" no es una aprobación.

| Criterio | AWS | Azure | Google Cloud |
| --- | --- | --- | --- |
| Compute | PENDIENTE | PENDIENTE | PENDIENTE |
| Dos instancias | PENDIENTE | PENDIENTE | PENDIENTE |
| Load Balancer | PENDIENTE | PENDIENTE | PENDIENTE |
| HTTPS / certificado | PENDIENTE | PENDIENTE | PENDIENTE |
| Container hosting | PENDIENTE | PENDIENTE | PENDIENTE |
| Observabilidad | PENDIENTE | PENDIENTE | PENDIENTE |
| Storage / CDN | PENDIENTE | PENDIENTE | PENDIENTE |
| Redis | PENDIENTE | PENDIENTE | PENDIENTE |
| Conectividad MongoDB | PENDIENTE | PENDIENTE | PENDIENTE |
| Terraform | PENDIENTE | PENDIENTE | PENDIENTE |
| Requiere tarjeta | PENDIENTE | PENDIENTE | PENDIENTE |
| Riesgo de cobro | PENDIENTE | PENDIENTE | PENDIENTE |
| Costo esperado total | PENDIENTE | PENDIENTE | PENDIENTE |

Reglas de la matriz:

1. Se evalúa el conjunto completo, no un servicio aislado.
2. Se registra región, cuota, duración, elegibilidad y fecha de consulta.
3. Un free tier no equivale a costo $0 permanente.
4. Se incluyen egress, logs, almacenamiento, IP pública, certificado y registro de imágenes.
5. Se registra si exige tarjeta y si el cobro es automático al exceder cuotas.
6. Si no se puede demostrar $0 con dos instancias y Load Balancer, se reporta BLOCKED.
7. La selección requiere aprobación humana separada de esta spec.

## 8. Decisiones y restricciones aplicables

- M-BE-007: MongoDB con `MongoDB.Driver`, encapsulado en Infrastructure.
- M-BE-008: Redis para caché y estado compartido que la spec requiera.
- M-BE-009: stateless, dos instancias y Load Balancer.
- M-BE-010: COST-001, costo obligatorio $0.
- M-BE-011: proveedor pendiente y matriz reservada.
- M-BE-018: OpenTelemetry y correlación.
- M-BE-019: Docker, Terraform y GitHub Actions futuros.
- COST-001: ningún recurso puede quedar fuera del cálculo.

Propuestas pendientes de aprobación:

| Tema | Propuesta inicial | Estado |
| --- | --- | --- |
| Topología | Dos instancias detrás de un Load Balancer | PROPUESTO |
| TLS | Terminación en Load Balancer con certificado gratuito | PENDIENTE |
| Observabilidad | OpenTelemetry con exportador gratuito/local | PENDIENTE |
| Redis | Servicio gratuito o contenedor autogestionado | PENDIENTE |
| MongoDB | Free tier elegible o instancia autogestionada | PENDIENTE |
| Estado Terraform | Local protegido o backend gratuito | PENDIENTE |
| Región | La más cercana con cuota y costo $0 verificados | PENDIENTE |

## 9. Seguridad

- No exponer MongoDB ni Redis a Internet sin control.
- Usar TLS para conexiones públicas y para dependencias cuando el proveedor lo permita.
- Aplicar mínimo privilegio a identidades, roles, tokens y claves.
- No almacenar secretos en Terraform, imágenes, repositorio o logs.
- Validar cabeceras de proxy antes de confiar en IP o esquema.
- No desactivar la validación de certificados.
- No usar sticky sessions para ocultar estado local.
- No usar fallbacks locales de seguridad que diverjan entre instancias.
- Revisar la exposición de health checks, métricas y paneles.
- Firmar y verificar la imagen antes de desplegar.
- Destruir credenciales y recursos temporales al finalizar.

## 10. Testing y evidencia

La verificación futura debe incluir:

- Reemplazo de una instancia sin pérdida de estado ni control de seguridad.
- Distribución de tráfico entre dos instancias y retirada por health check.
- TLS válido, HSTS y cabeceras de proxy.
- Fallo y recuperación de Redis sin abrir controles.
- Fallo y recuperación de MongoDB con timeouts controlados.
- Trazas y métricas correlacionadas entre instancias.
- Prueba de carga en entorno aislado y con SLO aprobado.
- Destrucción completa y verificación de costo posterior.

Ninguna de estas pruebas se ejecuta en esta entrega. No se declaran latencias, disponibilidad ni costos sin evidencia.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación propuesta |
| --- | --- | --- | --- |
| NV-03-RK01 | Free tier con expiración o cuota insuficiente | Cobro inesperado | Verificar elegibilidad y plan de destrucción |
| NV-03-RK02 | Dos instancias o Load Balancer no cubiertos por $0 | Arquitectura inviable | BLOCKED y revisión humana |
| NV-03-RK03 | Egress, logs o almacenamiento no contabilizados | Costo oculto | Inventario completo y monitoreo |
| NV-03-RK04 | Estado local en revocación, cuotas o sesión | Comportamiento divergente | Externalizar en Redis y probar fallos |
| NV-03-RK05 | Sticky sessions como atajo | Escalado frágil | Prohibidas como solución |
| NV-03-RK06 | Terraform con secretos o estado expuesto | Compromiso | Variables protegidas y estado seguro |
| NV-03-RK07 | Recursos residuales tras destroy | Cobro continuo | Inventario y verificación posterior |
| NV-03-RK08 | Proveedor elegido sin matriz | Decisión no justificada | Gate de matriz y aprobación |
| NV-03-RK09 | Certificado o DNS mal configurado | Interrupción o TLS inválido | Pruebas y renovación |
| NV-03-RK10 | Observabilidad con cardinalidad o retención excesiva | Costo y ruido | Límites y muestreo |

## 12. Dependencias

- Bootstrap existente (precondición).
- NV-01 para stateless, salud y correlación.
- NV-02 para escaneo, firma y gates de imagen.
- Aprobación humana independiente de NV-03.
- Matriz de proveedor completa y evidencia de costo $0.
- Autorización de rutas para Docker, Terraform, workflows y configuración.
- Entorno aislado para pruebas de fallo y carga.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 13 |
| Fases del plan | 6 |
| Tasks | 21 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado de ejecución | **⛔ NO EJECUTAR** |

## 14. Aprobación pendiente

La aprobación futura debe registrar aprobador, fecha, versiones, IDs, rutas, proveedor y servicios autorizados, presupuesto $0, evidencia de elegibilidad y plan de destrucción. Sin ese registro, NV-03 permanece **BLOCKED** y **⛔ NO EJECUTAR**.
