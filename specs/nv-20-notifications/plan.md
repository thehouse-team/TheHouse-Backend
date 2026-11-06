# NV-20 — Notificaciones — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
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

Se define el catálogo de eventos y plantillas primero; los emisores se conectan uno por uno.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Generación por eventos; Consulta y filtro; Marcar como leídas |
| F2 | Persistencia e integración | R04, R05 | T04, T05 | Contador eficiente; Retención |
| F4 | Pruebas y evidencia | R06 | T06, T07, T08 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Generación por eventos** (R01): Cada evento del catálogo crea una notificación idempotente para el destinatario correcto.
- **T02 · Consulta y filtro** (R02): Lista paginada por fecha con filtro de no leídas.
- **T03 · Marcar como leídas** (R03): Una o todas; idempotente.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Contador eficiente** (R04): Contador con índice y `ETag`; p95 < 100 ms.
- **T05 · Retención** (R05): TTL de 90 días; sin datos sensibles en el cuerpo.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T06 · Pruebas unitarias y de integración** (R06): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T07 · Pruebas de seguridad** (R06): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T08 · Evidencia y revisión final** (R06): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T06, T07.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Generación por eventos | Pruebas automatizadas en CI |
| R02 | Consulta y filtro | Pruebas automatizadas en CI |
| R03 | Marcar como leídas | Pruebas automatizadas en CI |
| R04 | Contador eficiente | Pruebas automatizadas en CI |
| R05 | Retención | Pruebas automatizadas en CI |
| R06 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Solo el dueño lee y marca sus notificaciones (BOLA).
- Sin datos sensibles en el cuerpo (RNF-02).
- El contador usa `ETag` para que el sondeo del cliente sea barato.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | TTL controla el volumen |
| FCM | $0 | Solo si se aprueba la decisión |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §12 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F4 | T06, T07, T08 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
