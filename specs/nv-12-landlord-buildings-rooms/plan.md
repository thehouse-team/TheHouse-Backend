# NV-12 — Arrendador, edificios y cuartos — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Se modela primero el dominio (estados y reglas) con pruebas unitarias, después la persistencia y al final la publicación, que depende de NV-13 y NV-21.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04 | T01, T02, T03, T04 | Alta de arrendador; Gestión de edificios; Generación y edición de cuartos; Máquina de estados |
| F2 | Persistencia e integración | R05, R06, R07 | T05, T06, T07 | Cálculo de distancia; Persistencia e índices; Publicación y tablero |
| F3 | Seguridad, autorización y límites | R08 | T08 | Autorización del dueño |
| F4 | Pruebas y evidencia | R09 | T09, T10, T11 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Alta de arrendador** (R01): Activa el modo arrendador una sola vez por cuenta (409 si ya existe); valida RFC si es inmobiliaria y registra la aceptación de términos.
- **T02 · Gestión de edificios** (R02): Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo y servicios; borrado bloqueado con cuartos Rentados.
- **T03 · Generación y edición de cuartos** (R03): `rooms:generate` crea cuartos numerados por piso con datos base; edición, duplicado, copia de datos, pausa y eliminación respetan los estados.
- **T04 · Máquina de estados** (R04): Transiciones válidas de edificio y cuarto implementadas en el dominio; transiciones inválidas 409; pruebas de cada transición.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T05 · Cálculo de distancia** (R05): Distancia en km y minutos a pie a la universidad elegida, recalculados al cambiar la ubicación o la universidad.
- **T06 · Persistencia e índices** (R06): Colecciones con índices únicos y geoespaciales; concurrencia optimista con `version`.
- **T07 · Publicación y tablero** (R07): `publication` lista pasos pendientes; `:publish` pasa a En revisión solo si se cumplen; Mis alojamientos devuelve totales, libres, ocupación e ingreso mensual.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T08 · Autorización del dueño** (R08): Ningún endpoint permite leer o modificar edificios o cuartos ajenos; pruebas con dos arrendadores.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T09 · Pruebas unitarias y de integración** (R09): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T10 · Pruebas de seguridad** (R09): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T11 · Evidencia y revisión final** (R09): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: autorización «EJECUTA».
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04.
- T07 depende de: T01, T02, T03, T04.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Alta de arrendador | Pruebas automatizadas en CI |
| R02 | Gestión de edificios | Pruebas automatizadas en CI |
| R03 | Generación y edición de cuartos | Pruebas automatizadas en CI |
| R04 | Máquina de estados | Pruebas automatizadas en CI |
| R05 | Cálculo de distancia | Pruebas automatizadas en CI |
| R06 | Persistencia e índices | Pruebas automatizadas en CI |
| R07 | Publicación y tablero | Pruebas automatizadas en CI |
| R08 | Autorización del dueño | Pruebas automatizadas en CI |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Todo endpoint `/landlord/*` exige perfil de arrendador y filtra por `ownerId = sub` (BOLA); un recurso ajeno responde 404.
- La dirección exacta y el teléfono nunca salen de los endpoints del dueño (RNF-02).
- Validación de RFC y teléfono; límites de tamaño en textos y listas.
- Operaciones de escritura con control de concurrencia optimista (`version`, 409 si cambió).
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Volumen estimado dentro de 512 MB |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F1 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F3 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
