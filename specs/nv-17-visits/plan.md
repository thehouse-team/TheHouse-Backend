# NV-17 — Visitas: solicitud, confirmación y calendario — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `plan.md` |
| Versión | 1.3.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-11 («apruebo y ejecuta»: aprobación de 1.3.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-17-visits/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.3.0 T01–T11 (2026-11-12): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.3.0-borrador |
| Tasks | `task.md` 1.3.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero las transiciones y la regla de dirección en el dominio con pruebas unitarias; después el calendario y las acciones del arrendador; luego la compatibilidad con 1.2.0 y NV-16; al final privacidad, límites y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04 | T01, T02, T03, T04 | Calendario del arrendador; Confirmar; Reprogramar; Cancelar por cualquiera de las partes |
| F2 | Persistencia e integración | R05, R06 | T05, T06 | Dirección exacta al confirmar; Compatibilidad y NV-16 |
| F3 | Seguridad, autorización y límites | R07 | T07 | Privacidad, autorización y límites |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Calendario del arrendador** (R01): Lista las visitas de sus cuartos por rango (≤ 62 días), estado y cuarto, ordenadas por horario; detalle con nombre y universidad del estudiante; lo ajeno 404; sin modo arrendador 403.
- **T02 · Confirmar** (R02): `requested` futura → `confirmed` con `confirmedAt` e historial; cualquier otro estado 409 `visit_state`; visita pasada 409 `visit_in_past`; cuarto no publicado 409.
- **T03 · Reprogramar** (R03): Nuevo horario válido con las reglas de 1.1.0, `previousScheduledAt` guardado, estado `confirmed`; 422 horario inválido; 409 `student_schedule_overlap`.
- **T04 · Cancelar por cualquiera de las partes** (R04): Arrendador con motivo del catálogo (nota obligatoria con `other`); estudiante también sobre `confirmed`; `cancellation.by` correcto; visita pasada 409.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T05 · Dirección exacta al confirmar** (R05): La visita del estudiante incluye `address` y `location` solo si está `confirmed`; en `requested` y `cancelled` son `null`; nunca en vistas del arrendador ni públicas.
- **T06 · Compatibilidad y NV-16** (R06): Las respuestas de 1.2.0 conservan todos sus campos; el filtro `status` acepta `confirmed`; la solicitud de NV-16 muestra la visita activa `requested` o `confirmed`.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Privacidad, autorización y límites** (R07): Pruebas con estudiante, arrendador y un tercero; sin datos de contacto; rate limits 429; Redis caído 503.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitar, calendario, confirmar con dirección, reprogramar, cancelar por ambas partes).
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
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Calendario del arrendador | Pruebas automatizadas en CI |
| R02 | Confirmar | Pruebas automatizadas en CI |
| R03 | Reprogramar | Pruebas automatizadas en CI |
| R04 | Cancelar por cualquiera de las partes | Pruebas automatizadas en CI |
| R05 | Dirección exacta al confirmar | Pruebas automatizadas en CI |
| R06 | Compatibilidad y NV-16 | Pruebas automatizadas en CI |
| R07 | Privacidad, autorización y límites | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: el estudiante solo ve sus visitas y el arrendador solo las de sus cuartos; lo ajeno responde 404.
- La dirección exacta nunca aparece en listados públicos ni en visitas no confirmadas; prueba que lo verifica.
- El arrendador ve el nombre del perfil y la universidad del estudiante, nunca su correo, teléfono ni id.
- Rate limit 60/min por usuario en el calendario y las acciones; Redis caído → 503.
- Notas y mensajes fuera de logs; entradas con campos desconocidos → 400.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Sin colecciones nuevas; un índice más en `visits` |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.3.0-borrador | BLOCKED |
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
| R07 | F3 | T07 |
| R08 | F4 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.3.0 T01–T11 (2026-11-12): PASS WITH WARNINGS, ver `evidence.md`**.
