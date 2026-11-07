# NV-17 — Visitas: solicitudes (v1) — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio (estados, horarios y zona horaria) con pruebas unitarias; después la persistencia con idempotencia y control de concurrencia; al final límites, pruebas de dos usuarios y la prueba real en Azure antes de entregar el contrato a Mobile.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04, R05 | T01, T02, T03, T04, T05 | Solicitar visita; Reglas de horario y zona; Estados y transiciones; Consultar y listar; Cancelar |
| F2 | Persistencia e integración | R06, R07 | T06, T07 | Idempotencia; Conflictos y concurrencia |
| F3 | Seguridad, autorización y límites | R08 | T08 | Autorización y límites |
| F4 | Pruebas y evidencia | R09, R10 | T09, T10, T11, T12 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Solicitar visita** (R01): `POST /visits` con propiedad existente y horario válido crea una visita `requested` (201); propiedad inexistente 404; formato inválido o fecha sin desplazamiento 400.
- **T02 · Reglas de horario y zona** (R02): Rechaza con 422 horarios a menos de 2 h, a más de 60 días, fuera de 08:00–20:00 en `America/Mexico_City` o no múltiplos de 15 min; respuestas en UTC con `timeZone`.
- **T03 · Estados y transiciones** (R03): Máquina de estados con `requested` → `cancelled`; estados futuros reservados; transiciones inválidas 409; `history` registrado.
- **T04 · Consultar y listar** (R04): `GET /visits/{id}` (solo propias, ajena 404) y `GET /users/me/visits` paginado, filtrable por estado, orden `scheduledAt` asc.
- **T05 · Cancelar** (R05): `POST /visits/{id}/cancel` cancela una visita `requested` futura (200); repetir responde 200; visita pasada 409.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T06 · Idempotencia** (R06): Misma `Idempotency-Key` y mismo cuerpo dentro de 24 h devuelven la visita original (200, `Idempotent-Replayed`); cuerpo distinto 409; sin llave 400; prueba de reintentos y doble toque.
- **T07 · Conflictos y concurrencia** (R07): Visita `requested` duplicada para la misma propiedad, otra visita a menos de 60 min o más de 5 activas → 409; pruebas concurrentes con el lock por usuario y el índice único parcial.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T08 · Autorización y límites** (R08): Sin `userId` del cliente; pruebas con dos usuarios; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T09 · Contrato para Mobile y despliegue** (R09): Contrato de la §6.1 entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (crear, repetir con la misma llave, conflicto, listar, consultar, cancelar).
- **T10 · Pruebas unitarias y de integración** (R10): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T11 · Pruebas de seguridad** (R10): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T12 · Evidencia y revisión final** (R10): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T05 depende de: autorización «EJECUTA».
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- T11 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- T12 depende de: T10, T11.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Solicitar visita | Pruebas automatizadas en CI |
| R02 | Reglas de horario y zona | Pruebas automatizadas en CI |
| R03 | Estados y transiciones | Pruebas automatizadas en CI |
| R04 | Consultar y listar | Pruebas automatizadas en CI |
| R05 | Cancelar | Pruebas automatizadas en CI |
| R06 | Idempotencia | Pruebas automatizadas en CI |
| R07 | Conflictos y concurrencia | Pruebas automatizadas en CI |
| R08 | Autorización y límites | Pruebas automatizadas en CI |
| R09 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R10 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- El usuario sale del `sub`; una visita ajena responde 404 (BOLA).
- Rate limit en Redis: 10 creaciones por día y 60 solicitudes por minuto por usuario; Redis caído → 503.
- El mensaje del estudiante se limita a 300 caracteres y no se registra en logs.
- `Idempotency-Key` se guarda solo con el hash del cuerpo, nunca el cuerpo completo.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Documentos pequeños con TTL; un lock y un contador Redis por creación |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | BLOCKED |
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
| R05 | F1 | T05 |
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F3 | T08 |
| R09 | F4 | T09 |
| R10 | F4 | T10, T11, T12 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
