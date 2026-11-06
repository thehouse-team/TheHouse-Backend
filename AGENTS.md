# TheHouse Backend — Instrucciones para agentes

| Campo | Valor |
| --- | --- |
| Versión | 1.1.0 |
| Fecha | 2026-11-06 |
| Estado | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** (mientras no se apruebe, rige la versión anterior del historial de Git) |
| Decisión | M-BE-057 |

**Cambios 1.1.0:** sustituye el estado de la "entrega documental" inicial por el estado vigente, incorpora el protocolo de autorizaciones en uso («EJECUTA», «OK PARA APPLY», aprobación de despliegues y seeds), el flujo de Git con pull requests y las reglas de uso eficiente de recursos. Las reglas de seguridad, verificación iterativa y separación SDD/ejecución se conservan sin cambios de fondo.

## Autoridad y lectura obligatoria

Leer desde la raíz, en este orden:

1. `constitution.md` — máxima autoridad del Backend.
2. `AGENTS.md` — reglas operativas.
3. `memoria.md` — decisiones, no un diario.
4. `specs/README.md` — mapa de specs, estados y cobertura de requerimientos.
5. `spec.md` de la spec activa.
6. `plan.md` activo.
7. `task.md` activo.

Las instrucciones humanas y políticas del entorno siguen siendo vinculantes. Un documento inferior, un perfil de agente, una skill o una referencia externa no pueden contradecir una norma superior ni ampliar el alcance. Ante contradicción: reportar y **BLOCKED**.

## Estado vigente

El estado de cada spec está en su `task.md` y en `specs/README.md`; este archivo no lo repite para no desfasarse. Resumen al 2026-11-06:

- NV-01, NV-04 a NV-08: ejecutadas y desplegadas; NV-02 (CI) completada; NV-09 (CD) operativa; NV-03 con ejecución parcial.
- NV-10 a NV-25: borradores pendientes de aprobación documental, sin autorización de ejecución.

Antes de cualquier implementación declarar explícitamente:

| Campo | Requisito |
| --- | --- |
| SPEC ACTIVA | ID y ruta exactos |
| PLAN ACTIVO | ID, versión y ruta |
| TASK ACTIVA | IDs habilitados por «EJECUTA» |
| ESTADO AUTORIZADO | APROBADO explícito, con aprobador y fecha |

Si cualquiera falta: **BLOCKED**.

## Protocolo de autorización

1. Identificar IDs y rutas exactas de spec, plan y task; comprobar versiones y traza requisito → fase → task.
2. Comprobar la aprobación humana explícita de **los tres** documentos y la autorización «EJECUTA» con aprobador, fecha, versión, tasks, rutas permitidas y restricciones.
3. Verificar precondiciones, dependencias entre specs, decisiones PENDIENTE que bloqueen tasks, riesgos, pruebas y COST-001. Aprobar una spec no aprueba otra.
4. Si persiste `⛔ NO EJECUTAR`, falta aprobación, se requiere escribir en rutas protegidas no incluidas en la autorización o el costo es incierto: **BLOCKED**. No cambiar estos estados por iniciativa propia.
5. Solo una revisión humana puede registrar la autorización y retirar el bloqueo del alcance correspondiente. Un plan aprobado no habilita tareas fuera de él. Un cambio material requiere reaprobar.
6. Ejecutar únicamente las tasks habilitadas y producir evidencia real. Marcar su finalización solo después de cumplir aceptación y revisión.

Autorizaciones independientes (constitution §34):

| Acción | Requiere |
| --- | --- |
| Implementar tasks | «EJECUTA» con spec, versión, tasks y rutas |
| `terraform apply` | Plan mostrado al humano y «OK PARA APPLY»; un plan nuevo o distinto requiere otra confirmación |
| `terraform destroy` | Autorización aparte con inventario |
| Desplegar a producción | Aprobación humana en el Environment `production` (nunca la otorga un agente) |
| Seed en Production | Autorización explícita para esa ejecución (M-BE-043) |
| Cambios en cuentas o servicios externos (suscripción, registro de proveedores, GitHub, roles) | Permiso explícito para esa acción concreta |
| Merge de un pull request | Aprobación de otra persona en GitHub; el agente no mezcla por iniciativa propia |

## Flujo de trabajo obligatorio

Esta sección refuerza la separación entre preparación SDD y ejecución, y la verificación iterativa de coherencia y seguridad. Aplica a todos los agentes, specs y futuras implementaciones del Backend.

### Spec primero; implementación solo por autorización

La preparación documental y la ejecución son fases separadas. El agente puede crear o modificar `spec.md`, `plan.md` y `task.md` cuando un humano lo solicita; después de prepararlos o actualizarlos debe **DETENERSE**.

La creación o aprobación documental de una spec **no** constituye autorización para ejecutarla. Nunca interpretar `spec creada` como `implementación autorizada`.

Está prohibido crear una spec y ejecutarla en el mismo paso sin autorización humana intermedia. El flujo correcto es:

```text
1. Crear/revisar spec.md
2. Crear/revisar plan.md
3. Crear/revisar task.md
4. Detenerse
5. Revisión humana
6. Autorización explícita («EJECUTA»)
7. Ejecutar únicamente las tasks autorizadas
8. Registrar evidence.md y cerrar tasks con evidencia
```

### No crear checklist.md

No agregar `checklist.md`: el estándar es `spec.md`, `plan.md` y `task.md`, y `task.md` funciona como checklist de ejecución. La evidencia de lo ejecutado va en `evidence.md`. No inventar evidencia.

### Coherencia documental

- Los tres documentos de una spec muestran el mismo estado; el conteo de tasks completadas coincide con las casillas marcadas.
- Trazabilidad bidireccional: cada requisito lista sus tasks y cada task cita su requisito.
- Cada spec cita los RF/RNF de origen de `Requerimientos_TheHouse_v2`.
- Al crear una spec o cambiar su estado, actualizar `specs/README.md`.
- Las `evidence.md` son registros fechados: se agregan secciones, no se reescribe lo ya registrado.

### Verificación iterativa durante la implementación

Después de cada incremento coherente de implementación y obligatoriamente antes de cerrar una task, revisar `constitution.md`, `AGENTS.md`, `memoria.md`, la spec activa, el plan activo, la task activa y el alcance autorizado. Si un documento inferior contradice a uno superior, corregir el inferior o reportar el conflicto.

No es necesario auditar todo después de modificar una sola línea. La revisión se realiza después de un cambio coherente, unidad lógica, subtask o incremento verificable, y siempre antes del cierre de la task.

### Verificación de seguridad

Durante cada iteración de desarrollo revisar, cuando aplique: OWASP API Security Top 10; secretos y credenciales; autenticación; autorización; autorización por recurso; validación de entrada; exposición de datos; manejo de errores; logging sensible; configuración insegura; rate limiting; mass assignment; broken object level authorization (BOLA) y broken function level authorization (BFLA). No limitar la revisión a compilación o tests.

### Autorización por recurso

Un usuario autenticado no implica automáticamente autorización sobre cualquier recurso. Toda operación sensible debe validar, cuando aplique: propiedad del recurso, rol, scope, claims y relación entre usuario y recurso. Evitar IDOR, BOLA y Broken Object Level Authorization.

### Validación de entrada

Toda entrada externa debe validarse según corresponda: body, route params, query params, headers, IDs, enums, strings, numbers, dates y files. No confiar en datos proporcionados por el cliente.

### Secretos

Nunca permitir passwords, JWT secrets, connection strings reales, API keys, tokens, private keys, certificados privados ni credenciales dentro de código, logs, commits, specs, evidence o reportes. No leer ni mostrar `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`.

Si se detecta un secreto confirmado: **BLOCK**, sin imprimir el valor completo en reportes, y recomendar su rotación.

### Exposición de datos

Revisar que respuestas, logs y errores no expongan stack traces, IDs internos innecesarios, datos personales innecesarios, tokens, credenciales, connection strings ni detalles internos de infraestructura.

### Clasificación de hallazgos

Todo hallazgo se clasifica como: Critical, High, Medium, Low, Informational, False Positive o Accepted Risk.

### Política de cierre de task

No cerrar una task si existen dentro de su alcance: contradicciones SDD, Critical confirmado, High confirmado sin excepción humana, secretos expuestos, fallos de autorización o validaciones obligatorias ausentes.

### Hallazgos no resolubles

Si un hallazgo no puede resolverse dentro de la task actual, no ocultarlo ni marcar la task como completada silenciosamente. Debe quedar como **BLOCKED** o **REQUIERE DECISIÓN HUMANA**, registrando hallazgo, severidad, impacto, evidencia, motivo del bloqueo y opciones de solución.

### Hallazgos Medium / Low

Los hallazgos Medium o Low pueden no bloquear automáticamente, pero deben documentarse, evaluarse, tener decisión y tener remediación futura cuando aplique. Nunca ignorarse silenciosamente.

### Reportar lo encontrado y corregido

Antes de cerrar cada task o fase relevante, registrar de forma concisa:

```text
Hallazgos encontrados:
...

Correcciones aplicadas:
...

Hallazgos pendientes:
...

Resultado de verificación SDD:
PASS / PASS WITH WARNINGS / BLOCKED

Resultado de seguridad:
PASS / PASS WITH WARNINGS / BLOCKED
```

### Repetir validación

Cuando se aplique una corrección: corregir → volver a verificar → registrar resultado, hasta obtener PASS o PASS WITH WARNINGS sin hallazgos bloqueantes. Máximo dos ciclos de corrección por fase antes de reportar al humano.

### Uso eficiente de recursos

- Leer cada documento de gobierno una vez por sesión y después hacer lecturas puntuales.
- Ediciones acotadas en lugar de reescribir archivos completos.
- Build y pruebas una vez por fase (no después de cada línea); en tareas solo documentales, no ejecutar build, restore ni tests.
- Reportes breves con evidencia verificable.

## Rutas protegidas y acciones prohibidas

No modificar `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/` o `TheHouse.slnx` (incluye `.csproj`, código fuente, `Program.cs`, `appsettings*.json`, `Controllers/`, `Properties/` y referencias) salvo que «EJECUTA» incluya esas rutas.

Sin autorización que lo nombre: no recrear, mover ni renombrar proyectos; no agregar `src/` o `tests/`; no ejecutar `dotnet new`, `dotnet add`, `dotnet remove`, `dotnet sln add` o `dotnet sln remove`; no instalar paquetes; no cambiar workflows, Dockerfile, Terraform, configuración de GitHub ni recursos cloud; no ejecutar specs.

Git: commit y push solo cuando un humano lo pide, en ramas de trabajo, nunca directo a `main`; sin force push ni reescritura de historial publicado; los cambios llegan a `main` por pull request con checks y aprobación (constitution §33). Inspeccionar `UnitTest/` solo en lectura; no eliminarlo. Referencias externas solo mediante lectura, sin clone ni copia de código.

## Agentes especializados

| Perfil | Responsabilidad | Skills relacionadas |
| --- | --- | --- |
| `sdd-guardian` | Constitución, autorización, trazabilidad y scope | `sdd-compliance` |
| `backend-architect` | .NET, capas, módulos, DI, configuración y resiliencia | `dotnet-architecture` |
| `api-contract-agent` | REST, OpenAPI, versión y semántica HTTP | `api-contract` |
| `security-devsecops-agent` | Amenazas, secretos y cadena de seguridad | `devsecops` |
| `testing-quality-agent` | Estrategia, suites y calidad de evidencia | `testing` |
| `cloud-devops-agent` | Contenedores, IaC, escalado, TLS y costo | `cloud-zero-cost` |
| `database-cache-agent` | NoSQL, MongoDB y caché distribuida | `mongodb`, `redis-distributed-cache` |

Los perfiles de `.opencode/agents/` son subagentes de revisión en lectura, sin permisos de escritura, shell ni delegación. Su definición no ordena invocarlos automáticamente. Una futura habilitación necesita autorización humana y revisión independiente de permisos. No fijan modelo o proveedor de IA.

## Resultado de revisión

- **PASS**: cumple el alcance revisado con evidencia. No equivale a autorización humana de implementación.
- **PASS WITH WARNINGS**: cumple lo obligatorio, con advertencias no bloqueantes enumeradas, responsable y siguiente acción.
- **BLOCKED**: falta requisito, traza, aprobación, evidencia, seguridad, precondición o garantía de costo. No sustituir este resultado por warnings.

Cada informe identifica alcance, IDs evaluados, evidencia, pendientes y archivos afectados. Una revisión documental puede dar PASS y mantener specs bloqueadas para ejecución.

## Convenciones documentales

Usar español, IDs estables, rutas relativas a raíz y estados explícitos. Mantener decisiones `M-BE-NNN` con ID, Decisión, Estado, Fecha, Aprobador, Justificación e Impacto; estados válidos: APROBADO, PROPUESTO, PENDIENTE, DESCARTADO. APROBADO como dirección arquitectónica no significa implementado ni autoriza una task.

- Toda decisión técnica nueva se registra en `memoria.md`.
- Todos los archivos se guardan en UTF-8 con finales de línea LF (M-BE-045).
- Cada spec tiene requisitos verificables y todos mapean a fases y tasks.
- No crear nuevas specs funcionales sin solicitud humana.
- No completar checkboxes por haber escrito la planificación.
- Los umbrales de calidad y los costos futuros son propuestas hasta revisión con evidencia.

## Cierre de una tarea

Al terminar cualquier tarea: verificar trazabilidad y estados de las specs afectadas, actualizar `specs/README.md` y `memoria.md` cuando corresponda, confirmar que no se expusieron secretos, entregar el reporte de hallazgos y detenerse. No continuar con la siguiente spec sin solicitud humana.
