# NV-20 — Notificaciones — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-12 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-20-notifications/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T07 completas; T08–T11 esperan CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el catálogo, las plantillas y el puerto con pruebas unitarias (destinatario, texto sin datos sensibles, idempotencia y agrupación); después la bandeja, el contador y las marcas; luego las llamadas desde NV-16, NV-17, NV-18 y NV-21; al final privacidad, límites y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Generación desde las acciones; Textos sin datos sensibles |
| F2 | Persistencia e integración | R03, R04, R05, R06 | T03, T04, T05, T06 | Bandeja y filtros; Marcar como leídas; Contador eficiente; Agrupación de mensajes y retención |
| F3 | Seguridad, autorización y límites | R07 | T07 | Privacidad, autorización y límites |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Generación desde las acciones** (R01): Cada tipo del catálogo se crea para el destinatario correcto cuando ocurre su acción en NV-16, NV-17, NV-18 o NV-21; repetir el hecho no duplica; quien actúa no se notifica; si MongoDB falla la acción principal no se deshace.
- **T02 · Textos sin datos sensibles** (R02): Cada tipo produce `title` y `body` en español con nombre sin apellidos y título del cuarto; ninguno contiene dirección, teléfono, correo, texto del chat, notas ni ids.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Bandeja y filtros** (R03): Lista paginada por `updatedAt` desc con filtros `unread` y `category`; validación de parámetros.
- **T04 · Marcar como leídas** (R04): Una (idempotente; ajena 404) o todas, opcionalmente por categoría; leer el chat marca la notificación de esa conversación.
- **T05 · Contador eficiente** (R05): Total y por categoría con índice; `ETag` débil, `private, no-cache` y 304 cuando no cambió.
- **T06 · Agrupación de mensajes y retención** (R06): Una notificación no leída por conversación con `count`; TTL de 90 días.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Privacidad, autorización y límites** (R07): Pruebas con dos usuarios; rate limits 429; Redis caído 503; campos desconocidos 400.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (solicitud, aceptación, mensaje agrupado, visita confirmada, convenio, contador con 304 y marcar leídas).
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
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02.
- T06 depende de: T01, T02.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Generación desde las acciones | Pruebas automatizadas en CI |
| R02 | Textos sin datos sensibles | Pruebas automatizadas en CI |
| R03 | Bandeja y filtros | Pruebas automatizadas en CI |
| R04 | Marcar como leídas | Pruebas automatizadas en CI |
| R05 | Contador eficiente | Pruebas automatizadas en CI |
| R06 | Agrupación de mensajes y retención | Pruebas automatizadas en CI |
| R07 | Privacidad, autorización y límites | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: cada consulta filtra por el `sub`; una notificación ajena responde 404.
- Textos sin datos sensibles (RNF-02): sin dirección exacta, teléfono, correo, texto del chat ni notas; prueba que lo verifica para cada tipo.
- El contador usa `ETag` con `Cache-Control: private, no-cache` para que ninguna caché compartida lo guarde (constitution §23).
- Rate limit 60/min por usuario en la bandeja y las marcas, y 30/min en el contador; Redis caído → 503.
- Entradas con campos desconocidos → 400.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva con TTL de 90 días; sin proveedores nuevos |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | PASS (M-BE-071) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-12) |
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
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F4 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — 1.1.0: T01–T07 completas; T08–T11 esperan CI, despliegue y Azure**.
