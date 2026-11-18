# NV-10 — Sesión: refresh token y cuenta propia — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-10 |
| Documento | `plan.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T09; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-10-account-session/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.2.0 T01–T09 (2026-11-18): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero la sesión sin tope y el registro legal (cambios pequeños con pruebas unitarias); después la vista previa y la eliminación módulo por módulo, cada uno con su prueba de integración; luego seguridad y límites; al final la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R03, R04 | T01, T03, T04 | Vista previa y bloqueos; Sesión siempre viva; Aviso de privacidad y términos |
| F2 | Persistencia e integración | R02 | T02 | Eliminación de cuenta |
| F3 | Seguridad, autorización y límites | R05 | T05 | Seguridad, privacidad y límites |
| F4 | Pruebas y evidencia | R06, R07 | T06, T07, T08, T09 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Vista previa y bloqueos** (R01): La vista previa cuenta lo que se borra o cancela y lista los bloqueos (`active_rental`, `staff_account`); coincide con lo que hace la eliminación.
- **T03 · Sesión siempre viva** (R03): Sin tope absoluto: una sesión usada cada menos de 30 días no vence; 30 días sin uso, logout, reuso, suspensión o eliminación la cierran; tokens existentes sin migración.
- **T04 · Aviso de privacidad y términos** (R04): `GET /legal` con versiones y URLs; aceptación registrada; `/users/me` con `legal` y `legalUpToDate`; versión distinta → 409.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T02 · Eliminación de cuenta** (R02): Contraseña y `ELIMINAR`; cada dato de la tabla de reglas queda borrado, cancelado, retirado o anonimizado (una prueba por módulo); pasos idempotentes; un fallo deja la sesión viva y repetir termina; al final 401 en toda ruta, refresh y login.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T05 · Seguridad, privacidad y límites** (R05): Reautenticación, rate limit, sin PII en logs, archivos borrados del almacenamiento, otra cuenta intacta, Redis caído 503.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T06 · Contrato para Mobile y despliegue** (R06): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure: cuenta de prueba con perfil, favorito, solicitud y visita, eliminada, y la contraparte ve «Usuario eliminado».
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
- T05 depende de: T01, T03, T04, T02.
- T06 depende de: T01, T03, T04, T02, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T07, T08.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Vista previa y bloqueos | Pruebas automatizadas en CI |
| R02 | Eliminación de cuenta | Pruebas automatizadas en CI |
| R03 | Sesión siempre viva | Pruebas automatizadas en CI |
| R04 | Aviso de privacidad y términos | Pruebas automatizadas en CI |
| R05 | Seguridad, privacidad y límites | Pruebas automatizadas en CI |
| R06 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R07 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Reautenticación con contraseña para eliminar (protege un teléfono desbloqueado o un token robado).
- BOLA: solo la cuenta del token; no existe forma de eliminar otra cuenta por esta ruta.
- Tras eliminar, el access token se rechaza en todas las réplicas (revocación por sesión en Redis, NV-10/NV-21).
- Sin correo, nombre ni contraseña en logs; el evento de auditoría guarda solo el id interno y la hora.
- Rate limit: 5 eliminaciones intentadas por hora por usuario y 30 lecturas por minuto; Redis caído → 503.
- Contratos estrictos: campos desconocidos → 400.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`deleted_accounts`) y un campo en `users`; sin recursos nuevos |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.2.0-borrador | PASS (M-BE-077) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-17) |
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
| R05 | F3 | T05 |
| R06 | F4 | T06 |
| R07 | F4 | T07, T08, T09 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.2.0 T01–T09 (2026-11-18): PASS WITH WARNINGS, ver `evidence.md`**.
