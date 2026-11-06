# NV-21 — Verificación, reportes y moderación — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
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

Primero roles y autorización por función (lo más crítico), después la revisión de edificios y al final reportes y moderación.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R04 | T01, T02, T04 | Roles administrativos; Revisión de edificios; Reportes |
| F2 | Persistencia e integración | R03, R05 | T03, T05 | Acceso auditado a documentos; Moderación y suspensión |
| F3 | Seguridad, autorización y límites | R06 | T06 | Bitácora inmutable |
| F4 | Pruebas y evidencia | R07 | T07, T08, T09 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Roles administrativos** (R01): Claims `reviewer`/`admin` en el JWT; endpoints `/admin/*` responden 403 sin rol; asignación solo por CLI auditada.
- **T02 · Revisión de edificios** (R02): Aprobar, pedir corrección y rechazar con motivo y transiciones válidas; efectos en cuartos; el dueño ve el estado y el motivo.
- **T04 · Reportes** (R04): Crear reportes con catálogo de motivos; sin duplicados abiertos; cola de administración por estado.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Acceso auditado a documentos** (R03): El detalle de revisión entrega enlaces temporales de 5 minutos y registra cada acceso.
- **T05 · Moderación y suspensión** (R05): Suspender publicaciones y cuentas oculta el contenido, revoca sesiones y queda auditado; reversible por admin.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Bitácora inmutable** (R06): `audit_events` solo de inserción; prueba que no existe ruta para modificarla.

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
- T02 depende de: autorización «EJECUTA».
- T03 depende de: T01, T02, T04.
- T04 depende de: autorización «EJECUTA».
- T05 depende de: T01, T02, T04.
- T06 depende de: T01, T02, T04, T03, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T07, T08.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Roles administrativos | Pruebas automatizadas en CI |
| R02 | Revisión de edificios | Pruebas automatizadas en CI |
| R03 | Acceso auditado a documentos | Pruebas automatizadas en CI |
| R04 | Reportes | Pruebas automatizadas en CI |
| R05 | Moderación y suspensión | Pruebas automatizadas en CI |
| R06 | Bitácora inmutable | Pruebas automatizadas en CI |
| R07 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Rutas `/admin/*` exigen rol; un usuario sin rol recibe 403 (OWASP API5, autorización a nivel de función).
- Los roles no se asignan por API: solo por una operación administrativa controlada (CLI o seed) con registro.
- Enlaces a documentos de 5 minutos, de un solo uso y auditados (RNF-19).
- `audit_events` no admite actualización ni borrado por la aplicación.
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
| R04 | F1 | T04 |
| R05 | F2 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07, T08, T09 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
