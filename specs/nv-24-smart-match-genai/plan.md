# NV-24 — Smart Match — explicaciones con IA generativa — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
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

Se implementa primero la plantilla determinista y el validador (funcionan sin proveedor); el proveedor de IA se integra solo si la decisión de costo se aprueba.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Explicación fundamentada; Validación anti-alucinación |
| F2 | Persistencia e integración | R03, R04 | T03, T04 | Respaldo por plantilla; Caché y costo |
| F3 | Seguridad, autorización y límites | R05 | T05 | Protección contra inyección |
| F4 | Pruebas y evidencia | R06 | T06, T07, T08 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Explicación fundamentada** (R01): Resumen, ventajas y desventajas derivados solo de los hechos de entrada; casos de prueba con hechos fijos.
- **T02 · Validación anti-alucinación** (R02): Un validador detecta números, servicios o condiciones ausentes de los hechos y cambia a plantilla; pruebas con salidas manipuladas.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Respaldo por plantilla** (R03): Con el proveedor caído o lento (> 3 s) responde la plantilla con `generatedBy=template`; el puntaje sigue disponible.
- **T04 · Caché y costo** (R04): Caché por cuarto y versiones con TTL de 24 h; límites que mantienen el uso dentro del plan gratuito.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T05 · Protección contra inyección** (R05): Pruebas con descripciones maliciosas que no llegan al prompt ni alteran la salida.

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
- T04 depende de: T01, T02.
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T06, T07.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Explicación fundamentada | Pruebas automatizadas en CI |
| R02 | Validación anti-alucinación | Pruebas automatizadas en CI |
| R03 | Respaldo por plantilla | Pruebas automatizadas en CI |
| R04 | Caché y costo | Pruebas automatizadas en CI |
| R05 | Protección contra inyección | Pruebas automatizadas en CI |
| R06 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Prompt injection: la entrada es JSON estructurado generado por el servidor; los textos libres del propietario se excluyen.
- Sin datos personales hacia el proveedor (RNF-11); credenciales del proveedor como secreto de Container Apps (M-BE-038).
- Rate limit 20/min por usuario y 10/min por IP de invitado; caché para no repetir llamadas.
- La salida se trata como texto no confiable: sin HTML ni enlaces.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Plantillas deterministas | $0 | Siempre disponibles |
| Proveedor de modelo de lenguaje | $0 solo con plan gratuito | PENDIENTE DE VERIFICAR; si no, solo plantillas (BLOCKED la parte `llm`) |

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
| R04 | F2 | T04 |
| R05 | F3 | T05 |
| R06 | F4 | T06, T07, T08 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
