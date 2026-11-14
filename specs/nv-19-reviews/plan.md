# NV-19 — Reseñas y calificaciones — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-19 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-13 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-13 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-19-reviews/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero la elegibilidad y el dominio de la reseña (criterios, promedios, plazo de edición) con pruebas unitarias; después crear, editar, listar con resumen y mis reseñas; luego el reporte y el ocultamiento en NV-21; al final privacidad, límites y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Elegibilidad verificada; Crear y editar |
| F2 | Persistencia e integración | R03, R04, R05 | T03, T04, T05 | Listado y resumen; Mis reseñas; Moderación |
| F3 | Seguridad, autorización y límites | R06 | T06 | Privacidad, autorización y límites |
| F4 | Pruebas y evidencia | R07, R08 | T07, T08, T09, T10 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Elegibilidad verificada** (R01): Visita confirmada pasada o convenio activo o terminado del autor en ese cuarto; el dueño 403 `own_room`; sin estancia 403 `no_stay`; la consulta de elegibilidad responde lo mismo que crear.
- **T02 · Crear y editar** (R02): 9 criterios de 1 a 5 obligatorios al crear y comentario ≤ 1000; una por autor y cuarto (409 `review_exists`); editar solo el autor durante 30 días (409 `review_locked`); promedios de la reseña con un decimal.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Listado y resumen** (R03): Reseñas publicadas del cuarto paginadas por fecha; resumen con promedio del cuarto, del arrendador (todas sus reseñas) y por criterio; sin reseñas, promedios `null`.
- **T04 · Mis reseñas** (R04): Lista del autor con su estado, incluidas las ocultas.
- **T05 · Moderación** (R05): Reporte con objetivo `review` en NV-21; ocultar y mostrar solo con rol `admin`, motivo y bitácora; una oculta deja de contar en los promedios.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Privacidad, autorización y límites** (R06): Pruebas con autor, arrendador, tercero y admin; autor solo con primer nombre; rate limits 429; Redis caído 503; campos desconocidos 400.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Contrato para Mobile y despliegue** (R07): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (no elegible, elegible por renta, crear, listar con resumen, editar, reportar y ocultar).
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
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Elegibilidad verificada | Pruebas automatizadas en CI |
| R02 | Crear y editar | Pruebas automatizadas en CI |
| R03 | Listado y resumen | Pruebas automatizadas en CI |
| R04 | Mis reseñas | Pruebas automatizadas en CI |
| R05 | Moderación | Pruebas automatizadas en CI |
| R06 | Privacidad, autorización y límites | Pruebas automatizadas en CI |
| R07 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Elegibilidad verificada en el servidor con las visitas y convenios del token; nunca con datos del cliente.
- BOLA: solo el autor edita (lo ajeno 404); el arrendador no puede calificar sus cuartos (403 `own_room`).
- El autor aparece solo con su primer nombre; nunca correo, teléfono, id ni universidad. Los textos de la reseña no se registran en logs.
- Rate limit 60/min por usuario y 10 reseñas nuevas al día; Redis caído → 503.
- Entradas con campos desconocidos → 400 (sin `authorId`, `status` ni promedios del cliente).
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva (`reviews`) con cuatro índices; sin proveedores nuevos |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | PASS (M-BE-072) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-13) |
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
| R06 | F3 | T06 |
| R07 | F4 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas**.
