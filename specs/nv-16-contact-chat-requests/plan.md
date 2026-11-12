# NV-16 — Contacto, solicitudes y chat — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-16 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-16-contact-chat-requests/`) |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T11 (2026-11-11): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio (estados, transiciones, límites y privacidad) con pruebas unitarias; después solicitudes y bandejas; luego conversaciones, mensajes, cursor, idempotencia y no leídos; al final límites, suspensión, privacidad y la prueba real en Azure con dos cuentas de prueba.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Crear solicitud; Bandeja del estudiante y cancelación; Bandeja del arrendador, aceptar y rechazar |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Chat por consulta incremental; No leídos y lectura; Persistencia e índices |
| F3 | Seguridad, autorización y límites | R07 | T07 | Privacidad y abuso |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Crear solicitud** (R01): Valida preguntas del catálogo y mensaje; crea solicitud y conversación en la misma operación; 404 cuarto no publicado; 409 activa o límite; 422 propio cuarto; 409 participante suspendido.
- **T02 · Bandeja del estudiante y cancelación** (R02): Lista, detalle y cancelación de sus solicitudes con la visita activa de NV-17; transiciones válidas; lo ajeno 404.
- **T03 · Bandeja del arrendador, aceptar y rechazar** (R03): Lista por estado y cuarto, detalle, aceptar y rechazar con motivo; solo el dueño del cuarto; 409 en transiciones inválidas o cuarto no publicado.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Chat por consulta incremental** (R04): Mensajes ordenados con cursor `after`, `hasMore` y `ETag`/304; envío idempotente por `clientMessageId`; conversación cerrada 409.
- **T05 · No leídos y lectura** (R05): `unreadCount` por conversación y `read` hasta un mensaje; nunca cuenta los propios mensajes.
- **T06 · Persistencia e índices** (R06): Índice único parcial de solicitudes activas, único de `clientMessageId` y de lectura de mensajes por conversación; prueba de doble toque concurrente.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Privacidad y abuso** (R07): Ninguna respuesta incluye teléfono, correo, apellidos ni ids de usuario; rate limits con 429; Redis caído 503; cuentas suspendidas no escriben; contenido fuera de logs.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure con un estudiante y un arrendador de prueba (solicitar, aceptar, rechazar, cancelar, chatear, 304 y no leídos).
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
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Crear solicitud | Pruebas automatizadas en CI |
| R02 | Bandeja del estudiante y cancelación | Pruebas automatizadas en CI |
| R03 | Bandeja del arrendador, aceptar y rechazar | Pruebas automatizadas en CI |
| R04 | Chat por consulta incremental | Pruebas automatizadas en CI |
| R05 | No leídos y lectura | Pruebas automatizadas en CI |
| R06 | Persistencia e índices | Pruebas automatizadas en CI |
| R07 | Privacidad y abuso | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: el estudiante solo ve sus solicitudes, el arrendador solo las de sus cuartos y cada conversación solo sus dos participantes; lo ajeno responde 404.
- Teléfono, correo, apellidos e ids de usuario nunca aparecen en solicitudes ni en el chat (RNF-02).
- Rate limit: 10 solicitudes nuevas al día por estudiante, 30 mensajes por minuto por usuario, 12 consultas de mensajes por minuto por conversación y 60/min por usuario en el resto.
- El contenido de solicitudes y mensajes no se registra en logs.
- Asignación masiva bloqueada: el cuerpo no acepta `status`, `studentId`, `ownerId` ni `senderId`.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Mensajes pequeños; la consulta incremental cada 10 s con 304 y un comando Redis por consulta; se mide el consumo de comandos en la prueba de Azure |

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
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F4 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.1.0 T01–T11 (2026-11-11): PASS WITH WARNINGS, ver `evidence.md`**.
