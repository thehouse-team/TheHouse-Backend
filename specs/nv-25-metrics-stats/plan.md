# NV-25 — Estadísticas del arrendador — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-25 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T09; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-25-metrics-stats/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el registro de vistas en el detalle público y las reglas de conteo con pruebas unitarias; después el resumen y la lista por cuarto con sus consultas; luego las sugerencias; al final privacidad, límites y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Registro de vistas; Resumen del arrendador |
| F2 | Persistencia e integración | R03, R04 | T03, T04 | Estadísticas por cuarto; Sugerencias por reglas |
| F3 | Seguridad, autorización y límites | R05 | T05 | Privacidad, autorización y límites |
| F4 | Pruebas y evidencia | R06, R07 | T06, T07, T08, T09 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Registro de vistas** (R01): Una vista por persona, cuarto o edificio y día con `actorHash` de sal diaria; sin vistas del dueño; dos réplicas no duplican; si falla, el detalle público responde igual.
- **T02 · Resumen del arrendador** (R02): Totales del rango, periodo anterior y serie diaria con conversión; solo datos propios; rango validado (90 días).

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Estadísticas por cuarto** (R03): Vistas, favoritos, solicitudes, visitas, convenios, conversión y calificación por cuarto; paginado y ordenado; filtro por edificio propio.
- **T04 · Sugerencias por reglas** (R04): Las 6 reglas documentadas, cada una con una prueba que la dispara y otra que no; máximo 20; textos en español con números del arrendador.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T05 · Privacidad, autorización y límites** (R05): Sin datos de usuarios en ninguna respuesta; otro arrendador o estudiante 403/404; 429; Redis caído 503 en estadísticas.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T06 · Contrato para Mobile y despliegue** (R06): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (vistas de un estudiante, favorito, visita y solicitud reflejados en el resumen, por cuarto y en sugerencias).
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
- T02 depende de: autorización «EJECUTA».
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T07, T08.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Registro de vistas | Pruebas automatizadas en CI |
| R02 | Resumen del arrendador | Pruebas automatizadas en CI |
| R03 | Estadísticas por cuarto | Pruebas automatizadas en CI |
| R04 | Sugerencias por reglas | Pruebas automatizadas en CI |
| R05 | Privacidad, autorización y límites | Pruebas automatizadas en CI |
| R06 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R07 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: todo se filtra por el `ownerId` del token; `buildingId` ajeno → 404.
- Privacidad (RNF-11): solo agregados; nunca ids, nombres ni hashes de quien vio, guardó o solicitó; los hashes cambian de sal cada día.
- El registro de vistas no agrega cookies ni identificadores al cliente y no cambia la respuesta pública.
- Rate limit: 30/min por usuario; Redis caído → 503 en estadísticas (el detalle público sigue respondiendo).
- Parámetros desconocidos o inválidos → 400.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección pequeña (`room_views`, un documento por persona, cuarto y día) y una llave de Redis al día |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | PASS (M-BE-075) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-17) |
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
| R06 | F4 | T06 |
| R07 | F4 | T07, T08, T09 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.1.0 T01–T09 (2026-11-17): PASS WITH WARNINGS, ver `evidence.md`**.
