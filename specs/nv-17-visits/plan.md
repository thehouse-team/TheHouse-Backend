# NV-17 — Visitas: solicitudes (v1) — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-17 |
| Documento | `plan.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero la traducción y el cambio del repositorio a `roomId` con las pruebas de 1.1.0 adaptadas; después respuestas de transición; luego la migración compartida con NV-15; al final la migración autorizada en Production y la prueba real.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Visitas a cuartos publicados; Respuesta con cuarto; Reglas por cuarto |
| F2 | Persistencia e integración | R04, R05 | T04, T05 | Transición desde propiedades; Migración de visitas |
| F3 | Seguridad, autorización y límites | R06 | T06 | Autorización y límites |
| F4 | Pruebas y evidencia | R07, R08 | T07, T08, T09, T10 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Visitas a cuartos publicados** (R01): `POST /visits` con `roomId` de un cuarto publicado crea la visita (201); cuarto no publicado o inexistente 404; ambos ids o ninguno 400.
- **T02 · Respuesta con cuarto** (R02): Las respuestas incluyen `room { id, buildingId, title, city }` y, en transición, `property` o `null`.
- **T03 · Reglas por cuarto** (R03): Horario, idempotencia y conflictos de 1.1.0 aplicados por cuarto, con sus pruebas.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Transición desde propiedades** (R04): `propertyId` se traduce a su cuarto y produce la misma visita que con `roomId`; misma llave con el id de propiedad o de cuarto es el mismo cuerpo.
- **T05 · Migración de visitas** (R05): `--migrate-to-rooms` convierte las visitas existentes, reporta las no mapeadas y es idempotente.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Autorización y límites** (R06): Pruebas con dos usuarios; rate limits 429; Redis caído 503; `no-store`.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Contrato para Mobile y despliegue** (R07): Contrato §6.1 entregado; migración autorizada en Production; prueba HTTP real en Azure (crear por cuarto y por propiedad, repetir, conflicto, listar, cancelar).
- **T08 · Pruebas unitarias y de integración** (R08): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T09 · Pruebas de seguridad** (R08): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T10 · Evidencia y revisión final** (R08): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Visitas a cuartos publicados | Pruebas automatizadas en CI |
| R02 | Respuesta con cuarto | Pruebas automatizadas en CI |
| R03 | Reglas por cuarto | Pruebas automatizadas en CI |
| R04 | Transición desde propiedades | Pruebas automatizadas en CI |
| R05 | Migración de visitas | Pruebas automatizadas en CI |
| R06 | Autorización y límites | Pruebas automatizadas en CI |
| R07 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- El usuario sale del `sub`; visita ajena → 404 (BOLA).
- Rate limit: 10 creaciones por día y 60 por minuto por usuario; Redis caído → 503.
- Mensaje ≤ 300 caracteres y fuera de logs; `Idempotency-Key` solo con el hash del cuerpo.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Sin colecciones nuevas |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.2.0-borrador | BLOCKED |
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
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
