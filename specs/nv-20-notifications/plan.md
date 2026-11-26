# NV-20 — Notificaciones — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-20 |
| Documento | `plan.md` |
| Versión | 1.3.0 |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-18 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T08; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-20-notifications/`; sin comandos en Production) |
| Estado de ejecución | **🟡 DESPLEGADA — 1.2.0: 7 de 8 tasks (2026-11-19); T05 espera la prueba del push en un teléfono real con Mobile; 1.3.0 (6 avisos nuevos) PROPUESTA** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.3.0 |
| Tasks | `task.md` 1.3.0 |

**Cambio 1.3.0 (2026-11-26) — PROPUESTO, pendiente de la validación de Mobile del §6.1 y de «Apruebo NV-20 1.3.0 y EJECUTA T09–T12»:** la revisión de los servicios encontró hechos importantes que hoy no avisan a nadie: el estudiante no se entera de que tiene un convenio por aceptar ni de que se canceló, y el arrendador no se entera de que recibió una reseña. Esta versión agrega **6 tipos** a la bandeja (y por lo tanto al push, regla 1): `agreement_proposed`, `agreement_cancelled`, `review_received`, `review_hidden`, `favorite_unavailable` y `report_resolved` (§9.1), con dos destinos nuevos para Mobile (`room` y `none`). Sin endpoints nuevos, sin colecciones nuevas, sin paquetes y sin costo. Los recordatorios de visita quedan fuera: requieren un proceso programado que corra aunque la API esté en cero réplicas (Container Apps Job y Terraform).

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el registro de dispositivos y sus pruebas; después el cliente de FCM contra un servidor falso en las pruebas; luego el envío desde el puerto de la bandeja; al final privacidad, el secreto en la Container App y la prueba con un teléfono real.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01 | T01 | Registro de dispositivos |
| F2 | Persistencia e integración | R02, R03 | T02, T03 | Envío push de la bandeja; Cliente de FCM |
| F3 | Seguridad, autorización y límites | R04 | T04 | Privacidad, seguridad y límites |
| F4 | Pruebas y evidencia | R05, R06 | T05, T06, T07, T08 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |
| F5 | Avisos nuevos (1.3.0) | R07, R08, R09, R10 | T09, T10, T11, T12 | Avisos de convenio, reseñas, favoritos y reportes; contrato 1.3.0 |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Registro de dispositivos** (R01): PUT idempotente por instalación, máximo 5, un token por cuenta, DELETE y lista sin tokens; limpieza al logout, suspensión y eliminación.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T02 · Envío push de la bandeja** (R02): Los 16 tipos se envían a los dispositivos del destinatario con título, cuerpo y datos de destino; nunca al actor; agrupados por conversación.
- **T03 · Cliente de FCM** (R03): OAuth 2.0 con la cuenta de servicio, renovación del token de acceso, API HTTP v1, borrado de tokens inválidos y un reintento; un fallo nunca rompe la acción principal.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T04 · Privacidad, seguridad y límites** (R04): Sin contenido privado en push, sin tokens en logs ni respuestas, BOLA, rate limit, secreto solo en la Container App.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T05 · Contrato para Mobile y despliegue** (R05): Contrato §6.1 entregado a Mobile; secreto cargado por Victor; desplegado por el CD; prueba real en Azure con un teléfono: solicitud, mensaje y visita llegan como push.
- **T06 · Pruebas unitarias y de integración** (R06): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T07 · Pruebas de seguridad** (R06): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T08 · Evidencia y revisión final** (R06): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.


### F5 — Avisos nuevos (1.3.0)

**Requisitos:** NV-20-R07, NV-20-R08, NV-20-R09, NV-20-R10. **Tasks:** T09, T10, T11, T12.

- Catálogo y textos de los 6 tipos; destinos `room` y `none`.
- Publicación desde `AgreementService`, `RoomReviewService`, `ReviewModerationService`, `ReportService`, `RoomService`, el efecto de renta de NV-18 y la suspensión de edificios de NV-21.
- Consulta acotada en el puerto de favoritos para `favorite_unavailable`.
- Pruebas de integración, de privacidad y de arquitectura; contrato §6.1 validado por Mobile; despliegue y prueba real.

**Criterios de salida:** los 6 avisos llegan a la bandeja y al push en Azure con cuentas de prueba; las 7 pruebas de arquitectura siguen en verde.

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
- T02 depende de: T01.
- T03 depende de: T01.
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T06, T07.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Registro de dispositivos | Pruebas automatizadas en CI |
| R02 | Envío push de la bandeja | Pruebas automatizadas en CI |
| R03 | Cliente de FCM | Pruebas automatizadas en CI |
| R04 | Privacidad, seguridad y límites | Pruebas automatizadas en CI |
| R05 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R06 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |
| R07 | Avisos de convenio | Pruebas automatizadas en CI |
| R08 | Avisos de reseñas | Pruebas automatizadas en CI |
| R09 | Avisos de favoritos y reportes | Pruebas automatizadas en CI |
| R10 | Contrato 1.3.0 para Mobile y despliegue | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Autenticación a FCM con la cuenta de servicio por OAuth 2.0 (JWT firmado con RS256 con la criptografía de .NET, sin paquete nuevo); el token de acceso se guarda en memoria y se renueva antes de vencer.
- Secreto `FIREBASE_SERVICE_ACCOUNT` solo en la Container App; nunca en el repositorio, en Terraform, en logs ni en respuestas. La cuenta de servicio solo tiene el rol de enviar mensajes de FCM.
- Los tokens de FCM son datos personales: no se devuelven, no se registran en logs y solo se usan para enviar.
- BOLA: un usuario solo ve y borra sus dispositivos; `installationId` ajeno → 404.
- Push sin contenido privado: ni el texto de los mensajes, ni direcciones, ni teléfonos.
- Rate limit: 30 registros por hora por usuario; Redis caído → 503.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Firebase Cloud Messaging | $0 | FCM no cobra por mensaje |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`push_devices`) |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.2.0-borrador | PASS (M-BE-078) |
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
| R03 | F2 | T03 |
| R04 | F3 | T04 |
| R05 | F4 | T05 |
| R06 | F4 | T06, T07, T08 |
| R07 | F5 | T09 |
| R08 | F5 | T10 |
| R09 | F5 | T11 |
| R10 | F5 | T12 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 DESPLEGADA — 1.2.0: 7 de 8 tasks (2026-11-19); T05 espera la prueba del push en un teléfono real con Mobile; 1.3.0 (6 avisos nuevos) PROPUESTA**.
