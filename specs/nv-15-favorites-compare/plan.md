# NV-15 — Favoritos — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-15 |
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

Primero el contrato (incluido el de Mobile) y la idempotencia; después índices y lectura por lotes; al final límites, pruebas de dos usuarios y la prueba real en Azure antes de avisar a Mobile.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04 | T01, T02, T03, T04 | Agregar favorito idempotente; Quitar favorito idempotente; Listado paginado; Ids de favoritos |
| F2 | Persistencia e integración | R05 | T05 | Persistencia e índices |
| F3 | Seguridad, autorización y límites | R06 | T06 | Autorización y límites |
| F4 | Pruebas y evidencia | R07, R08 | T07, T08, T09, T10 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Agregar favorito idempotente** (R01): `PUT` con propiedad existente responde 204 y crea el favorito una sola vez; repetirlo responde 204 sin duplicar; propiedad inexistente 404; id inválido 400; límite de 200 → 409.
- **T02 · Quitar favorito idempotente** (R02): `DELETE` responde 204 exista o no el favorito; después la propiedad ya no aparece en el listado ni en `ids`.
- **T03 · Listado paginado** (R03): `GET /users/me/favorites` devuelve el sobre de paginación de NV-01, orden `favoritedAt` desc y `propertyId` desc, con datos públicos de la propiedad y sin propiedades inexistentes.
- **T04 · Ids de favoritos** (R04): `GET /users/me/favorites/ids` devuelve todos los ids favoritos vigentes del usuario (máximo 200) en una sola respuesta.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T05 · Persistencia e índices** (R05): Índice único `{ userId, propertyId }` probado con solicitudes concurrentes; listado sin N+1 (lectura de propiedades por lotes).

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Autorización y límites** (R06): Sin `userId` del cliente; pruebas con dos usuarios (uno no ve ni altera los favoritos del otro); rate limit 429 con `Retry-After`; Redis caído 503; `no-store`.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Contrato para Mobile y despliegue** (R07): Contrato de la §6.1 entregado a Mobile con URL desplegada, resultado de pruebas y prueba HTTP real en Azure (agregar, repetir, listar, ids, quitar, repetir).
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
- T04 depende de: autorización «EJECUTA».
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Agregar favorito idempotente | Pruebas automatizadas en CI |
| R02 | Quitar favorito idempotente | Pruebas automatizadas en CI |
| R03 | Listado paginado | Pruebas automatizadas en CI |
| R04 | Ids de favoritos | Pruebas automatizadas en CI |
| R05 | Persistencia e índices | Pruebas automatizadas en CI |
| R06 | Autorización y límites | Pruebas automatizadas en CI |
| R07 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA imposible por diseño: no se aceptan identificadores de usuario; cada consulta filtra por `sub`.
- Rate limit en Redis de 60 solicitudes por 60 s por usuario para todo el módulo; si Redis falla, 503 (fallo cerrado).
- Respuestas con `no-store`; los logs no incluyen ids de usuario ni de propiedades favoritas.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Documentos pequeños; un comando Redis por solicitud |

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
| R05 | F2 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
