# NV-25 — Estadísticas del propietario y métricas de Smart Match — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
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

Se definen los eventos y su privacidad primero; los tableros se construyen sobre los agregados.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R03, R04 | T01, T03, T04 | Registro de eventos; Estadísticas del propietario; Recomendaciones por reglas |
| F2 | Persistencia e integración | R02, R05 | T02, T05 | Agregados diarios; Métricas de Smart Match |
| F3 | Seguridad, autorización y límites | R06 | T06 | Privacidad de la analítica |
| F4 | Pruebas y evidencia | R07 | T07, T08, T09 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Registro de eventos** (R01): Vistas deduplicadas por día y eventos de favoritos, solicitudes y recomendaciones con `actorHash` no reversible.
- **T03 · Estadísticas del propietario** (R03): Rango de hasta 90 días con vistas, favoritos, solicitudes y conversión; solo cuartos propios.
- **T04 · Recomendaciones por reglas** (R04): Reglas documentadas y probadas con casos que disparan cada sugerencia.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T02 · Agregados diarios** (R02): Tarea idempotente que produce contadores por cuarto y día; reejecución sin duplicar.
- **T05 · Métricas de Smart Match** (R05): Puntaje promedio, CTR, tasa de utilidad y descartes por versión de modelo; solo admin.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Privacidad de la analítica** (R06): Ninguna respuesta permite identificar usuarios; prueba con eventos de un mismo usuario en dos días.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Pruebas unitarias y de integración** (R07): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T08 · Pruebas de seguridad** (R07): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T09 · Evidencia y revisión final** (R07): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T02 depende de: T01, T03, T04.
- T03 depende de: autorización «EJECUTA».
- T04 depende de: autorización «EJECUTA».
- T05 depende de: T01, T03, T04.
- T06 depende de: T01, T03, T04, T02, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T07, T08.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Registro de eventos | Pruebas automatizadas en CI |
| R02 | Agregados diarios | Pruebas automatizadas en CI |
| R03 | Estadísticas del propietario | Pruebas automatizadas en CI |
| R04 | Recomendaciones por reglas | Pruebas automatizadas en CI |
| R05 | Métricas de Smart Match | Pruebas automatizadas en CI |
| R06 | Privacidad de la analítica | Pruebas automatizadas en CI |
| R07 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- El propietario solo ve estadísticas de sus cuartos (BOLA); métricas de Smart Match solo con rol admin (NV-21).
- `actorHash` con llave rotada diariamente para que no permita rastrear usuarios (RNF-11).
- Rate limit 30/min por usuario.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | TTL controla el volumen |
| Tarea diaria | $0 dentro de la concesión de Container Apps | PENDIENTE DE VERIFICAR según la opción elegida |

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
| R02 | F2 | T02 |
| R03 | F1 | T03 |
| R04 | F1 | T04 |
| R05 | F2 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07, T08, T09 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
