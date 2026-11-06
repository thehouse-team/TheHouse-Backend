# NV-15 — Favoritos y comparación — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
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

Spec pequeña: contrato e idempotencia primero, después índices y pruebas de concurrencia.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Agregar y quitar favoritos; Consultar favoritos; Comparación de cuartos |
| F2 | Persistencia e integración | R04 | T04 | Persistencia e índices |
| F4 | Pruebas y evidencia | R05 | T05, T06, T07 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Agregar y quitar favoritos** (R01): `PUT` y `DELETE` idempotentes con 204; cuarto inexistente o no publicable 404; límite de 200.
- **T02 · Consultar favoritos** (R02): Lista paginada por fecha descendente con datos públicos y `available`.
- **T03 · Comparación de cuartos** (R03): 2 a 4 ids únicos; respuesta con los mismos campos para cada cuarto; 404 si alguno no es publicable.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Persistencia e índices** (R04): Índice único evita duplicados en concurrencia; consulta del listado sin N+1.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T05 · Pruebas unitarias y de integración** (R05): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T06 · Pruebas de seguridad** (R05): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T07 · Evidencia y revisión final** (R05): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04.
- T07 depende de: T05, T06.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Agregar y quitar favoritos | Pruebas automatizadas en CI |
| R02 | Consultar favoritos | Pruebas automatizadas en CI |
| R03 | Comparación de cuartos | Pruebas automatizadas en CI |
| R04 | Persistencia e índices | Pruebas automatizadas en CI |
| R05 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Favoritos visibles solo para su dueño.
- La comparación no expone campos distintos a los del detalle público de NV-14.
- Rate limit de escritura 60/min por usuario.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Documentos pequeños |

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
| R05 | F4 | T05, T06, T07 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
