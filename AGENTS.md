# TheHouse Backend — Instrucciones para agentes

## Autoridad y lectura obligatoria

Leer desde la raíz, en este orden:

1. `constitution.md` — máxima autoridad del Backend.
2. `AGENTS.md` — reglas operativas.
3. `memoria.md` — decisiones, no un diario.
4. `spec.md` de la spec activa.
5. `plan.md` activo.
6. `task.md` activo.

Las instrucciones humanas y políticas del entorno siguen siendo vinculantes. Un documento inferior, un perfil de agente, una skill o una referencia externa no pueden contradecir una norma superior ni ampliar el alcance. Ante contradicción: reportar y **BLOCKED**.

## Estado de esta entrega

Autorizado: preparar y verificar únicamente documentación SDD en los tres Markdown de raíz, `.opencode/agents/`, `.opencode/skills/` y las tres carpetas de specs solicitadas. No hay spec de implementación activa ni tasks autorizadas. NV-01 es candidata a primera revisión, no ejecución autorizada.

Antes de cualquier implementación declarar explícitamente:

| Campo | Estado actual |
| --- | --- |
| SPEC ACTIVA | NINGUNA AUTORIZADA |
| PLAN ACTIVO | NINGUNO AUTORIZADO |
| TASK ACTIVO | NINGUNO AUTORIZADO |
| ESTADO AUTORIZADO | BLOCKED para implementación; solo preparación documental permitida |

## Protocolo de autorización

1. Identificar IDs y rutas exactas de spec, plan y task; comprobar versiones y traza requisito → fase → task.
2. Comprobar aprobación humana explícita de **los tres** documentos, con aprobador, fecha, versión, IDs autorizados, rutas permitidas y restricciones.
3. Verificar precondiciones, dependencias entre specs, riesgos, pruebas y COST-001. Aprobar NV-01 no aprueba NV-02 ni NV-03.
4. Si persiste `⛔ NO EJECUTAR`, falta aprobación, se requiere escribir en rutas protegidas sin levantamiento explícito o el costo es incierto: **BLOCKED**. No cambiar estos estados por iniciativa propia.
5. Solo una revisión humana puede registrar la autorización y retirar el bloqueo del alcance correspondiente. Un plan aprobado no habilita tareas fuera de él. Un cambio material requiere reaprobar.
6. En el futuro, ejecutar únicamente la task habilitada y producir evidencia real. Marcar su finalización únicamente después de cumplir aceptación y revisión; en esta entrega todas permanecen abiertas.

## Flujo de trabajo obligatorio

Esta sección refuerza la separación entre preparación SDD y ejecución, y la verificación iterativa de coherencia y seguridad. Aplica a todos los agentes, specs y futuras implementaciones del Backend.

### Spec primero; implementación solo por autorización

La preparación documental y la ejecución son fases separadas. El agente puede crear o modificar `spec.md`, `plan.md` y `task.md`; después de preparar o actualizar esos documentos debe **DETENERSE**.

La creación o aprobación documental de una spec **no** constituye autorización para ejecutarla. Nunca interpretar `spec creada` como `implementación autorizada`.

#### Autorización obligatoria

Antes de implementar debe existir explícitamente:

| Campo | Requisito |
| --- | --- |
| SPEC ACTIVA | ID y ruta exactos |
| PLAN ACTIVO | ID, versión y ruta |
| TASK ACTIVA | ID habilitado |
| ESTADO AUTORIZADO | APROBADO explícito |

Si cualquiera falta: **BLOCKED**. Solo se permite implementación cuando `ESTADO AUTORIZADO = APROBADO`, o equivalente explícito definido por el equipo. La tabla de estado se declara antes de cualquier implementación.

#### Prohibición

Está prohibido crear una spec y ejecutarla automáticamente en el mismo paso sin autorización humana intermedia. El flujo correcto es:

```text
1. Crear/revisar spec.md
2. Crear/revisar plan.md
3. Crear/revisar task.md
4. Detenerse
5. Revisión humana
6. Autorización explícita
7. Ejecutar únicamente las tasks autorizadas
```

### No crear checklist.md

No agregar `checklist.md`: el estándar actual es `spec.md`, `plan.md` y `task.md`, y `task.md` funciona como checklist de ejecución. Si se requiere evidencia, usar `evidence.md` cuando la spec lo justifique. No inventar evidencia.

### Verificación iterativa durante la implementación

Después de cada incremento coherente de implementación y obligatoriamente antes de cerrar una task, revisar `constitution.md`, `AGENTS.md`, `memoria.md`, la spec activa, el plan activo, la task activa y el alcance autorizado.

No es necesario auditar todo después de modificar una sola línea. La revisión se realiza después de un cambio coherente, unidad lógica, subtask o incremento verificable, y siempre antes del cierre de la task.

### Verificación de seguridad

Durante cada iteración de desarrollo revisar, cuando aplique: OWASP API Security Top 10; secretos y credenciales; autenticación; autorización; autorización por recurso; validación de entrada; exposición de datos; manejo de errores; logging sensible; configuración insegura; rate limiting; mass assignment; broken object level authorization (BOLA) y broken function level authorization (BFLA). No limitar la revisión a compilación o tests.

### Autorización por recurso

Un usuario autenticado no implica automáticamente autorización sobre cualquier recurso. Toda operación sensible debe validar, cuando aplique: propiedad del recurso, rol, scope, claims y relación entre usuario y recurso. Evitar IDOR, BOLA y Broken Object Level Authorization.

### Validación de entrada

Toda entrada externa debe validarse según corresponda: body, route params, query params, headers, IDs, enums, strings, numbers, dates y files. No confiar en datos proporcionados por el cliente.

### Secretos

Nunca permitir passwords, JWT secrets, connection strings reales, API keys, tokens, private keys, certificados privados ni credenciales dentro de código, logs, commits, specs, evidence o reportes.

Si se detecta un secreto confirmado: **BLOCK**, sin imprimir el valor completo en reportes.

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

No inventar evidencia.

### Repetir validación

Cuando se aplique una corrección: corregir → volver a verificar → registrar resultado, hasta obtener PASS o PASS WITH WARNINGS sin hallazgos bloqueantes.

## Rutas protegidas y acciones prohibidas

No modificar `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, `TheHouse.UnitTests/`, `TheHouse.IntegrationTests/`, `TheHouse.SecurityTests/` o `TheHouse.slnx`. Incluye `.csproj`, código fuente, `Program.cs`, `appsettings*.json`, `Controllers/`, `Properties/` y referencias.

No recrear, mover ni renombrar proyectos; no agregar `src/` o `tests/`. No ejecutar `dotnet new`, `dotnet add`, `dotnet remove`, `dotnet sln add` o `dotnet sln remove`. No instalar paquetes ni crear código, entidades, controllers, endpoints o repositories. No implementar persistencia, Redis o autenticación. No generar Dockerfile, Terraform, workflows o recursos cloud. No ejecutar specs.

No ejecutar build, restore o tests en esta preparación documental: pueden reescribir `bin/obj` y no son necesarios para validar Markdown. Se usa el estado técnico previo como precondición y se compara inventario/SHA-256 antes y después. No declarar pruebas nuevas o cobertura que no existan.

No inicializar Git, hacer commit, push ni borrar cambios ajenos. Inspeccionar `UnitTest/` solo en lectura y reportar su contenido; no eliminarlo. Referencias externas solo mediante lectura, sin clone ni copia de código.

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

Los perfiles son subagentes de revisión en lectura, sin permisos de escritura, shell ni delegación. Su definición no ordena invocarlos automáticamente. Una futura habilitación necesita autorización humana y revisión independiente de permisos; una aprobación textual no evita las restricciones técnicas. No fijan modelo o proveedor de IA.

## Resultado de revisión

- **PASS**: cumple el alcance revisado con evidencia. No equivale a autorización humana de implementación.
- **PASS WITH WARNINGS**: cumple lo obligatorio, con advertencias no bloqueantes enumeradas, responsable y siguiente acción.
- **BLOCKED**: falta requisito, traza, aprobación, evidencia, seguridad, precondición o garantía de costo. No sustituir este resultado por warnings.

Cada informe identifica alcance, IDs evaluados, evidencia, pendientes y archivos afectados. Una revisión documental puede dar PASS y mantener todas las specs bloqueadas para ejecución.

## Convenciones documentales

Usar español, IDs estables, rutas relativas a raíz y estados explícitos. Mantener decisiones `M-BE-NNN` con Decisión, Estado, Justificación e Impacto; estados válidos: APROBADO, PROPUESTO, PENDIENTE, DESCARTADO. APROBADO como dirección arquitectónica no significa implementado ni autoriza una task.

Cada spec tiene requisitos verificables y todos mapean a fases y tasks. No crear nuevas specs funcionales sin solicitud. No completar checkboxes por haber escrito la planificación. Los umbrales de calidad y los costos futuros son propuestas hasta revisión con evidencia.

## Cierre de esta tarea documental

Verificar existencia de los 27 archivos, frontmatter de agentes/skills, trazabilidad completa, todas las tasks abiertas y bloqueos de ejecución. Comparar las huellas de los siete proyectos y solución, informar el residual UnitTest y confirmar ausencia de operaciones Git de escritura. Entregar el reporte solicitado y detenerse, sin comenzar NV-01.
