# NV-02 — DevSecOps

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Precondición | Proyectos de testing existentes (M-BE-015) |
| Relación | Depende funcionalmente de NV-01 y del artefacto de NV-03, sin autorización transitiva |

Este documento es un borrador. No autoriza instalar herramientas, crear workflows, modificar proyectos, ejecutar scanners ni desplegar. **⛔ NO EJECUTAR** prevalece hasta la aprobación humana de los tres documentos.

## 1. Contexto

Existen `TheHouse.UnitTests`, `TheHouse.IntegrationTests` y `TheHouse.SecurityTests`. Su presencia es una precondición; no demuestra cobertura funcional. La prueba unitaria actual es un placeholder y las otras suites no contienen pruebas de negocio.

NV-02 define la estrategia de pruebas y la cadena DevSecOps que una futura implementación deberá cumplir. No crea proyectos nuevos ni instala paquetes.

### Referencias metodológicas

Se consultaron únicamente los README públicos de:

- `https://github.com/edelomeza/WebAPIDevSecOps`
- `https://github.com/edelomeza/WebAPIDevSecOpsScalling`

No se clonó ni copió código. La lectura se limita a metodología y secuenciación:

| Área | Observación de referencia | Decisión para TheHouse |
| --- | --- | --- |
| SAST/SCA/SBOM | Cadena con SonarCloud, Semgrep, auditoría NuGet y CycloneDX | Se planifica con herramientas gratuitas y evidencia |
| DAST/fuzzing | OWASP ZAP y RESTler sobre OpenAPI | Se planifica en entorno aislado y autorizado |
| Testing | Suites unitarias, integración, seguridad, contrato, mutación y performance | Se adopta el enfoque, no cantidades ni umbrales ajenos |
| Redis/scaling | Estado compartido y caché distribuida | Se traslada a NV-03; no se implementa aquí |
| Observabilidad | OpenTelemetry, métricas y health | Se traslada a NV-03; NV-01 define correlación |
| Docker/CI | Imagen escaneada, firmada y pipeline con gates | Se planifica, sin crear artefactos operativos |
| Persistencia | SQL Server/EF Core e InMemory | **No adoptado**: TheHouse usa MongoDB |
| Fallback local | Caché en memoria ante fallo de Redis | **No adoptado** para decisiones de seguridad |

## 2. Objetivo

Definir requisitos verificables para que la calidad y la seguridad sean medibles, reproducibles y bloqueantes cuando corresponda, sin confundir planificación con ejecución.

## 3. Precondiciones

1. Los tres proyectos de testing existen y conservan sus referencias.
2. `constitution.md`, `AGENTS.md` y `memoria.md` están vigentes.
3. NV-01 define los fundamentos HTTP que las pruebas de integración necesitan.
4. NV-03 definirá la imagen Docker que las pruebas de contenedor necesitan.
5. La aprobación de NV-01 o NV-03 no aprueba NV-02.
6. COST-001 sigue vigente.
7. Toda ejecución futura requiere autorización de rutas, entornos y credenciales de prueba.

## 4. Alcance

1. Pruebas unitarias.
2. Pruebas de integración.
3. Pruebas de seguridad.
4. SAST.
5. SCA.
6. SBOM.
7. Mutation testing.
8. Contract testing.
9. Performance testing.
10. DAST.
11. REST fuzzing.
12. Container security.
13. Signing y procedencia.
14. Quality gates.
15. CI/CD.
16. Hallazgos, excepciones y evidencia.

## 5. Fuera de alcance

- Implementar código, endpoints, entidades o repositorios.
- Crear proyectos de test adicionales sin autorización.
- Instalar paquetes o herramientas.
- Crear workflows de GitHub Actions, Dockerfile o Terraform.
- Escanear entornos reales, producción o servicios de terceros.
- Ejecutar pruebas, scanners o despliegues en esta entrega.
- Seleccionar proveedor cloud.
- Copiar código, workflows o configuraciones de los repositorios de referencia.
- Declarar resultados, coberturas o hallazgos no ejecutados.

## 6. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-02-R01 | Pruebas unitarias de Domain/Application | Casos de negocio y límites se prueban aislados, de forma determinista, sin infraestructura real; la suite no es un placeholder y su evidencia es reproducible. | F1 | T01, T02 |
| NV-02-R02 | Pruebas de integración con composición real | `WebApplicationFactory` verifica composición, HTTP, errores y adaptadores; los datos se aíslan y limpian; no se usan servicios productivos. | F1 | T03, T04 |
| NV-02-R03 | Pruebas de seguridad | Se cubren autorización, abuso, cabeceras, validación, límites y respuestas ante fallos; los casos negativos producen el código esperado y no abren controles. | F1 | T05 |
| NV-02-R04 | SAST | Semgrep y SonarAnalyzer/SonarCloud se configuran con reglas y baseline; no hay supresiones sin justificación, responsable y vencimiento. | F2 | T06, T07 |
| NV-02-R05 | SCA | La auditoría NuGet detecta vulnerabilidades conocidas, versiones fijadas y licencias incompatibles; un hallazgo crítico o alto bloquea o se excepciona formalmente. | F2 | T08 |
| NV-02-R06 | SBOM | Se genera un SBOM CycloneDX por entrega o imagen, con componentes y versiones, almacenado y trazable al artefacto. | F2 | T09 |
| NV-02-R07 | Mutation testing | Stryker.NET se ejecuta sobre un alcance acotado; el score se mide antes de fijar un gate y los mutantes no cubiertos se documentan. | F3 | T10 |
| NV-02-R08 | Contract testing | PactNet verifica consumidores/proveedor o se declara un alcance equivalente; el contrato OpenAPI y la implementación no divergen. | F3 | T11 |
| NV-02-R09 | Performance testing | NBomber define escenario, datos, concurrencia, duración, entorno y SLO; los resultados son reproducibles y no se extrapolan a producción. | F3 | T12 |
| NV-02-R10 | DAST | OWASP ZAP se ejecuta contra un entorno aislado y autorizado, con alcance y credenciales de prueba; no se atacan sistemas de terceros. | F4 | T13 |
| NV-02-R11 | REST fuzzing | RESTler usa el OpenAPI aprobado, límites y datos de prueba; los hallazgos se triagean y no se confunde fuzzing con prueba funcional. | F4 | T14 |
| NV-02-R12 | Seguridad de contenedores | Trivy y Dockle verifican imagen, capas, usuario no root, secretos y configuración; los hallazgos críticos/altos corregibles bloquean. | F5 | T15, T16 |
| NV-02-R13 | Firma y procedencia | Cosign firma y verifica la imagen o artefacto autorizado; la verificación ocurre antes de un despliegue y la clave o identidad se protege. | F5 | T17 |
| NV-02-R14 | Quality gates | Cada gate tiene criterio, alcance, evidencia, efecto bloqueante y proceso de excepción; un control no ejecutado no se reporta como aprobado. | F6 | T18, T22, T24 |
| NV-02-R15 | CI/CD verificable | GitHub Actions usa permisos mínimos, acciones fijadas, secretos protegidos, artefactos retenidos y etapas trazables; no se crea el workflow en esta entrega. | F6 | T20, T21, T22 |
| NV-02-R16 | Hallazgos, excepciones y evidencia | Todo hallazgo tiene severidad, origen, responsable, vencimiento y estado; las excepciones son explícitas y la evidencia se conserva y reproduce. | F6 | T19, T23, T24 |

## 7. Decisiones y restricciones aplicables

- M-BE-015: conservar los tres proyectos de testing existentes.
- M-BE-016: testing ampliado propuesto, no instalado.
- M-BE-017: DevSecOps y supply chain propuestos.
- M-BE-019: Docker/Terraform/GitHub Actions aprobados como dirección futura.
- M-BE-021: referencias consultadas solo en lectura.
- COST-001: toda herramienta y ejecución debe ser $0.

Propuestas pendientes de aprobación:

| Tema | Propuesta inicial | Estado |
| --- | --- | --- |
| Cobertura | Métrica informativa, no gate único | PROPUESTO |
| Mutation score | Medir baseline antes de fijar umbral | PROPUESTO |
| Performance | SLO por escenario, calibrado con evidencia | PROPUESTO |
| SonarCloud | Solo si la licencia y el uso cumplen $0 | PENDIENTE |
| Registry de imágenes | Solo si el almacenamiento y la firma son gratuitos | PENDIENTE |
| DAST | Entorno local/aislado, nunca producción | PROPUESTO |

## 8. Seguridad

- Los secretos de CI se almacenan fuera del repositorio y se enmascaran en logs.
- Los workflows usan `permissions` mínimos y acciones fijadas por versión o digest.
- Los scanners no reciben credenciales productivas.
- DAST y fuzzing se limitan a un entorno autorizado y aislado.
- Los reportes pueden contener rutas o payloads: se revisan antes de publicarlos.
- Las imágenes se escanean antes de firmar y desplegar.
- Un fallo de un control obligatorio no se compensa con otro control.
- Los hallazgos críticos o altos requieren corrección o excepción aprobada con vencimiento.
- No se copian reglas, workflows o configuraciones de los repositorios de referencia.

## 9. Costo — COST-001

| Componente | Requisito de costo | Si no se cumple |
| --- | --- | --- |
| xUnit, WebApplicationFactory, Stryker.NET, PactNet, NBomber | Open source / uso local | BLOCKED si aparece licencia paga |
| Semgrep, SonarAnalyzer, NuGet audit | Gratuito/local | Usar alternativa gratuita o BLOCKED |
| SonarCloud | Solo plan gratuito elegible | BLOCKED si exige pago |
| CycloneDX, Trivy, Dockle | Open source | BLOCKED si el registro/almacenamiento cobra |
| Cosign y registry | Firma y almacenamiento gratuitos | BLOCKED si no hay opción $0 |
| OWASP ZAP, RESTler | Open source, ejecución local | BLOCKED si requiere servicio pago |
| GitHub Actions | Minutos gratuitos o ejecución local | BLOCKED si el uso genera cobro no aprobado |

Costo obligatorio objetivo: **$0**. Un free tier no es prueba de costo cero. No se provisiona nada en NV-02.

## 10. Testing de esta spec

NV-02 no ejecuta pruebas. Su verificación futura consiste en:

- Revisar que cada requisito tenga task y evidencia.
- Comprobar que los gates no dependan de herramientas no disponibles o pagas.
- Validar que las suites propuestas no se solapen sin propósito.
- Revisar que los entornos de DAST/fuzzing estén aislados.
- Confirmar que los resultados se puedan reproducir con comando, versión y datos declarados.

Una suite vacía, un placeholder o un scanner no ejecutado no cuenta como evidencia.

## 11. Riesgos

| ID | Riesgo | Impacto | Mitigación propuesta |
| --- | --- | --- | --- |
| NV-02-RK01 | Mutación con runtime excesivo | Pipeline inviable | Alcance acotado y ejecución programada |
| NV-02-RK02 | Performance no representativo | Decisiones falsas | Declarar entorno, datos y límites |
| NV-02-RK03 | DAST/fuzzing destructivo | Afecta datos o terceros | Entorno aislado y alcance autorizado |
| NV-02-RK04 | Falsos positivos de scanners | Ruido y bloqueos | Baseline y triage con evidencia |
| NV-02-RK05 | Incompatibilidad con .NET 10 | Herramientas no ejecutan | Validar versión antes de adoptar |
| NV-02-RK06 | Costo oculto de CI/registry | Viola COST-001 | Verificar cuotas y bloquear si hay cobro |
| NV-02-RK07 | Imagen aún no autorizada por NV-03 | Tareas de contenedor bloqueadas | Declarar dependencia, no simular evidencia |
| NV-02-RK08 | Secretos en reportes o logs | Exposición | Revisión y enmascarado antes de publicar |

## 12. Dependencias

- Proyectos de testing existentes (precondición).
- NV-01 para fundamentos HTTP verificables.
- NV-03 para la imagen Docker que necesitan las tareas de contenedor de F5. Esta dependencia es acotada: F1–F4 no requieren NV-03 y NV-03 no requiere que NV-02 esté cerrado para producir la imagen.
- Aprobación humana independiente de NV-02.
- Disponibilidad y costo $0 de cada herramienta.
- Un entorno autorizado para DAST, fuzzing y performance.

## 13. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 16 |
| Fases del plan | 6 |
| Tasks | 24 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado de ejecución | **⛔ NO EJECUTAR** |

## 14. Aprobación pendiente

La aprobación futura debe registrar aprobador, fecha, versiones de los tres documentos, IDs autorizados, rutas, entornos, herramientas permitidas, presupuesto $0 y restricciones. Sin ese registro, NV-02 permanece **BLOCKED** y **⛔ NO EJECUTAR**.
