# NV-17 — Agenda de visitas — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
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

Se modela la máquina de estados con pruebas antes de los endpoints; la regla de dirección exacta se prueba en cada estado.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Proponer visita; Ciclo de estados |
| F2 | Persistencia e integración | R03, R05 | T03, T05 | Calendario y listas; Persistencia e índices |
| F3 | Seguridad, autorización y límites | R04 | T04 | Dirección exacta controlada |
| F4 | Pruebas y evidencia | R06 | T06, T07, T08 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Proponer visita** (R01): Solo con solicitud activa propia, horario futuro ≤ 60 días; 409 si ya hay una visita activa para la solicitud.
- **T02 · Ciclo de estados** (R02): Confirmar, reprogramar (máximo 3), aceptar reprogramación, cancelar con motivo y completar, con transiciones válidas y `history`.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Calendario y listas** (R03): Calendario del propietario por rango de hasta 62 días y lista del estudiante, paginados.
- **T05 · Persistencia e índices** (R05): Índices por propietario y estudiante con fecha; concurrencia optimista en cambios de estado.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T04 · Dirección exacta controlada** (R04): Visible solo para el estudiante con visita confirmada y dentro de la ventana; pruebas en cada estado.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

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
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: T01, T02.
- T04 depende de: T01, T02, T03, T05.
- T05 depende de: T01, T02.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T06, T07.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Proponer visita | Pruebas automatizadas en CI |
| R02 | Ciclo de estados | Pruebas automatizadas en CI |
| R03 | Calendario y listas | Pruebas automatizadas en CI |
| R04 | Dirección exacta controlada | Pruebas automatizadas en CI |
| R05 | Persistencia e índices | Pruebas automatizadas en CI |
| R06 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Participantes únicamente (BOLA).
- La dirección exacta no aparece en listados ni en notificaciones; solo en el detalle confirmado (RNF-02).
- Rate limit de propuestas 10/día por estudiante.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | — |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §12 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F2 | T03 |
| R04 | F3 | T04 |
| R05 | F2 | T05 |
| R06 | F4 | T06, T07, T08 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
