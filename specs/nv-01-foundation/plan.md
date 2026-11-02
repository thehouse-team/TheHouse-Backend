# NV-01 — Plan de ejecución

| Campo | Valor |
| --- | --- |
| ID | NV-01 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-10-30 |
| Estado | **⛔ NO EJECUTAR** |
| Aprobación humana | **NINGUNA** |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |

Este plan describe una ejecución futura. No se ejecuta ahora, no se marca ninguna task y no se modifica ningún proyecto. Cualquier inicio requiere aprobación humana de los tres documentos y autorización explícita de rutas.

## 1. Enfoque

La ejecución futura se organiza en seis fases incrementales. Cada fase produce un artefacto revisable y una evidencia mínima antes de habilitar la siguiente. La secuencia evita implementar contrato, middleware u operación antes de tener composición y configuración estables.

Principios:

1. No reabrir el bootstrap ni cambiar referencias.
2. Un requisito debe tener al menos una task; una task debe declarar su requisito.
3. Los valores concretos no aprobados se registran como propuestas y se revisan antes de codificar.
4. Las pruebas se diseñan junto con el requisito, no después.
5. Todo componente debe cumplir COST-001.
6. Un fallo de una puerta detiene la fase; no se compensa con tareas posteriores.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Composición y configuración | R01, R02 | T01, T02 | Composition root y opciones validables |
| F2 | Errores, validación y excepciones | R03, R04, R12 | T03, T04, T12 | Contrato de error y validación coherente |
| F3 | Contrato y evolución | R05, R06 | T05, T06 | Versión y OpenAPI verificables |
| F4 | Operación y protección | R07, R08, R09, R10 | T07, T08, T09, T10 | Salud, correlación, compresión y límites |
| F5 | Salida y límites modulares | R11, R13 | T11, T13 | Paginación acotada y fronteras comprobables |
| F6 | Verificación y evidencia | R01–R13 | T14, T15, T16, T17, T18 | Evidencia integrada y revisión final |

## 3. Detalle de fases

### F1 — Composición y configuración

**Requisitos:** NV-01-R01, NV-01-R02.

**Tasks:** T01, T02.

**Entregables futuros:**

- Mapa de registros de DI por capa y módulo, con lifetimes justificados.
- Composition root identificado en el host, sin service locator.
- Esquema de opciones tipadas y reglas de validación al inicio.
- Matriz de configuración por entorno con placeholders, sin secretos.
- Convención para detectar registros faltantes o duplicados.

**Criterios de salida:**

1. La validación de dependencias falla al inicio ante una configuración incompleta.
2. Ningún controller depende directamente de Infrastructure.
3. No hay secretos en archivos versionados.
4. La estrategia de configuración está documentada y es revisable.

**Evidencia:** diff autorizado, prueba de arranque con configuración inválida, inventario de opciones y revisión de seguridad.

### F2 — Errores, validación y excepciones

**Requisitos:** NV-01-R03, NV-01-R04, NV-01-R12.

**Tasks:** T03, T04, T12.

**Entregables futuros:**

- Catálogo de errores con tipo, código HTTP, título y extensión de correlación.
- Fábrica o mecanismo único de Problem Details.
- Reglas de validación de entrada y mapeo a 400.
- Matriz de excepciones conocidas, desconocidas, cancelación y timeout.
- Registro estructurado sin datos sensibles.

**Criterios de salida:**

1. Todo error esperado tiene un contrato Problem Details.
2. Una excepción desconocida no revela stack trace ni rutas internas.
3. La validación no vive en controllers como lógica de negocio.
4. Cancelación y timeout tienen comportamiento definido y probado.

**Evidencia:** pruebas unitarias de validadores, pruebas de integración de errores, revisión de fuga de información.

### F3 — Contrato y evolución

**Requisitos:** NV-01-R05, NV-01-R06.

**Tasks:** T05, T06.

**Entregables futuros:**

- Registro de decisión sobre versionado, con propuesta inicial `/api/v1`.
- Política de cambios incompatibles, deprecación y compatibilidad.
- Documento OpenAPI reproducible con esquemas, errores, seguridad y ejemplos.
- Comprobación de que el contrato coincide con las rutas y respuestas.

**Criterios de salida:**

1. La versión del contrato y la ruta son coherentes.
2. OpenAPI no contiene secretos, datos reales ni endpoints inventados.
3. Existe una prueba o verificación de contrato planificada.
4. La estrategia de evolución está aprobada antes de implementar.

**Evidencia:** decisión registrada, documento OpenAPI generado, lint o comparación de contrato, revisión de ejemplos.

### F4 — Operación y protección

**Requisitos:** NV-01-R07, NV-01-R08, NV-01-R09, NV-01-R10.

**Tasks:** T07, T08, T09, T10.

**Entregables futuros:**

- Health checks de liveness y readiness con timeouts y códigos definidos.
- Middleware de correlation ID con validación, propagación y eco.
- Política de compresión por algoritmo, tipo MIME y umbral.
- Políticas de rate limiting por endpoint, identidad e IP, con 429 y `Retry-After`.
- Exclusión explícita de respuestas sensibles y health checks.

**Criterios de salida:**

1. Liveness no falla por una dependencia externa.
2. Readiness refleja solo dependencias necesarias.
3. Un correlation ID inválido no se propaga sin control.
4. La compresión no se aplica a tokens o secretos.
5. El alcance local del rate limiting queda documentado; la cuota global se difiere a NV-03.

**Evidencia:** pruebas de integración de salud, correlación, compresión y límites; revisión de cabeceras y configuración.

### F5 — Salida y límites modulares

**Requisitos:** NV-01-R11, NV-01-R13.

**Tasks:** T11, T13.

**Entregables futuros:**

- Contrato de paginación con orden estable, tamaño máximo y metadatos.
- Reglas de módulo: contratos públicos, internals y prohibición de ciclos.
- Pruebas de arquitectura para dependencias y fronteras.
- Documentación de límites y deuda aceptada.

**Criterios de salida:**

1. Ninguna colección queda sin máximo o criterio de continuación.
2. Un módulo no referencia internals de otro.
3. Las reglas de dependencia son comprobables automáticamente.
4. No se crean módulos funcionales de negocio.

**Evidencia:** pruebas de arquitectura, revisión de consultas y contrato de paginación.

### F6 — Verificación y evidencia

**Requisitos:** NV-01-R01 a NV-01-R13.

**Tasks:** T14, T15, T16, T17, T18.

**Entregables futuros:**

- Pruebas de contrato para Problem Details, versionado y OpenAPI.
- Pruebas de integración para salud, correlación, compresión y rate limiting.
- Pruebas negativas de validación y paginación.
- Revisión de seguridad de configuración, errores, cabeceras y límites.
- Paquete de evidencia por requisito, sin marcar tasks.

**Criterios de salida:**

1. Cada requisito tiene evidencia identificable o una brecha declarada.
2. No hay tasks marcadas como completadas por haber escrito documentación.
3. La revisión final emite PASS, PASS WITH WARNINGS o BLOCKED.
4. Un BLOCKED impide cerrar NV-01.

**Evidencia:** reportes de pruebas, revisión de seguridad, matriz de cobertura y acta de revisión.

## 4. Secuencia y dependencias

```text
F1 Composición/configuración
  ↓
F2 Errores/validación/excepciones
  ↓
F3 Contrato/versión/OpenAPI
  ↓
F4 Salud/correlación/compresión/rate limiting
  ↓
F5 Paginación/límites modulares
  ↓
F6 Verificación/evidencia
```

Dependencias internas:

- T02 depende de T01.
- T04 y T12 dependen de T03.
- T06 depende de T05.
- T07 depende de T01 y T02.
- T10 depende de T01 y T08.
- T14 depende de T03, T05 y T06.
- T15 depende de T07, T08, T09 y T10.
- T16 depende de T04 y T11.
- T17 depende de T02, T03, T08, T10 y T12.
- T18 depende de T01 a T17.

Una dependencia no autoriza la ejecución de la task de la que se depende. Cada task requiere su propia autorización dentro del alcance aprobado.

## 5. Estrategia de pruebas

| Nivel | Alcance | Herramienta prevista | Evidencia |
| --- | --- | --- | --- |
| Unitaria | Opciones, validadores, reglas de módulo | xUnit | Reporte de ejecución |
| Integración | Errores, correlación, compresión, rate limiting, salud | WebApplicationFactory | Reporte y respuestas |
| Contrato | Problem Details, versión, OpenAPI | Verificación de contrato | Documento y diff |
| Arquitectura | Dependencias y fronteras | Pruebas de arquitectura | Reporte |
| Seguridad | Fugas, cabeceras, límites, entradas | Pruebas negativas | Hallazgos y evidencia |

Los umbrales numéricos no están aprobados. Se medirán antes de convertirlos en gates. Una suite vacía no cuenta como evidencia.

## 6. Seguridad y DevSecOps

- Revisar configuración y opciones antes de ejecutar.
- No imprimir valores sensibles en logs de arranque.
- Validar cabeceras de proxy y confianza antes de usarlas.
- Revisar que los errores no filtren detalles internos.
- Revisar que OpenAPI no exponga información sensible.
- Revisar que el rate limiting no pueda eludirse trivialmente por IP falsificada.
- Revisar que la compresión no introduzca riesgos en respuestas autenticadas.
- Preparar el handoff de SAST/SCA a NV-02; no ejecutar scanners aquí.

## 7. Costo

| Concepto | Costo esperado | Evidencia requerida |
| --- | --- | --- |
| Componentes de NV-01 | $0 | Diseño local, sin servicios pagos |
| Herramientas de prueba | $0 | Licencia/versión gratuita verificada |
| CI futuro | $0 o BLOCKED | Minutos gratuitos o ejecución local |
| Cloud | No aplica | NV-03 lo evalúa por separado |

Si aparece un costo incierto, la fase se detiene y se reporta BLOCKED. No se sustituye $0 por un free tier no verificado.

## 8. Puertas de revisión

| Puerta | Momento | Criterio | Resultado si falla |
| --- | --- | --- | --- |
| G0 | Antes de empezar | Aprobación humana de spec, plan y task | BLOCKED |
| G1 | Fin de F1 | DI y configuración validables | No avanzar a F2 |
| G2 | Fin de F2 | Errores y validación coherentes | No avanzar a F3 |
| G3 | Fin de F3 | Versión y OpenAPI aprobados | No avanzar a F4 |
| G4 | Fin de F4 | Salud, correlación, compresión y límites verificables | No avanzar a F5 |
| G5 | Fin de F5 | Paginación y fronteras comprobables | No avanzar a F6 |
| G6 | Fin de F6 | Evidencia completa y revisión emitida | NV-01 no se cierra |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01, T18 |
| R02 | F1 | T02, T17, T18 |
| R03 | F2 | T03, T14, T17, T18 |
| R04 | F2 | T04, T16, T18 |
| R05 | F3 | T05, T14, T18 |
| R06 | F3 | T06, T14, T18 |
| R07 | F4 | T07, T15, T18 |
| R08 | F4 | T08, T15, T17, T18 |
| R09 | F4 | T09, T15, T18 |
| R10 | F4 | T10, T15, T17, T18 |
| R11 | F5 | T11, T16, T18 |
| R12 | F2 | T12, T17, T18 |
| R13 | F5 | T13, T18 |

## 10. No ejecución

Este plan no autoriza:

- Crear o modificar código, proyectos, referencias o configuración.
- Instalar paquetes o herramientas.
- Ejecutar build, restore, tests, scanners o despliegues.
- Marcar tasks como completadas.
- Iniciar NV-02 o NV-03.
- Seleccionar proveedor cloud.

Estado: **⛔ NO EJECUTAR**. Siguiente paso: revisión humana de `spec.md`, `plan.md` y `task.md`.
