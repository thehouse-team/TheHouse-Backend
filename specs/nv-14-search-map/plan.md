# NV-14 — Búsqueda, filtros y mapa — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio (celdas, distancias y filtros) con pruebas unitarias; después la consulta en dos pasos, el detalle y las colonias; luego los seeds; al final límites, prueba de rendimiento, seeds autorizados en Production y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04 | T01, T02, T03, T04 | Búsqueda con filtros; Mapa de rentas; Conteo por presupuesto; Detalle con sesión |
| F2 | Persistencia e integración | R05, R06, R07 | T05, T06, T07 | Colonias y ciudades; Ubicación aproximada; Datos de demostración |
| F3 | Seguridad, autorización y límites | R08, R09 | T08, T09 | Índices y rendimiento; Anti-extracción y límites |
| F4 | Pruebas y evidencia | R10, R11 | T10, T11, T12, T13 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Búsqueda con filtros** (R01): Combina precio, tipo de cuarto, baño, tipo de edificio, con quién vivir, servicios y distancia; orden `distance`, `price` o `newest` estable con id; paginación máximo 50; solo cuartos publicables.
- **T02 · Mapa de rentas** (R02): Marcadores dentro de un `bbox` válido (máximo 0.3° por lado), máximo 300, con precio y distancia; `bbox` inválido 400.
- **T03 · Conteo por presupuesto** (R03): `rooms/count` devuelve el total con los mismos criterios que la búsqueda.
- **T04 · Detalle con sesión** (R04): Invitado 401; con sesión devuelve el detalle sin dirección, coordenadas reales ni dueño; cuarto no publicado 404.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T05 · Colonias y ciudades** (R05): Coincidencia parcial sin acentos ni mayúsculas, máximo 10, lista vacía con 200; seed autorizado desde OpenStreetMap.
- **T06 · Ubicación aproximada** (R06): Centro de celda estable para el mismo edificio y a menos de 250 m de la ubicación real; distancias calculadas desde la celda; prueba de que dos universidades no permiten triangular más allá de la celda.
- **T07 · Datos de demostración** (R07): `--seed-demo-rooms` crea los 12 cuartos `demo` verificados, idempotente, con arrendador de sistema sin acceso y mapeo propiedad → cuarto.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T08 · Índices y rendimiento** (R08): Índices geoespaciales y compuestos; prueba de carga básica con p95 < 500 ms en Azure con los datos de demostración.
- **T09 · Anti-extracción y límites** (R09): Límites de `pageSize`, `bbox` y marcadores; rate limits con 429; Redis caído 503; ningún campo privado en respuestas públicas.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T10 · Contrato para Mobile y despliegue** (R10): Contrato de la §6.1 entregado a Mobile con URL desplegada; seeds autorizados en Production; prueba HTTP real en Azure (búsqueda, mapa, conteo, detalle, colonias).
- **T11 · Pruebas unitarias y de integración** (R11): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T12 · Pruebas de seguridad** (R11): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T13 · Evidencia y revisión final** (R11): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T07 depende de: T01, T02, T03, T04.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- T11 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T12 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T13 depende de: T11, T12.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Búsqueda con filtros | Pruebas automatizadas en CI |
| R02 | Mapa de rentas | Pruebas automatizadas en CI |
| R03 | Conteo por presupuesto | Pruebas automatizadas en CI |
| R04 | Detalle con sesión | Pruebas automatizadas en CI |
| R05 | Colonias y ciudades | Pruebas automatizadas en CI |
| R06 | Ubicación aproximada | Pruebas automatizadas en CI |
| R07 | Datos de demostración | Pruebas automatizadas en CI |
| R08 | Índices y rendimiento | Pruebas automatizadas en CI |
| R09 | Anti-extracción y límites | Pruebas automatizadas en CI |
| R10 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R11 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Ningún endpoint público devuelve dirección, coordenadas reales, `ownerId`, teléfono ni nombre del arrendador (RNF-02).
- Anti-extracción: `pageSize` ≤ 50, `bbox` ≤ 0.3° por lado, máximo 300 marcadores, rate limit 60/min por IP en búsqueda, mapa, conteo y colonias; detalle 60/min por usuario.
- Las distancias desde la celda impiden triangular la ubicación exacta consultando varias universidades.
- El texto de búsqueda de colonias se escapa antes de usarse como patrón.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Lecturas con índices existentes; `places` con unas decenas de documentos; un comando Redis por solicitud |

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
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F3 | T08 |
| R09 | F3 | T09 |
| R10 | F4 | T10 |
| R11 | F4 | T11, T12, T13 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
