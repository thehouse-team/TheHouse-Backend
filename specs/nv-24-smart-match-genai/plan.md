# NV-24 — Smart Match — explicaciones con TheHouse-GenAI — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-24 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-10 («Apruebo y EJECUTA NV-16, NV-23 y NV-24»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-24-smart-match-genai/`; activar `TheHouse-GenAI` en Production requiere las decisiones M-BE de su hosting y de su proveedor) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — T01–T11 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero hechos, plantillas y endpoints con pruebas unitarias (funciona sin GenAI); después el cliente de GenAI con un doble de prueba, la validación y la degradación; al final caché, límites, seguridad y la prueba real en Azure con plantilla.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Hechos estructurados; Ventajas, desventajas y plantilla; Endpoints de explicación |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Cliente de TheHouse-GenAI; Validación y degradación segura; Caché y límites |
| F3 | Seguridad, autorización y límites | R07 | T07 | Protección contra inyección y minimización |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Hechos estructurados** (R01): Los hechos se arman solo desde NV-23 y los datos públicos del cuarto; prueba de que no incluyen descripción libre, dirección, coordenadas ni datos del usuario.
- **T02 · Ventajas, desventajas y plantilla** (R02): Ventajas y desventajas por umbral del desglose y resumen por plantilla en español de México, deterministas, sin porcentajes por factor y con máximo de 600 caracteres; casos de prueba fijos.
- **T03 · Endpoints de explicación** (R03): `explanations/{roomId}` con sesión y `explanations/query` para invitados; 404 cuarto no publicado o no elegible; 422 perfil incompleto.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Cliente de TheHouse-GenAI** (R04): `GenAiExplanationClient` con el contrato `v1` de la §6, credencial de servicio, 3 s de timeout, un reintento y circuit breaker; probado con un doble de GenAI, sin red.
- **T05 · Validación y degradación segura** (R05): Respuestas con números, servicios o universidades ausentes, demasiado largas o con formato inválido usan la plantilla; con GenAI caído, lento, desactivado o con el circuito abierto responde 200 con `generatedBy=template`.
- **T06 · Caché y límites** (R06): Caché de 24 h por cuarto, preferencias y versiones; rate limits con 429; Redis caído 503.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Protección contra inyección y minimización** (R07): Pruebas con descripciones maliciosas del arrendador que no llegan a GenAI ni alteran la salida; ningún dato personal en la solicitud a GenAI ni en logs.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato §6.1 entregado a Mobile; desplegado por el CD con GenAI desactivado; prueba HTTP real en Azure con plantilla; activar GenAI en Production solo con sus decisiones M-BE aprobadas y una prueba real adicional.
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
| R01 | Hechos estructurados | Pruebas automatizadas en CI |
| R02 | Ventajas, desventajas y plantilla | Pruebas automatizadas en CI |
| R03 | Endpoints de explicación | Pruebas automatizadas en CI |
| R04 | Cliente de TheHouse-GenAI | Pruebas automatizadas en CI |
| R05 | Validación y degradación segura | Pruebas automatizadas en CI |
| R06 | Caché y límites | Pruebas automatizadas en CI |
| R07 | Protección contra inyección y minimización | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Solo el backend llama a `TheHouse-GenAI`, con credencial de servicio; nunca reenvía el JWT del usuario (constitution §6.1).
- Prompt injection: el contexto se arma en el servidor con campos estructurados; los textos libres del arrendador no se envían.
- Sin datos personales hacia GenAI (RNF-11) y sin prompts ni explicaciones completas en logs.
- Rate limit 20/min por usuario y 10/min por IP de invitado; caché para no repetir llamadas.
- Si GenAI no está disponible el endpoint sigue respondiendo 200 con plantilla (RNF-16); 503 solo si fallan MongoDB o Redis.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Plantillas en el backend | $0 | Siempre disponibles |
| `TheHouse-GenAI` (hosting y proveedor) | $0 solo con evidencia de §17 | PENDIENTE; mientras tanto desactivado y sin costo |
| Caché en MongoDB Atlas M0 | $0 | TTL de 24 h |

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

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — T01–T11 autorizadas**.
